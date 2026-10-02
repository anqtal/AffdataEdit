#if UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SFB
{
    public class StandaloneFileBrowserWindows : IStandaloneFileBrowser
    {
        private const int Cancelled = unchecked((int)0x800704C7);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("AffdataFileBrowser", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int AdeShowFileDialog(int mode, IntPtr owner, string title,
            string directory, string name, string extension, string filters, int multiple,
            out IntPtr result);

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) =>
            Show(0, title, directory, "", extensions, multiselect);

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) =>
            Show(1, title, directory, "", null, multiselect);

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions)
        {
            string[] result = Show(2, title, directory, defaultName, extensions, false);
            return result.Length == 0 ? "" : result[0];
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) =>
            cb.Invoke(OpenFilePanel(title, directory, extensions, multiselect));

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) =>
            cb.Invoke(OpenFolderPanel(title, directory, multiselect));

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) =>
            cb.Invoke(SaveFilePanel(title, directory, defaultName, extensions));

        private static string[] Show(int mode, string title, string directory, string name,
            ExtensionFilter[] extensions, bool multiple)
        {
            string filters = extensions == null ? "" : string.Join("\n", extensions.Select(filter =>
                (filter.Name ?? "Files").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ') + "\t" +
                string.Join(";", filter.Extensions.Select(ext => ext == "*" ? "*.*" : "*." + ext.TrimStart('.')))));
            string extension = extensions != null && extensions.Length > 0 && extensions[0].Extensions.Length > 0
                ? extensions[0].Extensions[0].TrimStart('.') : "";
            if (extension == "*") extension = "";
            string folder = !string.IsNullOrEmpty(directory) && Directory.Exists(directory)
                ? Path.GetFullPath(directory) : "";
            IntPtr result = IntPtr.Zero;
            try
            {
                int hr = AdeShowFileDialog(mode, GetActiveWindow(), title ?? "", folder,
                    name ?? "", extension, filters, multiple ? 1 : 0, out result);
                if (hr == Cancelled) return Array.Empty<string>();
                Marshal.ThrowExceptionForHR(hr);
                string paths = Marshal.PtrToStringUni(result);
                return string.IsNullOrEmpty(paths) ? Array.Empty<string>() : paths.Split('\n');
            }
            finally
            {
                if (result != IntPtr.Zero) Marshal.FreeCoTaskMem(result);
            }
        }
    }
}
#endif
