using System;
using System.Collections.Generic;
using System.IO;
using Arcade.Compose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public class AdeStorageMigrationTests
{
    private string root;

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "affdata-storage-tests-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Test]
    public void MigrationVerifiesPersistedValuesBeforeClearingLegacy()
    {
        string path = Path.Combine(root, "settings.json");
        var settings = new AdeSettingsFile(path);
        settings.Load();
        bool cleared = false;
        settings.Migrate(new Dictionary<string, JToken>
        {
            ["sound"] = JObject.Parse("{\"Chart\":0.7}"),
            ["darkTheme"] = "claude-dark"
        }, () =>
        {
            var reloaded = new AdeSettingsFile(path);
            reloaded.Load();
            Assert.That(reloaded.Get("sound")["Chart"].Value<double>(), Is.EqualTo(.7));
            Assert.That(reloaded.Get("darkTheme").Value<string>(), Is.EqualTo("claude-dark"));
            cleared = true;
        });
        Assert.That(cleared, Is.True);
    }

    [Test]
    public void FailedWriteDoesNotClearLegacyAndCanBeRetried()
    {
        Directory.CreateDirectory(root);
        string blocker = Path.Combine(root, "blocked");
        File.WriteAllText(blocker, "file blocks parent directory");
        var settings = new AdeSettingsFile(Path.Combine(blocker, "settings.json"));
        settings.Load();
        bool cleared = false;
        var legacy = new Dictionary<string, JToken> { ["darkMode"] = 0 };
        Assert.Throws<IOException>(() => settings.Migrate(legacy, () => cleared = true));
        Assert.That(cleared, Is.False);
        File.Delete(blocker);
        settings.Migrate(legacy, () => cleared = true);
        Assert.That(cleared, Is.True);
    }

    [Test]
    public void CorruptFileCannotBeOverwrittenByDefaultSettings()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, "broken{");
        var settings = new AdeSettingsFile(path);
        Assert.Throws<JsonReaderException>(() => settings.Load());
        settings.Set("darkMode", 1);
        Assert.Throws<InvalidOperationException>(() => settings.Save());
        Assert.That(File.ReadAllText(path), Is.EqualTo("broken{"));
    }

    [Test]
    public void InterruptedCleanupKeepsNewPreferencesOnRetry()
    {
        string path = Path.Combine(root, "settings.json");
        var settings = new AdeSettingsFile(path);
        settings.Load();
        var old = new Dictionary<string, JToken> { ["darkMode"] = 0 };
        Assert.Throws<IOException>(() => settings.Migrate(old, () => throw new IOException("cleanup interrupted")));
        settings.Set("darkMode", 1);
        settings.Save();
        var restarted = new AdeSettingsFile(path);
        restarted.Load();
        restarted.Migrate(old, () => { });
        Assert.That(restarted.Get("darkMode").Value<int>(), Is.EqualTo(1));
        Assert.That(File.Exists(path + ".bak"), Is.True);
    }

    [Test]
    public void CloudMigrationPreservesConflictsAndOnlyRemovesVerifiedSources()
    {
        string source = Path.Combine(root, "old", "Cloud");
        string target = Path.Combine(root, "Charts", "Cloud");
        Directory.CreateDirectory(Path.Combine(source, "42", "Arcade"));
        Directory.CreateDirectory(Path.Combine(target, "42"));
        File.WriteAllText(Path.Combine(source, "42", "2.aff"), "old chart");
        File.WriteAllText(Path.Combine(source, "42", "Arcade", "Project.arcade"), "metadata");
        File.WriteAllText(Path.Combine(target, "42", "2.aff"), "new edited chart");
        string local = Path.Combine(root, "local.aff");
        File.WriteAllText(local, "local untouched");
        AdeStorageMigration.MoveDirectory(source, target);
        Assert.That(Directory.Exists(source), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(target, "42", "2.aff")), Is.EqualTo("new edited chart"));
        string[] backups = Directory.GetFiles(Path.Combine(target, ".migration-backup"), "2.aff", SearchOption.AllDirectories);
        Assert.That(backups.Length, Is.EqualTo(1));
        Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("old chart"));
        Assert.That(File.ReadAllText(local), Is.EqualTo("local untouched"));
        AdeStorageMigration.MoveDirectory(source, target);
    }

    [Test]
    public void MovingEditorSettingsPreservesExistingRootSettingsAndOtherUserFiles()
    {
        string oldFolder = Path.Combine(root, "UserSettings");
        Directory.CreateDirectory(oldFolder);
        string source = Path.Combine(oldFolder, "settings.json");
        string target = Path.Combine(root, "settings.json");
        File.WriteAllText(source, "old settings");
        File.WriteAllText(target, "current settings");
        File.WriteAllText(Path.Combine(oldFolder, "EditorUserSettings.asset"), "Unity data");
        AdeStorageMigration.MoveFile(source, target);
        Assert.That(File.Exists(source), Is.False);
        Assert.That(File.ReadAllText(target), Is.EqualTo("current settings"));
        string[] backups = Directory.GetFiles(root, "settings.json.migration-backup-*");
        Assert.That(backups.Length, Is.EqualTo(1));
        Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("old settings"));
        Assert.That(File.ReadAllText(Path.Combine(oldFolder, "EditorUserSettings.asset")), Is.EqualTo("Unity data"));
    }

    [Test]
    public void FailedCloudMigrationRetainsTheSource()
    {
        string source = Path.Combine(root, "old");
        string target = Path.Combine(root, "blocked");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "2.aff"), "chart");
        File.WriteAllText(target, "not a directory");
        Assert.Throws<IOException>(() => AdeStorageMigration.MoveDirectory(source, target));
        Assert.That(File.ReadAllText(Path.Combine(source, "2.aff")), Is.EqualTo("chart"));
    }

    [Test]
    public void DownloadReplacementKeepsTheEntirePreviousProject()
    {
        string target = Path.Combine(root, "42");
        string staging = Path.Combine(root, "download");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(target, "2.aff"), "edited chart");
        File.WriteAllText(Path.Combine(staging, "2.aff"), "remote chart");
        AdeStorageMigration.InstallDownload(staging, target);
        Assert.That(File.ReadAllText(Path.Combine(target, "2.aff")), Is.EqualTo("remote chart"));
        string[] backup = Directory.GetFiles(Path.Combine(root, ".backups"), "2.aff", SearchOption.AllDirectories);
        Assert.That(backup.Length, Is.EqualTo(1));
        Assert.That(File.ReadAllText(backup[0]), Is.EqualTo("edited chart"));
    }

    [Test]
    public void FailedDownloadReplacementRestoresThePreviousProject()
    {
        string target = Path.Combine(root, "42");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "2.aff"), "edited chart");
        Assert.Throws<DirectoryNotFoundException>(() => AdeStorageMigration.InstallDownload(Path.Combine(root, "missing"), target));
        Assert.That(File.ReadAllText(Path.Combine(target, "2.aff")), Is.EqualTo("edited chart"));
    }
}
