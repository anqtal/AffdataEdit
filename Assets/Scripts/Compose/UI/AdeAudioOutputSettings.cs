using Arcade.Audio;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
	internal static class AdeAudioOutputSettings
	{
		internal static void AddTo(ArcadeComposeManager compose)
		{
			Transform template = compose.TargetFramerateDropdown.GetComponentInParent<HorizontalLayoutGroup>(true).transform;
			Transform sync = compose.PlaybackSyncToggle.GetComponentInParent<HorizontalLayoutGroup>(true).transform;
			Transform parent = sync.parent;
			int index = sync.GetSiblingIndex();
			Dropdown output = AddDropdown(template, parent, "WindowsAudioOutput", "音频输出", index++,
				new List<string> { "WASAPI", "DirectSound" }, BassOutputSettings.DirectSound ? 1 : 0);
			output.onValueChanged.AddListener(value => BassOutputSettings.DirectSound = value == 1);

			int[] buffers = { 20, 30, 50 };
			Dropdown buffer = AddDropdown(template, parent, "WindowsAudioBuffer", "输出缓冲", index++,
				new List<string> { "20 ms · 低延迟", "30 ms · 默认", "50 ms · 稳定" }, System.Array.IndexOf(buffers, BassOutputSettings.BufferMilliseconds));
			buffer.onValueChanged.AddListener(value => BassOutputSettings.BufferMilliseconds = buffers[value]);

			Text labelTemplate = template.GetComponentInChildren<Text>(true);
			Text hint = AdeUiKit.CreateLabelRow(parent, labelTemplate.transform,
				"音频设置重启后生效。低缓冲若出现断音，请调高；实际延迟取决于音频设备。");
			hint.fontSize = 18;
			hint.transform.parent.name = "WindowsAudioHint";
			hint.transform.parent.SetSiblingIndex(index);
			var hintSize = hint.GetComponent<LayoutElement>();
			hintSize.minWidth = 0;
			hintSize.preferredWidth = 392;
			hintSize.preferredHeight = -1;
		}

		private static Dropdown AddDropdown(Transform template, Transform content, string name, string caption, int index, List<string> options, int selected)
		{
			GameObject row = AdeUiKit.CloneRow(template, content, name);
			row.transform.SetSiblingIndex(index);
			row.GetComponentInChildren<Text>(true).text = caption;
			Dropdown dropdown = row.GetComponentInChildren<Dropdown>(true);
			// CloneRow does not clear dropdown callbacks; do not inherit frame-rate changes.
			dropdown.onValueChanged = new Dropdown.DropdownEvent();
			dropdown.ClearOptions();
			dropdown.AddOptions(options);
			dropdown.captionText.fontSize = 20;
			dropdown.itemText.fontSize = 20;
			dropdown.SetValueWithoutNotify(selected);
			return dropdown;
		}
	}
}
