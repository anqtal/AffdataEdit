using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Arcade.Compose
{
    public static class AdeStorageMigration
    {
        public static void MoveFile(string source, string destination)
        {
            if (!File.Exists(source)) return;
            if (File.Exists(destination) && !SameFile(source, destination))
                destination += ".migration-backup-" + Hash(source);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination))
            {
                string temporary = destination + ".migrating";
                File.Copy(source, temporary, true);
                if (!SameFile(source, temporary)) throw new IOException("Migration verification failed: " + source);
                File.Move(temporary, destination);
            }
            if (!SameFile(source, destination)) throw new IOException("Migration verification failed: " + source);
            File.Delete(source);
        }

        public static void InstallDownload(string staging, string destination)
        {
            string backup = null;
            if (Directory.Exists(destination))
            {
                backup = Path.Combine(Path.GetDirectoryName(destination), ".backups",
                    Path.GetFileName(destination) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                Directory.Move(destination, backup);
            }
            try { Directory.Move(staging, destination); }
            catch
            {
                if (backup != null) Directory.Move(backup, destination);
                throw;
            }
        }

        // Copy and verify the entire batch before deleting sources. Existing different
        // versions are retained in a deterministic backup, making interrupted runs safe.
        public static void MoveDirectory(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            var copied = new Dictionary<string, string>();
            foreach (string original in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = original.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                string target = Path.Combine(destination, relative);
                if (File.Exists(target) && !SameFile(original, target))
                    target = Path.Combine(destination, ".migration-backup", Hash(original), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target))
                {
                    string temporary = target + ".migrating";
                    File.Copy(original, temporary, true);
                    if (!SameFile(original, temporary)) throw new IOException("Cloud migration verification failed: " + original);
                    File.Move(temporary, target);
                }
                copied.Add(original, target);
            }
            foreach (var pair in copied)
                if (!SameFile(pair.Key, pair.Value)) throw new IOException("Cloud migration verification failed: " + pair.Key);
            foreach (var pair in copied)
            {
                if (!SameFile(pair.Key, pair.Value)) throw new IOException("Cloud source changed during migration: " + pair.Key);
                File.Delete(pair.Key);
            }
            RemoveEmptyDirectories(source);
        }

        private static void RemoveEmptyDirectories(string path)
        {
            foreach (string child in Directory.GetDirectories(path)) RemoveEmptyDirectories(child);
            if (Directory.GetFileSystemEntries(path).Length == 0) Directory.Delete(path);
        }

        private static bool SameFile(string first, string second) => File.Exists(second)
            && new FileInfo(first).Length == new FileInfo(second).Length && Hash(first) == Hash(second);

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
