using Arcade.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Gameplay
{
    public class ArcAudioManager : MonoBehaviour
    {
        public static ArcAudioManager Instance { get; private set; }
        public Text PlayBackSpeedButtonText;
        public BassClip Clip { get; private set; }
        private BassVoice voice;
        private double scheduledStart = -1;
        private float playBackSpeed = 1;
        private void Awake() { Instance = this; }
        public float Timing
        {
            get => voice == null ? 0 : (float)voice.Position;
            set { if (voice != null) voice.Position = value; }
        }
        public float PlayBackSpeed
        {
            get => playBackSpeed;
            private set
            {
                if (voice != null) BassNative.Check(BassNative.BASS_ChannelSetAttribute(voice.Handle, 0x10000, (value - 1) * 100), "tempo");
                playBackSpeed = value;
            }
        }
        public void Load(BassClip clip)
        {
            ReleaseVoice();
            Clip = clip;
            voice = new BassVoice(clip, true);
            PlayBackSpeed = playBackSpeed;
            ApplyVolume();
        }
        public void ApplyVolume()
        {
            if (voice != null) BassNative.Check(BassNative.BASS_ChannelSetAttribute(voice.Handle, 2, BassAudio.MusicVolume), "music volume");
        }
        public void Play()
        {
            scheduledStart = -1;
            if (voice != null) BassNative.Check(BassNative.BASS_ChannelPlay(voice.Handle, false), "play music");
        }
        public void PlayDelayed(float seconds) { scheduledStart = BassAudio.Clock + Mathf.Max(0, seconds); }
        public void Pause()
        {
            scheduledStart = -1;
            if (voice != null && BassNative.BASS_ChannelIsActive(voice.Handle) != 0)
                BassNative.Check(BassNative.BASS_ChannelPause(voice.Handle), "pause music");
        }
        public void Stop()
        {
            scheduledStart = -1;
            if (voice != null) { BassNative.BASS_ChannelStop(voice.Handle); voice.Position = 0; }
        }
        private void Update() { if (scheduledStart >= 0 && BassAudio.Clock >= scheduledStart) Play(); }
        public void ReleaseVoice() { scheduledStart = -1; voice?.Dispose(); voice = null; Clip = null; }
        private void OnDestroy() { ReleaseVoice(); if (Instance == this) Instance = null; }
		public void NextPlaybackSpeed()
		{
			if (PlayBackSpeedButtonText.text == "100%")
			{
				PlayBackSpeed = 0.75f;
				PlayBackSpeedButtonText.text = "75%";
			}
			else if (PlayBackSpeedButtonText.text == "75%")
			{
				PlayBackSpeed = 0.5f;
				PlayBackSpeedButtonText.text = "50%";
			}
			else if (PlayBackSpeedButtonText.text == "50%")
			{
				PlayBackSpeed = 0.25f;
				PlayBackSpeedButtonText.text = "25%";
			}
			else
			{
				PlayBackSpeed = 1f;
				PlayBackSpeedButtonText.text = "100%";
			}
		}
	}
}
