using Arcade.Audio;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Arcade.Compose.UI;

namespace Arcade.Compose.Dialog
{
	[Serializable]
	public class SoundPreferences
	{
		public float Chart = 0.7f;
		public float Effect = 0f;
		public float Guide = 1f;
	}
	public class AdeSoundDialogContent : AdeDialogContent<AdeSingleDialog>
	{
		public static AdeSoundDialogContent Instance { get; private set; }

		public AdeNumberInputWithSlider ChartAudioInput, EffectAudioInput;
		private AdeNumberInputWithSlider guideAudioInput;
		private SoundPreferences preferences;
		public string PreferencesSavePath
		{
			get
			{
				return ArcadeComposeManager.ArcadePersistentFolder + "/Sound.json";
			}
		}

		private void Awake()
		{
			Instance = this;
			// The guide sound row is cloned from the effect row.
			GameObject guideRow = Instantiate(EffectAudioInput.gameObject, EffectAudioInput.transform.parent);
			guideRow.name = "GuideAudio";
			guideRow.transform.SetSiblingIndex(EffectAudioInput.transform.GetSiblingIndex() + 1);
			Text label = guideRow.transform.Find("Text")?.GetComponent<Text>();
			if (label) label.text = "正解音量";
			guideAudioInput = guideRow.GetComponent<AdeNumberInputWithSlider>();
			foreach (var input in new[] { ChartAudioInput, EffectAudioInput, guideAudioInput }) ConfigureVolume(input);
			ChartAudioInput.onValueEdited += OnChartAudioChange;
			EffectAudioInput.onValueEdited += OnEffectAudioChange;
			guideAudioInput.onValueEdited += OnGuideAudioChange;
		}

		// AffdataPlay volume settings: 0.0-2.0 in steps of 0.1.
		private static void ConfigureVolume(AdeNumberInputWithSlider input)
		{
			input.SliderValueScale = 1;
			input.NumberFormat = "0.0";
			input.slider.Slider.wholeNumbers = false;
			input.slider.Slider.minValue = 0;
			input.slider.Slider.maxValue = BassAudio.MaxVolume;
		}

		private static float Volume(float value) => Mathf.Round(Mathf.Clamp(value, 0, BassAudio.MaxVolume) * 10) / 10;

		private void Start()
		{
			Load();
		}
		private void OnDestroy()
		{
			Save();
		}
		private void Load()
		{
			try
			{
				if (File.Exists(PreferencesSavePath))
				{
					PlayerPrefs.SetString("AdeSoundDialog", File.ReadAllText(PreferencesSavePath));
					File.Delete(PreferencesSavePath);
				}
				preferences = JsonConvert.DeserializeObject<SoundPreferences>(PlayerPrefs.GetString("AdeSoundDialog", ""));
				if (preferences == null) preferences = new SoundPreferences();
			}
			catch (Exception)
			{
				preferences = new SoundPreferences();
			}
			finally
			{
				ChartAudioInput.SetValueWithoutNotify(preferences.Chart);
				EffectAudioInput.SetValueWithoutNotify(preferences.Effect);
				guideAudioInput.SetValueWithoutNotify(preferences.Guide);
				BassAudio.MusicVolume = preferences.Chart;
				BassAudio.EffectVolume = preferences.Effect;
				BassAudio.GuideVolume = preferences.Guide;
			}
		}
		private void Save()
		{
			PlayerPrefs.SetString("AdeSoundDialog", JsonConvert.SerializeObject(preferences));
		}

		public void OnChartAudioChange(float val)
		{
			val = Volume(val);
			preferences.Chart = val;
			BassAudio.MusicVolume = val;
			Save();
			ChartAudioInput.SetValueWithoutNotify(preferences.Chart);
		}
		public void OnEffectAudioChange(float val)
		{
			val = Volume(val);
			preferences.Effect = val;
			BassAudio.EffectVolume = val;
			Save();
			EffectAudioInput.SetValueWithoutNotify(preferences.Effect);
		}
		public void OnGuideAudioChange(float val)
		{
			val = Volume(val);
			preferences.Guide = val;
			BassAudio.GuideVolume = val;
			Save();
			guideAudioInput.SetValueWithoutNotify(preferences.Guide);
		}
	}
}
