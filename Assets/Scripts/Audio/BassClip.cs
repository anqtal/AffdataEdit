using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Arcade.Audio
{
    public sealed class BassClip : ScriptableObject
    {
        private string filePath;
        private byte[] externalData;
        private float? duration;
        internal byte[] Bytes => externalData != null && externalData.Length > 0
            ? externalData : (externalData = File.ReadAllBytes(filePath));
        public float length
        {
            get
            {
                if (!duration.HasValue)
                    using (var voice = new BassVoice(this, false)) duration = (float)voice.Length;
                return duration.Value;
            }
        }
        public static BassClip Load(string path)
        {
            if (!File.Exists(path)) return null;
            var clip = CreateInstance<BassClip>();
            clip.name = path;
            clip.filePath = path;
            clip.externalData = File.ReadAllBytes(path);
            try
            {
                if (IsOpus(clip.externalData)) clip.externalData = DecodeOpus(clip.externalData);
                _ = clip.length;
                return clip;
            }
            catch { Destroy(clip); throw; }
        }
        public static BassClip CreateSilence(string name, int frames, int channels, int frequency)
        {
            var clip = CreateInstance<BassClip>();
            clip.name = name;
            clip.externalData = Wav(new byte[checked(frames * channels * 2)], channels, frequency);
            return clip;
        }
        private static byte[] Wav(byte[] pcm, int channels, int frequency)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(pcm.Length + 36);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(frequency);
                writer.Write(frequency * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
                return stream.ToArray();
            }
        }
        // Ogg pages whose first packet is an OpusHead identification header.
        private static bool IsOpus(byte[] data)
        {
            return data.Length >= 36 && data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S'
                && System.Text.Encoding.ASCII.GetString(data, 28, 8) == "OpusHead";
        }
        // BASS core cannot decode Opus, so bassopus decodes it once into 16-bit PCM WAV;
        // streams and samples then use the WAV bytes unchanged.
        private static byte[] DecodeOpus(byte[] data)
        {
            BassAudio.EnsureInitialized();
            var memory = GCHandle.Alloc(data, GCHandleType.Pinned);
            int handle = 0;
            try
            {
                handle = BassNative.BASS_OPUS_StreamCreateFile(true, memory.AddrOfPinnedObject(), 0, data.LongLength, BassNative.Decode);
                BassNative.Check(handle != 0, "opus decode");
                BassNative.Check(BassNative.BASS_ChannelGetInfo(handle, out BassNative.ChannelInfo info), "opus info");
                using (var pcm = new MemoryStream())
                {
                    var buffer = new byte[65536];
                    int read;
                    while ((read = BassNative.BASS_ChannelGetData(handle, buffer, buffer.Length)) > 0) pcm.Write(buffer, 0, read);
                    return Wav(pcm.ToArray(), info.Channels, info.Frequency);
                }
            }
            finally
            {
                if (handle != 0) BassNative.BASS_StreamFree(handle);
                memory.Free();
            }
        }
        private void OnDestroy() { BassAudio.ReleaseSample(this); }
    }
}
