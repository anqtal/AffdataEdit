using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Microsoft.Win32;
#endif

namespace Arcade.Compose
{
    // Earlier builds used AffdataEdit as the company name; the current company is
    // Affdata. Unity cannot read the former PlayerPrefs domain through its public API.
    internal sealed class AdeLegacyPreferences
    {
        private const string MacDomain = "unity.AffdataEdit.AffdataEdit";
        private readonly Dictionary<string, object> previous = new Dictionary<string, object>();
        private readonly bool hasPrevious;

        public AdeLegacyPreferences()
        {
            if (Application.companyName == "AffdataEdit") return;
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    "Library", "Preferences", MacDomain + ".plist");
                if (!File.Exists(path)) return;
                string xml = RunDefaults("export " + MacDomain + " -");
                var dictionary = XDocument.Parse(xml).Root?.Element("dict");
                if (dictionary == null) throw new InvalidDataException("Invalid legacy PlayerPrefs property list.");
                var values = new List<XElement>(dictionary.Elements());
                for (int i = 0; i + 1 < values.Count; i += 2)
                {
                    if (values[i].Name != "key") throw new InvalidDataException("Invalid legacy PlayerPrefs key.");
                    XElement value = values[i + 1];
                    if (value.Name == "string") previous[values[i].Value] = value.Value;
                    else if (value.Name == "integer") previous[values[i].Value] = long.Parse(value.Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                hasPrevious = true;
            }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            else if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(WindowsKey))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames()) previous[name] = key.GetValue(name);
                    hasPrevious = true;
                }
            }
#endif
        }

        private bool TryPrevious(string name, out object value)
        {
            if (previous.TryGetValue(name, out value)) return true;
            foreach (var item in previous)
                if (item.Key.StartsWith(name + "_h", StringComparison.Ordinal)) { value = item.Value; return true; }
            return false;
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(key) || TryPrevious(key, out _);

        public string GetString(string key)
        {
            if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetString(key);
            if (!TryPrevious(key, out object value)) return "";
            return value is byte[] bytes ? Encoding.UTF8.GetString(bytes).TrimEnd('\0') : Convert.ToString(value);
        }

        public int GetInt(string key)
        {
            if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetInt(key);
            return TryPrevious(key, out object value) ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }

        public void Clear()
        {
            if (hasPrevious)
            {
                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        "Library", "Preferences", MacDomain + ".plist");
                    // Importing an empty dictionary is idempotent; `defaults delete`
                    // fails for an already-empty domain even when its plist still exists.
                    if (File.Exists(path)) RunDefaults("import " + MacDomain + " -", "<plist version=\"1.0\"><dict/></plist>");
                }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                else Registry.CurrentUser.DeleteSubKeyTree(WindowsKey, false);
#endif
            }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static string WindowsKey => Application.isEditor
            ? @"Software\Unity\UnityEditor\AffdataEdit\AffdataEdit" : @"Software\AffdataEdit\AffdataEdit";
#endif

        private static string RunDefaults(string arguments, string input = null)
        {
            using (var process = Process.Start(new ProcessStartInfo("/usr/bin/defaults", arguments)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = input != null, CreateNoWindow = true
            }))
            {
                if (input != null)
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.Close();
                }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException("Cannot migrate legacy PlayerPrefs: " + error);
                return output;
            }
        }
    }
}
