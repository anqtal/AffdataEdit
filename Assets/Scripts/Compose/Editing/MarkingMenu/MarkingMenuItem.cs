using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using UnityEngine.EventSystems;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Arcade.Compose.MarkingMenu
{
	public class MarkingMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
	{
		public CanvasGroup ItemCanvasGroup;
		public RectTransform ItemRectTransform;
		public Text ItemText;
		public Image Background;
		public Color InColor, OutColor;

		[Header("Settings")]
		public string StartupText;
		public UnityEvent OnConfirmed = new UnityEvent();

		[Header("Sub Menu")]
		public bool HasSubMenu;
		public float EnterTimeThreshold = 0.4f;
		public MarkingMenuItem[] SubItems;
		public UnityEvent OnHangOver = new UnityEvent();

		public float Degree { get; set; }
		public string Text
		{
			get
			{
				return ItemText.text;
			}
			set
			{
				caption = value;
				ItemText.text = value + (HasSubMenu && !verticalStyle ? " ·" : "");
			}
		}
		public Vector2 Position
		{
			get
			{
				return ItemRectTransform.anchoredPosition;
			}
			set
			{
				ItemRectTransform.anchoredPosition = value;
			}
		}

		public bool PointerInItem { get; private set; }
		public bool PointerHangOver
		{
			get
			{
				return PointerInItem && pointerInItemTiming > EnterTimeThreshold;
			}
		}
		private float pointerInItemTiming;
		private bool active = false;
		private bool initialized;
		private bool verticalStyle;
		private string caption;
		private Image separator;
		private ContextMenuIcon icon;
		private Text chevron;
		private MotionHandle fadeMotion;
		private int themeRevision = -1;

		private void Start()
		{
			InitializeText();
			ApplyTheme();
		}
		private void InitializeText()
		{
			if (initialized) return;
			Text = StartupText;
			initialized = true;
		}
		private void OnDestroy()
		{
			fadeMotion.TryCancel();
		}
		public void Show(bool vertical = false)
		{
			gameObject.SetActive(true);
			verticalStyle = vertical;
			InitializeText();
			Text = caption;
			var layout = GetComponent<VerticalLayoutGroup>();
			layout.childAlignment = vertical ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
			layout.childForceExpandWidth = vertical;
			var padding = layout.padding;
			padding.top = padding.bottom = vertical ? 4 : 12;
			padding.left = vertical ? 40 : 12;
			padding.right = vertical ? 28 : 12;
			if (icon) icon.gameObject.SetActive(vertical);
			if (vertical && HasSubMenu && !chevron)
			{
				var go = new GameObject("SubmenuArrow", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
				go.layer = gameObject.layer;
				go.transform.SetParent(transform, false);
				go.GetComponent<LayoutElement>().ignoreLayout = true;
				chevron = go.GetComponent<Text>();
				AdeEditorText.Configure(chevron, AdeEditorText.CaptionSize);
				chevron.text = ">";
				chevron.alignment = TMPro.TextAlignmentOptions.Center;
				var rect = chevron.rectTransform;
				rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
				rect.anchoredPosition = new Vector2(-14, 0);
				rect.sizeDelta = new Vector2(12, 24);
			}
			if (chevron) chevron.gameObject.SetActive(vertical && HasSubMenu);
			if (separator) separator.gameObject.SetActive(false);
			var fitter = GetComponent<ContentSizeFitter>();
			fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			active = true;
			ItemCanvasGroup.blocksRaycasts = true;
			GetComponent<Canvas>().sortingOrder = AdeMarkingMenuManager.MenuSortingOrder + (vertical ? 1 : 0);
			fadeMotion.TryComplete();
			ApplyTheme();
			LayoutRebuilder.ForceRebuildLayoutImmediate(ItemRectTransform);
			fadeMotion = LMotion.Create(ItemCanvasGroup.alpha, 1f, 0.15f)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.Update)
				.BindToAlpha(ItemCanvasGroup);
		}
		internal void SetIcon(string kind)
		{
			if (!icon)
			{
				var go = new GameObject("ContextMenuIcon", typeof(RectTransform), typeof(ContextMenuIcon), typeof(LayoutElement));
				go.layer = gameObject.layer;
				go.transform.SetParent(transform, false);
				go.GetComponent<LayoutElement>().ignoreLayout = true;
				icon = go.GetComponent<ContextMenuIcon>();
				icon.raycastTarget = false;
				var rect = icon.rectTransform;
				rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
				rect.anchoredPosition = new Vector2(20, 0);
				rect.sizeDelta = new Vector2(20, 20);
			}
			icon.Kind = kind;
			icon.SetVerticesDirty();
			icon.gameObject.SetActive(verticalStyle);
		}
		internal void SetRowSize(float width, float height, bool dividerAfter = true)
		{
			var fitter = GetComponent<ContentSizeFitter>();
			fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
			ItemRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			ItemRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
			if (!separator)
			{
				var go = new GameObject("ContextMenuRowDivider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
				go.layer = gameObject.layer;
				go.transform.SetParent(transform, false);
				go.GetComponent<LayoutElement>().ignoreLayout = true;
				separator = go.GetComponent<Image>();
				separator.raycastTarget = false;
				var rect = separator.rectTransform;
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = new Vector2(1, 0);
				rect.offsetMin = new Vector2(12, 0);
				rect.offsetMax = new Vector2(-12, 1);
			}
			separator.color = AdeUiTheme.Current.Border;
			separator.gameObject.SetActive(dividerAfter);
			LayoutRebuilder.ForceRebuildLayoutImmediate(ItemRectTransform);
		}
		public void Hide()
		{
			active = false;
			ItemCanvasGroup.blocksRaycasts = false;
			PointerInItem = false;
			pointerInItemTiming = 0;
			fadeMotion.TryComplete();
			if (verticalStyle)
			{
				ItemCanvasGroup.alpha = 0;
				gameObject.SetActive(false);
				return;
			}
			fadeMotion = LMotion.Create(ItemCanvasGroup.alpha, 0f, 0.15f)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.Update)
				.WithOnComplete(() =>
				{
					ApplyTheme();
					if (!active)
					{
						gameObject.SetActive(false);
					}
				})
				.BindToAlpha(ItemCanvasGroup);
		}

		public void OnPointerEnter(PointerEventData data)
		{
			if (active)
			{
				PointerInItem = true;
				ApplyTheme();
				pointerInItemTiming = 0;
			}
		}
		public void OnPointerExit(PointerEventData data)
		{
			if (active)
			{
				PointerInItem = false;
				ApplyTheme();
				pointerInItemTiming = 0;
			}
		}
		public void OnPointerClick(PointerEventData data)
		{
			if (active && data.button == PointerEventData.InputButton.Left)
				AdeMarkingMenuManager.Instance.ClickItem(this);
		}
		private void ApplyTheme()
		{
			if (!AdeUiTheme.Instance)
			{
				Background.color = PointerInItem ? InColor : OutColor;
				return;
			}
			themeRevision = AdeUiTheme.Revision;
			Background.sprite = verticalStyle ? null : AdeUiTheme.ContextMenuSurface(PointerInItem);
			Background.type = verticalStyle ? Image.Type.Simple : Image.Type.Sliced;
			Background.color = verticalStyle ? (PointerInItem ? AdeUiTheme.Current.Selection : Color.clear) : Color.white;
			Background.rectTransform.sizeDelta = Vector2.zero;
			AdeEditorText.Configure(ItemText, verticalStyle ? 22 : AdeEditorText.BodySize);
			ItemText.alignment = verticalStyle ? TMPro.TextAlignmentOptions.Left : TMPro.TextAlignmentOptions.Center;
			ItemText.color = AdeUiTheme.Current.Text;
			if (icon) icon.color = AdeUiTheme.Current.Muted;
			if (chevron) chevron.color = AdeUiTheme.Current.Muted;
			var shadow = Background.GetComponent<Shadow>();
			if (shadow)
			{
				shadow.enabled = !verticalStyle;
				shadow.effectColor = new Color(0, 0, 0, AdeUiTheme.Dark ? .24f : .12f);
			}
		}

		private void Update()
		{
			if (themeRevision != AdeUiTheme.Revision) ApplyTheme();
			if (Mouse.current.rightButton.isPressed && PointerInItem)
			{
				pointerInItemTiming += Time.deltaTime;
			}
		}
	}
}
