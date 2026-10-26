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
				if (HasSubMenu) value += " ·";
				ItemText.text = value;
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
		private MotionHandle fadeMotion;
		private int themeRevision = -1;

		private void Start()
		{
			Text = StartupText;
			ApplyTheme();
		}
		private void OnDestroy()
		{
			fadeMotion.TryCancel();
		}
		public void Show()
		{
			gameObject.SetActive(true);
			active = true;
			ItemCanvasGroup.blocksRaycasts = true;
			GetComponent<Canvas>().sortingOrder = AdeMarkingMenuManager.MenuSortingOrder;
			fadeMotion.TryComplete();
			ApplyTheme();
			fadeMotion = LMotion.Create(ItemCanvasGroup.alpha, 1f, 0.15f)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.Update)
				.BindToAlpha(ItemCanvasGroup);
		}
		public void Hide()
		{
			active = false;
			ItemCanvasGroup.blocksRaycasts = false;
			PointerInItem = false;
			pointerInItemTiming = 0;
			fadeMotion.TryComplete();
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
			Background.sprite = AdeUiTheme.ContextMenuSurface(PointerInItem);
			Background.type = Image.Type.Sliced;
			Background.color = Color.white;
			Background.rectTransform.sizeDelta = Vector2.zero;
			AdeEditorText.Configure(ItemText, AdeEditorText.BodySize);
			ItemText.alignment = TMPro.TextAlignmentOptions.Center;
			ItemText.color = AdeUiTheme.Current.Text;
			var shadow = Background.GetComponent<Shadow>();
			if (shadow) shadow.effectColor = new Color(0, 0, 0, AdeUiTheme.Dark ? .24f : .12f);
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
