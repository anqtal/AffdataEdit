using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Arcade.Compose
{
    // File operations are separate from PlayerPrefs so migration can be checked without
    // changing the user's actual preferences.
    public sealed class AdeSettingsFile
    {
        private readonly string path;
        private JObject data = new JObject { ["version"] = 1 };
        private bool loaded;
        private bool dirty;

        public AdeSettingsFile(string path) { this.path = path; }

        public void Load()
        {
            if (File.Exists(path))
            {
                JObject saved = JObject.Parse(File.ReadAllText(path));
                if (saved.Value<int?>("version") != 1)
                    throw new InvalidDataException("Unsupported settings.json version.");
                data = saved;
            }
            loaded = true;
        }

        public JToken Get(string key) => data[key]?.DeepClone();

        public void Set(string key, JToken value)
        {
            if (JToken.DeepEquals(data[key], value)) return;
            data[key] = value.DeepClone();
            dirty = true;
        }

        public void Migrate(IReadOnlyDictionary<string, JToken> legacy, Action clearLegacy)
        {
            foreach (var entry in legacy)
                if (data[entry.Key] == null) Set(entry.Key, entry.Value);
            // Always verify a durable copy before removing any old preference, including
            // after an interrupted cleanup where the new file already has these values.
            Save();
            Verify(path);
            clearLegacy();
        }

        public void Save()
        {
            if (!loaded) throw new InvalidOperationException("Settings were not loaded; refusing to overwrite the existing file.");
            if (!dirty && File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(data.ToString(Formatting.Indented));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            Verify(temporary);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            Verify(path);
            dirty = false;
        }

        private void Verify(string filename)
        {
            if (!JToken.DeepEquals(data, JObject.Parse(File.ReadAllText(filename))))
                throw new IOException("Settings verification failed: " + filename);
        }
    }
}
