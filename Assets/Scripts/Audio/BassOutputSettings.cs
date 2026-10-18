using UnityEngine;

namespace Arcade.Audio
{
    internal static class BassOutputSettings
    {
        private const string OutputKey = "AffdataEdit.WindowsAudioOutput";
        private const string BufferKey = "AffdataEdit.WindowsAudioBufferMs";
        // BASS 2.4 constants from the official bass.h header.
        private const uint DeviceDirectSound = 0x40000, ConfigDeviceBuffer = 27;

        internal static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor
            || Application.platform == RuntimePlatform.WindowsPlayer;

        // BASS uses shared WASAPI when no DirectSound flag is supplied.
        internal static bool DirectSound
        {
            get => PlayerPrefs.GetInt(OutputKey, 0) == 1;
            set { PlayerPrefs.SetInt(OutputKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        internal static int BufferMilliseconds
        {
            get => NormalizeBuffer(PlayerPrefs.GetInt(BufferKey, 30));
            set { PlayerPrefs.SetInt(BufferKey, NormalizeBuffer(value)); PlayerPrefs.Save(); }
        }

        private static int NormalizeBuffer(int value) => value == 20 || value == 50 ? value : 30;

        internal static uint ConfigureDevice()
        {
            if (!IsWindows) return 0;
            // Device buffering must be configured before BASS_Init; changes in settings
            // are saved for the next launch so playing streams are left intact.
            BassNative.Check(BassNative.BASS_SetConfig(ConfigDeviceBuffer, (uint)BufferMilliseconds), "set Windows output buffer");
            return DirectSound ? DeviceDirectSound : 0;
        }
    }
}
