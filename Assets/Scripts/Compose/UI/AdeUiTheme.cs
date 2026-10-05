using System.Collections.Generic;
using Arcade.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade.Compose
{
	// Light/dark editor chrome. Every themed graphic is classified by its sprite and by a
	// colour that belongs to exactly one palette entry, so applying either theme is
	// stateless and also covers UI cloned or instantiated later.
	public sealed class AdeUiTheme : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";
		private const string PreferenceKey = "AffdataEdit.DarkUi";
		private const float SweepInterval = 0.5f;

		public static AdeUiTheme Instance { get; private set; }
		public static bool Dark { get; private set; } = true;
		public static Color IconColor => Dark ? new Color32(0xC5, 0xC5, 0xC5, 0xFF) : new Color32(0x7F, 0x7F, 0x7F, 0xFF);

		private struct Entry
		{
			public string Sprite;
			public bool Text;
			public Color32 Light, Dark, Highlighted, Pressed;
			// The tile is transparent until hovered or pressed (toolbar icon buttons).
			public bool HiddenAtRest;
			public bool HasStates => Highlighted.a != 0;
		}

		private static Color32 C(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
		private static Entry Image(string sprite, uint light, uint dark, uint highlighted = 0, uint pressed = 0) => new Entry
		{
			Sprite = sprite, Light = C(light), Dark = C(dark),
			Highlighted = highlighted == 0 ? default : C(highlighted), Pressed = pressed == 0 ? default : C(pressed),
		};

		// Specific sprites first; a null sprite matches any image.
		private static Entry HiddenAtRest(Entry entry)
		{
			entry.HiddenAtRest = true;
			return entry;
		}

		// VS Code Dark Modern colours. Specific sprites first; a null sprite matches any image.
		private static readonly Entry[] Palette =
		{
			Image("Top", 0xFFFFFF, 0x181818), Image("Left", 0xFFFFFF, 0x181818),
			Image("Right", 0xFFFFFF, 0x181818), Image("Bottom", 0xFFFFFF, 0x181818),
			HiddenAtRest(Image("ToolBackground", 0xE6E6E6, 0x181818, 0x2A2D2E, 0x37373D)),
			Image("ToolBackground", 0xFFFFFF, 0x37373D, 0x45454B, 0x505056),
			Image("UISprite", 0xFFFFFF, 0x313131, 0x3C3C3C, 0x454545),
			Image("InputFieldBackground", 0xFFFFFF, 0x313131, 0x353535, 0x3C3C3C),
			Image("Background", 0xFFFFFF, 0x3C3C3C, 0x454545, 0x4F4F4F),
			Image("Knob", 0xFFFFFF, 0xCCCCCC, 0xE0E0E0, 0xB0B0B0),
			Image("UIMask", 0xFFFFFF, 0x252526),
			Image("Refresh", 0x000000, 0xC5C5C6),
			Image(null, 0x7F7F7F, 0xC5C5C5),
			Image(null, 0x323232, 0xCCCCCC),
			Image(null, 0xF5F5F5, 0x2B2B2B),
			new Entry { Text = true, Light = C(0x323232), Dark = C(0xCCCCCC) },
		};

		private readonly List<Graphic> graphics = new List<Graphic>();
		private readonly List<Selectable> selectables = new List<Selectable>();
		private readonly Dictionary<Graphic, Selectable> targets = new Dictionary<Graphic, Selectable>();
		private readonly List<Transform> roots = new List<Transform>();
		// UI moved or built outside the editor canvas and dialog layers (the settings drawer).
		private static readonly List<Transform> extraRoots = new List<Transform>();

		public static void AddRoot(Transform root)
		{
			if (!extraRoots.Contains(root)) extraRoots.Add(root);
		}
		private float nextSweep;
		private Sprite[] darkSprites;
		private Sprite closeIcon;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			Dark = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(AdeUiTheme)).AddComponent<AdeUiTheme>();
			};
		}

		private void Awake()
		{
			Instance = this;
		}

		private void Start()
		{
			CollectRoots();
			AddToggle();
			ApplySkinSprites();
			Sweep();
		}

		private void Update()
		{
			if (Time.unscaledTime < nextSweep) return;
			Sweep();
		}

		public static void SetDark(bool dark)
		{
			if (Dark == dark) return;
			Dark = dark;
			PlayerPrefs.SetInt(PreferenceKey, dark ? 1 : 0);
			if (!Instance) return;
			// Restoring the skin's own sprites also calls OnSkinApplied.
			if (!dark && AdeSkinHost.Instance) ArcSkinManager.Instance.SetSimpleSkin(AdeSkinHost.Instance.skinData);
			else Instance.ApplySkinSprites();
			Instance.Sweep();
		}

		// Aligns a dialog's controls as soon as it opens instead of on the next sweep.
		public static void OnDialogOpened()
		{
			if (!Instance || !Dark) return;
			Canvas.ForceUpdateCanvases();
			Instance.AlignDialogControls();
		}

		// Re-themes now instead of on the next sweep, after code changes a themed color.
		public static void Refresh()
		{
			if (Instance) Instance.Sweep();
		}

		// Called after the skin's UI sprites are (re)applied.
		public static void OnSkinApplied()
		{
			if (Instance) Instance.ApplySkinSprites();
		}

		private void CollectRoots()
		{
			roots.Clear();
			var editor = GameObject.Find("EditorCanvas");
			if (editor)
			{
				foreach (Transform child in editor.transform)
					if (child.name != "InGame") roots.Add(child);
			}
			extraRoots.RemoveAll(root => !root);
			roots.AddRange(extraRoots);
			if (AdeDialogManager.Instance)
			{
				roots.Add(AdeDialogManager.Instance.Opening);
				roots.Add(AdeDialogManager.Instance.Closed);
			}
		}

		private void Sweep()
		{
			nextSweep = Time.unscaledTime + SweepInterval;
			foreach (Transform root in roots)
			{
				if (!root) continue;
				targets.Clear();
				root.GetComponentsInChildren(true, selectables);
				foreach (Selectable selectable in selectables)
				{
					if (selectable.transition == Selectable.Transition.ColorTint && selectable.targetGraphic)
						targets[selectable.targetGraphic] = selectable;
				}
				root.GetComponentsInChildren(true, graphics);
				foreach (Graphic graphic in graphics) Apply(graphic);
			}
			if (Dark) AlignDialogControls();
		}

		// Places the buttons and the close button EdgeGap inside the visible edge of the
		// dialog sprites, measured from the laid-out rects, so their corners are concentric.
		private void AlignDialogControls()
		{
			var skin = ArcSkinManager.Instance;
			if (!skin) return;
			foreach (AdeSingleDialog dialog in skin.SingleDialogs)
			{
				if (!dialog || !dialog.View.activeInHierarchy) continue;
				AlignButton(dialog.CompleteButton, dialog.DialogBackground, 0);
				AlignClose(dialog, dialog.DialogTop);
			}
			foreach (AdeDualDialog dialog in skin.DualDialogs)
			{
				if (!dialog || !dialog.View.activeInHierarchy) continue;
				AlignButton(dialog.RightButton, dialog.DialogBackground, 0);
				AlignButton(dialog.LeftButton, dialog.DialogBackground, 1);
				AlignClose(dialog, dialog.DialogTop);
			}
		}

		private static readonly Vector3[] corners = new Vector3[4];

		private static void AlignButton(Button button, UnityEngine.UI.Image background, int slot)
		{
			if (!button || !background) return;
			var rect = (RectTransform)button.transform;
			var parent = (RectTransform)rect.parent;
			background.rectTransform.GetWorldCorners(corners);
			Vector2 edge = parent.InverseTransformPoint(corners[3]);
			Vector2 anchor = new Vector2(parent.rect.xMax, parent.rect.yMin);
			rect.anchoredPosition = edge - anchor + new Vector2(-BackgroundMargin.x - EdgeGap - slot * (ButtonWidth + ButtonGap), BackgroundMargin.y + EdgeGap);
		}

		private static void AlignClose(AdeDialog dialog, UnityEngine.UI.Image header)
		{
			Transform close = dialog.View.transform.Find(CloseName);
			if (!close || !header) return;
			var rect = (RectTransform)close;
			var parent = (RectTransform)rect.parent;
			header.rectTransform.GetWorldCorners(corners);
			Vector2 edge = parent.InverseTransformPoint(corners[2]);
			Vector2 anchor = new Vector2(parent.rect.xMax, parent.rect.yMax);
			rect.anchoredPosition = edge - anchor - new Vector2(HeaderMargin.x + EdgeGap, HeaderMargin.y + EdgeGap);
		}

		private void Apply(Graphic graphic)
		{
			bool text = graphic is Text;
			string sprite = graphic is Image image && image.sprite ? image.sprite.name : null;
			targets.TryGetValue(graphic, out Selectable selectable);
			Color32 current = graphic.color;
			Color32 normal = selectable ? (Color32)selectable.colors.normalColor : default;
			foreach (Entry entry in Palette)
			{
				if (entry.Text != text || (entry.Sprite != null && entry.Sprite != sprite)) continue;
				bool tinted = selectable && entry.HasStates;
				bool isDark = tinted ? Same(normal, entry.Dark) && Same(current, Color.white) : Same(current, entry.Dark);
				bool isLight = Same(current, entry.Light) && (!tinted || !Same(normal, entry.Dark));
				if (!isDark && !isLight) continue;
				if (isDark == Dark)
				{
					// Light keeps the prefab colors but still hides resting toolbar tiles.
					if (tinted && entry.HiddenAtRest && selectable.colors.normalColor.a != 0) SetStates(selectable, entry);
					return;
				}
				byte alpha = current.a;
				if (tinted) SetStates(selectable, entry);
				Color32 color = Dark && tinted ? new Color32(255, 255, 255, 255) : Dark ? entry.Dark : entry.Light;
				color.a = alpha;
				graphic.color = color;
				return;
			}
			// A tile given a state color by code (e.g. AUTO on) must stay visible at rest.
			if (selectable && sprite == "ToolBackground" && selectable.colors.normalColor.a == 0)
				selectable.colors = ColorBlock.defaultColorBlock;
		}

		private static void SetStates(Selectable selectable, Entry entry)
		{
			ColorBlock colors = ColorBlock.defaultColorBlock;
			if (Dark)
			{
				colors.normalColor = (Color)entry.Dark;
				colors.highlightedColor = (Color)entry.Highlighted;
				colors.pressedColor = (Color)entry.Pressed;
				colors.selectedColor = (Color)entry.Dark;
				Color disabled = (Color)entry.Dark;
				disabled.a = 0.5f;
				colors.disabledColor = disabled;
			}
			if (entry.HiddenAtRest)
			{
				// A square translucent gray tile on hover and press, nothing at rest.
				colors.normalColor = Transparent(colors.normalColor);
				colors.selectedColor = Transparent(colors.selectedColor);
				colors.disabledColor = Transparent(colors.disabledColor);
				colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 0.16f);
				colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 0.32f);
			}
			selectable.colors = colors;
		}

		private static Color Transparent(Color color)
		{
			color.a = 0;
			return color;
		}

		private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

		private void ApplySkinSprites()
		{
			if (!ArcSkinManager.Instance) return;
			if (!Dark)
			{
				ApplyDialogLayouts();
				return;
			}
			if (darkSprites == null) darkSprites = LoadDarkSprites();
			if (darkSprites == null) return;
			ApplyDialogLayouts();
			Sprite Get(int i) => darkSprites[i];
			var skin = ArcSkinManager.Instance;
			foreach (AdeSingleDialog dialog in skin.SingleDialogs)
				if (dialog) dialog.SetDialogSkin(Get(0), Get(1), Get(2), Get(3), Get(4));
			foreach (AdeDualDialog dialog in skin.DualDialogs)
				if (dialog) dialog.SetDialogSkin(Get(0), Get(1), Get(5), Get(6), Get(7), Get(8), Get(9), Get(10));
			skin.Toast.sprite = Get(1);
			skin.SongInfo.sprite = Get(11);
			var compose = ArcadeComposeManager.Instance;
			compose.SetPlaySprite(Get(12));
			compose.SetPlayPressedSprite(Get(13));
			compose.SetPauseSprite(Get(14));
			compose.SetPausePressedSprite(Get(15));
		}

		// Dark dialogs follow VS Code: a left-aligned title, a close button and compact
		// buttons on the right. Corners are concentric with the 22px dialog corner: each
		// control sits 12px from the edge and has a 10px radius. Light restores the
		// CommonSingle/CommonDual prefab layout.
		private void ApplyDialogLayouts()
		{
			var skin = ArcSkinManager.Instance;
			foreach (AdeSingleDialog dialog in skin.SingleDialogs)
			{
				if (!dialog) continue;
				LayoutTitle(dialog.Title);
				LayoutButton(dialog.CompleteButton, dialog.ButtonText, new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(788, 58), 0);
				LayoutClose(dialog);
			}
			foreach (AdeDualDialog dialog in skin.DualDialogs)
			{
				if (!dialog) continue;
				LayoutTitle(dialog.Title);
				LayoutButton(dialog.LeftButton, dialog.LeftButtonText, new Vector2(0, 0), new Vector2(-1, 4), new Vector2(395, 58), 1);
				LayoutButton(dialog.RightButton, dialog.RightButtonText, new Vector2(1, 0), new Vector2(1, 4), new Vector2(395, 58), 0);
				LayoutClose(dialog);
			}
		}

		private const float ButtonWidth = 170, ButtonHeight = 46, ButtonGap = 12, TitlePadding = 28, TitleHeight = 74;
		// Controls sit this far inside the visible dialog edge; 22 - 12 gives their 10px radius.
		private const float EdgeGap = 12;
		// Transparent margins of the dark dialog sprites (right, bottom).
		private static readonly Vector2 BackgroundMargin = new Vector2(1, 2), HeaderMargin = new Vector2(1, 0);
		private const string TitleSpacerName = "ThemeTitleSpacer", CloseName = "ThemeClose";

		// The view's VerticalLayoutGroup owns the title rect, so in dark mode the title leaves
		// the layout and a spacer of the same height keeps the content below in place.
		private static void LayoutTitle(Text title)
		{
			if (!title) return;
			var rect = (RectTransform)title.transform;
			var element = title.GetComponent<LayoutElement>();
			Transform spacer = rect.parent.Find(TitleSpacerName);
			if (Dark && !spacer && element)
			{
				var go = new GameObject(TitleSpacerName, typeof(RectTransform), typeof(LayoutElement));
				go.transform.SetParent(rect.parent, false);
				go.transform.SetSiblingIndex(rect.GetSiblingIndex());
				go.GetComponent<LayoutElement>().preferredHeight = TitleHeight;
			}
			else if (!Dark && spacer) Destroy(spacer.gameObject);
			if (element) element.ignoreLayout = Dark;
			if (Dark)
			{
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.anchorMin = new Vector2(0, 1);
				rect.anchorMax = new Vector2(1, 1);
				rect.sizeDelta = new Vector2(-TitlePadding * 2, TitleHeight);
				rect.anchoredPosition = new Vector2(0, -TitleHeight / 2);
			}
			title.alignment = Dark ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
			title.fontSize = Dark ? 30 : 40;
		}

		// slot counts buttons from the right edge in the dark layout.
		private static void LayoutButton(Button button, Text label, Vector2 lightAnchor, Vector2 lightPosition, Vector2 lightSize, int slot)
		{
			if (!button) return;
			var rect = (RectTransform)button.transform;
			Vector2 anchor = Dark ? new Vector2(1, 0) : lightAnchor;
			rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
			// The dark position is set by AlignDialogControls once the layout is known.
			if (!Dark) rect.anchoredPosition = lightPosition;
			rect.sizeDelta = Dark ? new Vector2(ButtonWidth, ButtonHeight) : lightSize;
			button.image.type = Dark ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
			if (label) label.fontSize = Dark ? 26 : 35;
		}

		private void LayoutClose(AdeDialog dialog)
		{
			if (darkSprites == null) return;
			Transform view = dialog.View.transform;
			Transform existing = view.Find(CloseName);
			if (existing)
			{
				// Cloned dialogs copy the button with its listeners stripped.
				existing.SetAsLastSibling();
				AdeUiKit.SetOnClick(existing.GetComponent<Button>(), dialog.Close);
				return;
			}
			var go = new GameObject(CloseName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
			go.GetComponent<LayoutElement>().ignoreLayout = true;
			var rect = (RectTransform)go.transform;
			rect.SetParent(view, false);
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
			rect.sizeDelta = new Vector2(42, 42);
			// The secondary button sprite (10px radius) only shows while hovered or pressed.
			var background = go.GetComponent<Image>();
			background.sprite = darkSprites[5];
			background.type = UnityEngine.UI.Image.Type.Sliced;
			var button = go.GetComponent<Button>();
			ColorBlock colors = ColorBlock.defaultColorBlock;
			colors.normalColor = new Color(1, 1, 1, 0);
			colors.selectedColor = new Color(1, 1, 1, 0);
			colors.highlightedColor = Color.white;
			colors.pressedColor = new Color(1.2f, 1.2f, 1.2f, 1);
			button.colors = colors;
			AdeUiKit.SetOnClick(button, dialog.Close);
			var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
			var iconRect = (RectTransform)icon.transform;
			iconRect.SetParent(rect, false);
			iconRect.sizeDelta = new Vector2(24, 24);
			var iconImage = icon.GetComponent<Image>();
			iconImage.sprite = closeIcon;
			iconImage.color = new Color32(0xCC, 0xCC, 0xCC, 0xFF);
			iconImage.raycastTarget = false;
		}

		private static Sprite[] LoadDarkSprites()
		{
			string[] names =
			{
				"Dialog/DialogTop", "Dialog/DialogBackground",
				"Dialog/ButtonSingle", "Dialog/ButtonSingleDisabled", "Dialog/ButtonSinglePressed",
				"Dialog/ButtonDualLeft", "Dialog/ButtonDualLeftDisabled", "Dialog/ButtonDualLeftPressed",
				"Dialog/ButtonDualRight", "Dialog/ButtonDualRightDisabled", "Dialog/ButtonDualRightPressed",
				"SongInfo", "PlayPause/Play", "PlayPause/PlayPressed", "PlayPause/Pause", "PlayPause/PausePressed",
			};
			var sprites = new Sprite[names.Length];
			if (Instance) Instance.closeIcon = Resources.Load<Sprite>("AffdataEdit/Icons/Close");
			for (int i = 0; i < names.Length; i++)
			{
				sprites[i] = Resources.Load<Sprite>("AffdataEdit/Theme/Dark/" + names[i]);
				if (!sprites[i])
				{
					Debug.LogWarning($"Dark theme sprite missing: {names[i]}");
					return null;
				}
			}
			return sprites;
		}

		// Settings row next to the playback sync toggle, like the arc cross-timing switch.
		private void AddToggle()
		{
			Toggle template = ArcadeComposeManager.Instance ? ArcadeComposeManager.Instance.PlaybackSyncToggle : null;
			Transform templateRow = template ? template.GetComponentInParent<HorizontalLayoutGroup>(true)?.transform : null;
			if (templateRow == null) return;
			GameObject row = AdeUiKit.CloneRow(templateRow, templateRow.parent, "DarkUi");
			row.transform.SetSiblingIndex(templateRow.GetSiblingIndex() + 1);
			row.GetComponentInChildren<Text>(true).text = "深色界面";
			Toggle toggle = row.GetComponentInChildren<Toggle>(true);
			toggle.SetIsOnWithoutNotify(Dark);
			toggle.onValueChanged.AddListener(SetDark);
		}
	}
}
