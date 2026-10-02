using System.IO;
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
            try { _ = clip.length; return clip; }
            catch { Destroy(clip); throw; }
        }
        public static BassClip CreateSilence(string name, int frames, int channels, int frequency)
        {
            var clip = CreateInstance<BassClip>();
            clip.name = name;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                int size = checked(frames * channels * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(size + 36);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(frequency);
                writer.Write(frequency * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(size); writer.Write(new byte[size]);
                clip.externalData = stream.ToArray();
            }
            return clip;
        }
        private void OnDestroy() { BassAudio.ReleaseSample(this); }
    }
}
