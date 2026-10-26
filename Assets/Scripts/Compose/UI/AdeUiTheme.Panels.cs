using Arcade.Compose.Editing;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

namespace Arcade.Compose
{
	public sealed partial class AdeUiTheme
	{
		private RectTransform valuePanel, timingPanel;
		private readonly System.Collections.Generic.List<Text> panelLabels = new System.Collections.Generic.List<Text>();

		private void StyleEditorPanels()
		{
			var value = AdeValueEditor.Instance;
			var timing = AdeTimingEditor.Instance;
			if (value && value.Panel)
			{
				RectTransform panel = (RectTransform)value.Timing.parent.parent;
				if (valuePanel != panel)
				{
					valuePanel = panel;
					AddPanelLabel(panel, "ThemePanelTitle", "物件编辑", value.Timing.GetComponentInChildren<Text>(true), 36, 1);
				}
				StylePanel(panel);
				var rows = value.Timing.parent.GetComponent<VerticalLayoutGroup>();
				rows.childForceExpandWidth = true;
				foreach (Transform row in rows.transform)
				{
					var layout = row.GetComponent<HorizontalLayoutGroup>();
					if (!layout) continue;
					layout.childAlignment = TextAnchor.MiddleLeft;
					layout.spacing = 12;
					layout.padding = new RectOffset();
					var label = row.Find("Text")?.GetComponent<Text>();
					if (!label) continue;
					label.alignment = TMPro.TextAlignmentOptions.Left;
					var width = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
					width.minWidth = width.preferredWidth = 96;
					width.flexibleWidth = 0;
					var rowSize = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
					rowSize.preferredWidth = 312;
				}
			}
			if (timing && timing.View)
			{
				var panel = (RectTransform)timing.View.transform.Find("Panel");
				if (!panel) return;
				if (timingPanel != panel)
				{
					timingPanel = panel;
					AddPanelLabel(panel, "ThemeTimingColumns", "时间 (ms) · BPM · 拍数", panel.Find("Header/Text").GetComponent<Text>(), 24, 3);
				}
				StylePanel(panel);
				var header = panel.Find("Header").GetComponent<HorizontalLayoutGroup>();
				header.childAlignment = TextAnchor.MiddleLeft;
				var scroll = panel.Find("Scroll View").GetComponent<ScrollRect>();
				var scrollSize = scroll.GetComponent<LayoutElement>();
				scrollSize.preferredWidth = 384;
				scrollSize.preferredHeight = 300;
				var background = scroll.GetComponent<Image>();
				background.sprite = Shape("ThemeTimingList", ControlRadius, Current.Chrome, Current.Border);
				background.color = Color.white;
				background.type = UnityEngine.UI.Image.Type.Sliced;
				if (scroll.verticalScrollbar)
				{
					((RectTransform)scroll.verticalScrollbar.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 6);
					scroll.viewport.offsetMax = new Vector2(-12, scroll.viewport.offsetMax.y);
					var area = (RectTransform)scroll.verticalScrollbar.handleRect.parent;
					area.offsetMin = area.offsetMax = Vector2.zero;
					scroll.verticalScrollbar.handleRect.sizeDelta = Vector2.zero;
				}
			}
		}

		private static void AddPanelLabel(RectTransform panel, string name, string caption, Text template, float height, int index)
		{
			if (panel.Find(name)) return;
			var label = Instantiate(template, panel, false);
			label.name = name;
			label.text = caption;
			label.raycastTarget = false;
			label.transform.SetSiblingIndex(index);
			var layout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
			layout.preferredHeight = height;
			layout.preferredWidth = 312;
			layout.flexibleWidth = 1;
		}

		private void StylePanel(RectTransform panel)
		{
			var layout = panel.GetComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(16, 16, 14, 16);
			layout.spacing = 12;
			var background = panel.Find("Background").GetComponent<Image>();
			background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
			background.sprite = Shape("ThemePanel", PanelRadius, Current.Panel, Current.Border);
			background.color = Color.white;
			background.type = UnityEngine.UI.Image.Type.Sliced;
			panel.GetComponentsInChildren(true, panelLabels);
			foreach (Text label in panelLabels)
			{
				bool title = label.name == "ThemePanelTitle" || label.transform.parent.name == "Header";
				bool help = label.name == "ThemeTimingColumns" || label.name == "Slide range help";
				label.fontSize = title ? 26 : help ? 16 : 20;
				label.color = help ? Current.Muted : Current.Text;
				if (title || help) label.alignment = TMPro.TextAlignmentOptions.Left;
				if (label.text == "X" && label.transform.parent.name == "Remove") label.text = "×";
				var input = label.GetComponentInParent<InputField>();
				if (input && input.textComponent == label)
				{
					label.rectTransform.offsetMin = new Vector2(10, 0);
					label.rectTransform.offsetMax = new Vector2(-10, 0);
				}
			}
		}
	}
}
