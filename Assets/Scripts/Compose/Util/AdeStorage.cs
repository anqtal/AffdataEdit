using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Arcade.Compose
{
    public static class AdeStorage
    {
        public static string DataFolder
        {
            get
            {
                string folder = Path.GetDirectoryName(Application.dataPath);
                if (Application.platform == RuntimePlatform.OSXPlayer) folder = Path.GetDirectoryName(folder);
                return folder;
            }
        }

        public static string SettingsPath => Path.Combine(DataFolder, "settings.json");
        public static string CloudChartsFolder => Path.Combine(DataFolder, "Charts", "Cloud");
        public static string LegacyFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arcade");

        public static string AccountPath
        {
            get
            {
                string folder = IsMac
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "AffdataEdit", "Account")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AffdataEdit", "Account");
                if (Application.isEditor) folder = Path.Combine(folder, "Editor");
                return Path.Combine(folder, "account.json");
            }
        }

        private static bool IsMac => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

        internal static void PrepareAccountFolder()
        {
            string folder = Path.GetDirectoryName(AccountPath);
            Directory.CreateDirectory(folder);
            // The account file stays with this OS user, outside the portable install.
            // Windows inherits the user's LocalAppData ACL; macOS restricts the directory.
            if (IsMac && chmod(folder, 448) != 0) // 0700
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot protect the account directory.");
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string path, uint mode);
    }
}
