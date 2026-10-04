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
        // Voice channel -> bus, so bus volume changes reach playing voices.
        private static readonly Dictionary<int, Bus> voices = new Dictionary<int, Bus>();
        private readonly List<int> finished = new List<int>();
        private static float musicVolume = .7f, effectVolume, guideVolume = 1;
        public enum Bus { Effect, Music, Guide }
        public static double Clock => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        // Volumes range 0.0-2.0 like AffdataPlay; BASS amplifies above 1.
        public const float MaxVolume = 2;
        public static float MusicVolume { get => musicVolume; set { musicVolume = Mathf.Clamp(value, 0, MaxVolume); Gameplay.ArcAudioManager.Instance?.ApplyVolume(); } }
        public static float EffectVolume { get => effectVolume; set => effectVolume = Mathf.Clamp(value, 0, MaxVolume); }
        public static float GuideVolume { get => guideVolume; set => guideVolume = Mathf.Clamp(value, 0, MaxVolume); }
        private static float Volume(Bus bus) => bus == Bus.Music ? musicVolume : bus == Bus.Guide ? guideVolume : effectVolume;

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

        public static void PlayOneShot(BassClip clip, bool musicBus = false) => PlayOneShot(clip, musicBus ? Bus.Music : Bus.Effect);

        public static void PlayOneShot(BassClip clip, Bus bus)
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
            BassNative.Check(BassNative.BASS_ChannelSetAttribute(channel, 2, Volume(bus)), "sample volume");
            BassNative.Check(BassNative.BASS_ChannelPlay(channel, true), "play sample");
            voices[channel] = bus;
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
                else BassNative.BASS_ChannelSetAttribute(voice.Key, 2, Volume(voice.Value));
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
