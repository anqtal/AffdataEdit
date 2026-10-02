using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Arcade.Audio
{
    public sealed class BassAudio : MonoBehaviour
    {
        private static BassAudio instance;
        private static readonly Dictionary<BassClip, int> samples = new Dictionary<BassClip, int>();
        private static readonly Dictionary<int, bool> voices = new Dictionary<int, bool>();
        private readonly List<int> finished = new List<int>();
        private static float musicVolume = .7f, effectVolume;
        public static double Clock => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        public static float MusicVolume { get => musicVolume; set { musicVolume = Mathf.Clamp01(value); Gameplay.ArcAudioManager.Instance?.ApplyVolume(); } }
        public static float EffectVolume { get => effectVolume; set => effectVolume = Mathf.Clamp01(value); }

        internal static void EnsureInitialized()
        {
            if (instance) return;
            // Native state can survive an Editor domain reload. Error 14 means the
            // device is already initialized and can be reused by the new owner.
            if (!BassNative.BASS_Init(-1, 48000, 0, System.IntPtr.Zero, System.IntPtr.Zero)
                && BassNative.BASS_ErrorGetCode() != 14)
                BassNative.Check(false, "initialize");
            BassNative.BASS_SetConfig(0, 100); // output buffer in milliseconds
            BassNative.BASS_SetConfig(1, 5);   // update period
            var go = new GameObject("BASS Audio");
            instance = go.AddComponent<BassAudio>();
            DontDestroyOnLoad(go);
        }

        public static void PlayOneShot(BassClip clip, bool musicBus = false)
        {
            if (!clip) return;
            EnsureInitialized();
            if (!samples.TryGetValue(clip, out int sample))
            {
                var bytes = clip.Bytes;
                var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try { sample = BassNative.BASS_SampleLoad(true, pin.AddrOfPinnedObject(), 0, (uint)bytes.Length, 32, 0x20000); }
                finally { pin.Free(); }
                BassNative.Check(sample != 0, "load hit sample");
                samples.Add(clip, sample);
            }
            int channel = BassNative.BASS_SampleGetChannel(sample, false);
            BassNative.Check(channel != 0, "sample voice");
            BassNative.Check(BassNative.BASS_ChannelSetAttribute(channel, 2, musicBus ? musicVolume : effectVolume), "sample volume");
            BassNative.Check(BassNative.BASS_ChannelPlay(channel, true), "play sample");
            voices[channel] = musicBus;
        }

        internal static void ReleaseSample(BassClip clip)
        {
            if (!samples.TryGetValue(clip, out int sample)) return;
            BassNative.BASS_SampleFree(sample);
            samples.Remove(clip);
        }
        private void Update()
        {
            finished.Clear();
            foreach (var voice in voices)
            {
                if (BassNative.BASS_ChannelIsActive(voice.Key) == 0) finished.Add(voice.Key);
                else BassNative.BASS_ChannelSetAttribute(voice.Key, 2, voice.Value ? musicVolume : effectVolume);
            }
            foreach (int channel in finished) voices.Remove(channel);
        }
        private void OnDisable() { Shutdown(); }
        private void OnDestroy() { Shutdown(); }
        private void Shutdown()
        {
            if (instance != this) return;
            Gameplay.ArcAudioManager.Instance?.ReleaseVoice();
            voices.Clear(); samples.Clear();
            BassNative.BASS_Free();
            instance = null;
        }
    }
}
