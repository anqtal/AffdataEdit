using System;
using System.Runtime.InteropServices;

namespace Arcade.Audio
{
    internal sealed class BassVoice : IDisposable
    {
        private GCHandle memory;
        internal int Handle { get; private set; }
        internal double Length => BassNative.BASS_ChannelBytes2Seconds(Handle, BassNative.BASS_ChannelGetLength(Handle, 0));
        internal double Position
        {
            get => BassNative.BASS_ChannelBytes2Seconds(Handle, BassNative.BASS_ChannelGetPosition(Handle, 0));
            set => BassNative.Check(BassNative.BASS_ChannelSetPosition(Handle,
                BassNative.BASS_ChannelSeconds2Bytes(Handle, Math.Max(0, Math.Min(value, Length))), 0), "seek");
        }
        internal BassVoice(BassClip clip, bool tempo)
        {
            BassAudio.EnsureInitialized();
            var bytes = clip.Bytes;
            memory = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            int source = BassNative.BASS_StreamCreateFile(true, memory.AddrOfPinnedObject(), 0, bytes.LongLength,
                BassNative.Decode | BassNative.Float);
            if (source == 0) { memory.Free(); BassNative.Check(false, "decode"); }
            Handle = tempo ? BassNative.BASS_FX_TempoCreate(source, BassNative.FreeSource | BassNative.Float) : source;
            if (Handle == 0) { BassNative.BASS_StreamFree(source); memory.Free(); BassNative.Check(false, "tempo stream"); }
        }
        public void Dispose()
        {
            if (Handle != 0) { BassNative.BASS_StreamFree(Handle); Handle = 0; }
            if (memory.IsAllocated) memory.Free();
        }
    }
}
