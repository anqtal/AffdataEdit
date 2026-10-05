using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade.Compose
{
	// A square side drawer beside the left bar, between the top and bottom bars, holding the
	// settings rows. The settings button toggles it. Labels sit on the left and controls on
	// the right of each row, like VS Code's settings.
	public sealed class AdeSettingsDrawer : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";
		private const string SettingButtonPath = "ArcaeaEditor/EditorCanvas/Bars/Left/View/Bottom/Setting";
		// Narrow enough that labels and controls do not leave a wide gap between them.
		private const float Width = 440, Padding = 24, RowSpacing = 12, ControlWidth = 180;
		private const float OpenSeconds = 0.28f, CloseSeconds = 0.2f, ContentShift = 24;
		// Bar sprites end in a 20px drop shadow drawn over the drawer's edges.
		private const float BarShadow = 20;
		// The built-in check sprite is dark gray, so the dark theme swaps in a white check;
		// the light theme keeps the scene's sprite and color.
		private Image[] checks = new Image[0];
		private Sprite[] lightSprites = new Sprite[0];
		private Color[] lightColors = new Color[0];
		private Sprite darkCheck;
		private static readonly Color DarkFill = new Color32(0x1F, 0x1F, 0x1F, 0xFF), LightFill = new Color32(0xF3, 0xF3, 0xF3, 0xFF);

		private RectTransform drawer, viewport;
		private CanvasGroup contentGroup;
		private Image fill;
		private float shownX;
		private MotionHandle motion;
		private bool open;
		private float progress;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(AdeSettingsDrawer)).AddComponent<AdeSettingsDrawer>();
			};
		}

		private void Start()
		{
			var compose = ArcadeComposeManager.Instance;
			var dialog = compose && compose.PlaybackSyncToggle ? compose.PlaybackSyncToggle.GetComponentInParent<AdeDialog>(true) : null;
			var settingButton = GameObject.Find(SettingButtonPath)?.GetComponent<Button>();
			if (!dialog || !settingButton)
			{
				Debug.LogWarning("Settings drawer: settings dialog or button not found");
				return;
			}
			shownX = compose.LeftBar.sizeDelta.x - BarShadow;
			float top = compose.TopBar.sizeDelta.y - BarShadow, bottom = compose.BottomBar.sizeDelta.y - BarShadow;

			// The bars are override-sorted canvases above the background. As the first child of the
			// left bar's canvas, the drawer draws over the background but under the bar and its
			// shadow, so it slides out from beneath the bar.
			drawer = new GameObject("SettingsDrawer", typeof(RectTransform), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
			drawer.SetParent(compose.LeftBar, false);
			drawer.SetAsFirstSibling();
			drawer.anchorMin = new Vector2(0, 0);
			drawer.anchorMax = new Vector2(0, 1);
			drawer.pivot = new Vector2(0, 1);
			drawer.sizeDelta = new Vector2(Width, -(top + bottom));
			drawer.anchoredPosition = new Vector2(shownX - Width, -top);
			fill = drawer.GetComponent<Image>();

			viewport = Child("Viewport", drawer, typeof(RectMask2D), typeof(CanvasGroup));
			contentGroup = viewport.GetComponent<CanvasGroup>();
			viewport.anchorMin = Vector2.zero;
			viewport.anchorMax = Vector2.one;
			viewport.sizeDelta = Vector2.zero;
			var content = Child("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			content.anchorMin = new Vector2(0, 1);
			content.anchorMax = new Vector2(1, 1);
			content.pivot = new Vector2(0.5f, 1);
			content.sizeDelta = Vector2.zero;
			var layout = content.GetComponent<VerticalLayoutGroup>();
			// Rows start below the play button, level with the left bar's first button.
			var leftLayout = compose.LeftBarView.GetComponent<VerticalLayoutGroup>();
			int contentTop = leftLayout ? Mathf.Max((int)Padding, leftLayout.padding.top - (int)top) : (int)Padding;
			layout.padding = new RectOffset((int)Padding, (int)Padding, contentTop, (int)Padding);
			layout.spacing = RowSpacing;
			Stretch(layout);
			content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			var scroll = drawer.GetComponent<ScrollRect>();
			scroll.viewport = viewport;
			scroll.content = content;
			scroll.horizontal = false;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 30;

			// Move the settings out of the dialog, which is no longer opened.
			Transform source = AdeUiKit.Content(dialog);
			while (source.childCount > 0) source.GetChild(0).SetParent(content, false);
			Arrange(content);
			darkCheck = Resources.Load<Sprite>("AffdataEdit/Icons/Checkmark");
			var toggles = content.GetComponentsInChildren<Toggle>(true);
			checks = new Image[toggles.Length];
			lightSprites = new Sprite[toggles.Length];
			lightColors = new Color[toggles.Length];
			for (int i = 0; i < toggles.Length; i++)
			{
				checks[i] = toggles[i].graphic as Image;
				if (!checks[i]) continue;
				lightSprites[i] = checks[i].sprite;
				lightColors[i] = checks[i].color;
			}

			AdeUiTheme.AddRoot(drawer);
			AdeUiKit.SetOnClick(settingButton, Toggle);
			Pose(0);
			drawer.gameObject.SetActive(false);
		}

		private void Update()
		{
			if (fill) fill.color = AdeUiTheme.Dark ? DarkFill : LightFill;
			bool dark = AdeUiTheme.Dark && darkCheck;
			for (int i = 0; i < checks.Length; i++)
			{
				if (!checks[i]) continue;
				Sprite sprite = dark ? darkCheck : lightSprites[i];
				if (checks[i].sprite != sprite) checks[i].sprite = sprite;
				checks[i].color = dark ? Color.white : lightColors[i];
			}
		}

		private static RectTransform Child(string name, Transform parent, params System.Type[] components)
		{
			var go = new GameObject(name, typeof(RectTransform));
			foreach (var type in components) go.AddComponent(type);
			go.transform.SetParent(parent, false);
			return (RectTransform)go.transform;
		}

		private static void Stretch(HorizontalOrVerticalLayoutGroup group)
		{
			group.childAlignment = TextAnchor.UpperLeft;
			group.childControlWidth = group.childControlHeight = true;
			group.childForceExpandWidth = true;
			group.childForceExpandHeight = false;
		}

		// Containers stretch to the drawer width. A row with a label and a control puts the
		// label on the left and pushes the control to the right; text alone sits left and a
		// lone control sits right.
		private static void Arrange(Transform root)
		{
			foreach (var group in root.GetComponentsInChildren<VerticalLayoutGroup>(true))
				Stretch(group);
			// Rows and containers fit their content width in the dialog; here the parent sets it.
			foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true))
				fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			foreach (var row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
			{
				Text label = null;
				bool control = false;
				foreach (Transform child in row.transform)
				{
					if (!label && child.GetComponent<Text>()) label = child.GetComponent<Text>();
					else if (child.GetComponent<Selectable>()) control = true;
				}
				// Widths must come from the layout for the label to take the free space. The dialog
				// padded checkbox rows on the right to line them up with its 300px dropdowns.
				row.childControlWidth = true;
				row.padding = new RectOffset(0, 0, row.padding.top, row.padding.bottom);
				row.childForceExpandWidth = false;
				foreach (Transform child in row.transform)
				{
					var size = child.GetComponent<LayoutElement>();
					if (!size || !child.GetComponent<Selectable>()) continue;
					if (size.preferredWidth > ControlWidth) size.preferredWidth = ControlWidth;
					// Controls keep their width instead of shrinking when a row is tight.
					size.minWidth = size.preferredWidth;
				}
				if (label && control)
				{
					row.childAlignment = TextAnchor.MiddleLeft;
					label.transform.SetAsFirstSibling();
					label.alignment = TextAnchor.MiddleLeft;
					label.horizontalOverflow = HorizontalWrapMode.Overflow;
					var element = label.GetComponent<LayoutElement>();
					if (!element) element = label.gameObject.AddComponent<LayoutElement>();
					element.flexibleWidth = 1;
				}
				else row.childAlignment = control ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
			}
			foreach (var text in root.GetComponentsInChildren<Text>(true))
				if (!text.GetComponentInParent<Selectable>(true)) text.alignment = Left(text.alignment);
			// The footer is centered, with "forked from Arcade" under the name.
			foreach (Transform child in root)
			{
				var footer = child.GetComponent<Text>();
				if (!footer) continue;
				footer.alignment = TextAnchor.MiddleCenter;
				footer.text = footer.text.Replace(" <size=", "\n<size=");
				var size = child.GetComponent<LayoutElement>();
				if (size) size.preferredHeight = -1;
			}
		}

		private static TextAnchor Left(TextAnchor anchor) =>
			anchor == TextAnchor.UpperCenter || anchor == TextAnchor.UpperRight ? TextAnchor.UpperLeft
			: anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight ? TextAnchor.LowerLeft
			: anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.MiddleRight ? TextAnchor.MiddleLeft : anchor;

		// Slides the panel out from under the left bar; the rows follow slightly later,
		// moving in from the left while fading in. Closing reverses from the current pose.
		private void Toggle()
		{
			if (!drawer) return;
			open = !open;
			motion.TryCancel();
			if (open) drawer.gameObject.SetActive(true);
			float target = open ? 1 : 0;
			float duration = (open ? OpenSeconds : CloseSeconds) * Mathf.Abs(target - progress);
			motion = LMotion.Create(progress, target, Mathf.Max(0.01f, duration))
				.WithEase(open ? Ease.OutQuart : Ease.InQuart)
				.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
				.WithOnComplete(() =>
				{
					if (!open) drawer.gameObject.SetActive(false);
				})
				.Bind(Pose);
		}

		private void Pose(float t)
		{
			progress = t;
			drawer.anchoredPosition = new Vector2(Mathf.LerpUnclamped(shownX - Width, shownX, t), drawer.anchoredPosition.y);
			float rows = Mathf.Clamp01((t - 0.3f) / 0.7f);
			contentGroup.alpha = rows;
			viewport.anchoredPosition = new Vector2(-ContentShift * (1 - rows), 0);
		}
	}
}
