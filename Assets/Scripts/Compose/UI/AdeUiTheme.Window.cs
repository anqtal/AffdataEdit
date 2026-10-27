#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Process = System.Diagnostics.Process;
#endif

namespace Arcade.Compose
{
    public sealed partial class AdeUiTheme
    {
        partial void ApplyWindowTheme();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int ImmersiveDarkMode = 20, CaptionColor = 35, CaptionTextColor = 36;
        private const int InvalidArgument = unchecked((int)0x80070057);
        private IntPtr themedWindow;
        private int windowThemeRevision = -1;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        partial void ApplyWindowTheme()
        {
            IntPtr window = themedWindow;
            if (window == IntPtr.Zero || !IsWindow(window))
            {
                using (Process process = Process.GetCurrentProcess())
                    window = process.MainWindowHandle;
            }
            if (window == IntPtr.Zero || (window == themedWindow && windowThemeRevision == Revision)) return;
            themedWindow = window;
            windowThemeRevision = Revision;

            SetWindowThemeAttribute(window, ImmersiveDarkMode, Dark ? 1 : 0);
            // Windows 11 supports palette colors; Windows 10 retains the dark/light frame above.
            SetWindowThemeAttribute(window, CaptionColor, WindowColor(Current.Chrome));
            SetWindowThemeAttribute(window, CaptionTextColor, WindowColor(Current.Text));
        }

        private static int WindowColor(Color32 color) => color.r | (color.g << 8) | (color.b << 16);

        private static void SetWindowThemeAttribute(IntPtr window, int attribute, int value)
        {
            int result = DwmSetWindowAttribute(window, attribute, ref value, sizeof(int));
            // Caption colors are optional on Windows 10; failures of the dark/light mode are reported.
            if (result < 0 && (attribute == ImmersiveDarkMode || result != InvalidArgument))
                Debug.LogError($"Unable to apply window theme attribute {attribute}: HRESULT 0x{result:X8}.");
        }
#endif
    }
}
