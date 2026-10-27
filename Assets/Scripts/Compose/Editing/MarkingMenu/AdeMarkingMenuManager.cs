using System.Collections.Generic;
using UnityEngine;
using Arcade.Util.UnityExtension;
using Arcade.Compose.MarkingMenu;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public abstract class AdeMarkingMenuItemProvider : MonoBehaviour
	{
		public virtual bool IsOnlyMarkingMenu { get => false; }
		public virtual MarkingMenuItem[] MarkingMenuItems { get => null; }
	}
	public class AdeMarkingMenuManager : MonoBehaviour
	{
		public static AdeMarkingMenuManager Instance { get; private set; }
		public static bool BlocksEditorInput => Instance && (Instance.isOn || Instance.consumedFrame == Time.frameCount);
		public bool IsOpen => isOn;
		internal const int MenuSortingOrder = 30;

		public MarkingMenuLine Line;
		public List<AdeMarkingMenuItemProvider> Providers = new List<AdeMarkingMenuItemProvider>();

		private readonly Stack<MarkingMenuItem[]> shownItems = new Stack<MarkingMenuItem[]>();
		private readonly List<MarkingMenuItem> parentItems = new List<MarkingMenuItem>();
		private GameObject inputBlocker;
		private MarkingMenuItem backItem;
		private ArcChart openedChart;
		private bool keepOpen;
		private int consumedFrame = -1;

		private bool isOn = false;
		private void Awake()
		{
			Instance = this;
		}
		private void Update()
		{
			var mouse = Mouse.current;
			if (mouse == null) return;
			var keyboard = Keyboard.current;
			bool dialogOpen = AdeDialogManager.Instance && AdeDialogManager.Instance.Opening.childCount > 0;
			if (isOn && (!ArcadeComposeManager.Instance.IsEditorMode || dialogOpen
				|| openedChart != ArcGameplayManager.Instance.Chart))
			{
				Exit();
				return;
			}
			if (isOn && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
			{
				Exit();
				return;
			}
			if (mouse.rightButton.wasPressedThisFrame)
			{
				if (isOn) Exit();
				else if (!dialogOpen && ArcadeComposeManager.Instance.IsEditorMode && PointerOverChart())
					Enter();
				return;
			}
			if (!isOn) return;
			if (keepOpen)
			{
				if (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame) ReturnParentMenu();
				return;
			}
			if (!mouse.rightButton.isPressed)
			{
				var target = PointerTarget();
				var selected = target ? target.GetComponentInParent<MarkingMenuItem>() : null;
				if (System.Array.IndexOf(shownItems.Peek(), selected) < 0) selected = null;
				Exit();
				if (selected) selected.OnConfirmed.Invoke();
			}
			else Check();
		}

		private void LateUpdate()
		{
			// Retain the raycast shield through the dismissal click, including its release.
			if (!isOn && inputBlocker && consumedFrame != Time.frameCount
				&& (Mouse.current == null || !Mouse.current.leftButton.isPressed))
				inputBlocker.SetActive(false);
		}

		private void OnApplicationFocus(bool focused)
		{
			if (!focused) Exit();
		}

		private void OnDisable()
		{
			Exit();
			if (inputBlocker) inputBlocker.SetActive(false);
		}

		private void OnDestroy()
		{
			if (inputBlocker) Destroy(inputBlocker);
			if (backItem) Destroy(backItem.gameObject);
			if (Instance == this) Instance = null;
		}

		public void Enter()
		{
			if (isOn) return;
			var items = GetCurrentMenuItems();
			if (items.Length == 0) return;
			keepOpen = ArcadeComposeManager.Instance.ArcadePreference.KeepContextMenuOpen;
			openedChart = ArcGameplayManager.Instance.Chart;
			CreateInputBlocker();
			inputBlocker.SetActive(true);
			EventSystem.current.SetSelectedGameObject(null);
			isOn = true;
			Line.Enable = true;
			Line.AddJoint();
			ShowAt(InputExtension.scaledMousePosition, items);
		}

		private static bool PointerOverChart()
		{
			var target = PointerTarget();
			return target && target.GetComponentInParent<AdeGameplayContentInputHandler>();
		}

		private static GameObject PointerTarget()
		{
			// Raycast this position rather than using the previous frame's hover state.
			var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
			var hits = new List<RaycastResult>();
			EventSystem.current.RaycastAll(pointer, hits);
			return hits.Count > 0 ? hits[0].gameObject : null;
		}

		// Closing never confirms a highlighted item. Confirmation is an explicit action.
		public void Exit()
		{
			if (!isOn) return;
			consumedFrame = Time.frameCount;
			isOn = false;
			Line.Enable = false;
			foreach (var items in shownItems)
				foreach (var item in items)
					item.Hide();
			shownItems.Clear();
			parentItems.Clear();
			if (backItem) backItem.Hide();
			openedChart = null;
		}

		public void ClickItem(MarkingMenuItem item)
		{
			if (!isOn || !keepOpen) return;
			if (item == backItem) { ReturnParentMenu(); return; }
			if (System.Array.IndexOf(shownItems.Peek(), item) < 0) return;
			if (item.HasSubMenu && item.SubItems.Length > 0)
			{
				parentItems.Add(item);
				ShowSubMenu(item.SubItems);
				ShowBackItem(item);
				return;
			}
			// Arc/ArcTap parents select a creation mode. Defer that until a leaf is chosen,
			// so browsing a submenu and cancelling cannot change the current tool.
			var parents = parentItems.ToArray();
			Exit();
			foreach (var parent in parents) parent.OnConfirmed.Invoke();
			item.OnConfirmed.Invoke();
		}

		private void CreateInputBlocker()
		{
			if (inputBlocker) return;
			var root = ArcadeComposeManager.Instance.EditorCanvas.GetComponent<Canvas>();
			inputBlocker = new GameObject("ContextMenuInputBlocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button));
			inputBlocker.layer = root.gameObject.layer;
			var rect = (RectTransform)inputBlocker.transform;
			rect.SetParent(root.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			var canvas = inputBlocker.GetComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingLayerID = root.sortingLayerID;
			canvas.sortingOrder = MenuSortingOrder - 1;
			var image = inputBlocker.GetComponent<Image>();
			image.color = Color.clear;
			var button = inputBlocker.GetComponent<Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.None;
			button.navigation = new Navigation { mode = Navigation.Mode.None };
			button.onClick.AddListener(Exit);
		}

		private void ShowBackItem(MarkingMenuItem template)
		{
			if (!backItem)
			{
				backItem = Instantiate(template, template.transform.parent);
				backItem.name = "ContextMenuBack";
				backItem.StartupText = "返回";
				backItem.HasSubMenu = false;
				backItem.OnConfirmed = new UnityEvent();
				backItem.OnHangOver = new UnityEvent();
				backItem.SubItems = System.Array.Empty<MarkingMenuItem>();
				backItem.Text = backItem.StartupText;
			}
			backItem.Position = template.Position;
			backItem.Show();
		}

		public void Check()
		{
			foreach (var item in shownItems.Peek())
			{
				if (item.PointerHangOver)
				{
					if (item.HasSubMenu)
					{
						item.OnHangOver.Invoke();
						item.OnConfirmed.Invoke();
						ShowSubMenu(item.SubItems);
						return;
					}
				};
			}
		}
		public MarkingMenuItem[] GetCurrentMenuItems()
		{
			List<MarkingMenuItem> items = new List<MarkingMenuItem>();
			foreach (var provider in Providers)
			{
				var it = provider.MarkingMenuItems;
				if (it == null) continue;
				if (provider.IsOnlyMarkingMenu)
				{
					return it;
				}
				items.AddRange(it);
			}
			return items.ToArray();
		}

		public void ShowSubMenu(MarkingMenuItem[] items, float from = 0, float to = 360)
		{
			Line.AddJoint();
			var parent = shownItems.Peek();
			foreach (var item in parent) item.Hide();
			ShowAt(InputExtension.scaledMousePosition, items, from, to);
		}
		public void ReturnParentMenu()
		{
			if (!isOn || shownItems.Count <= 1) return;
			consumedFrame = Time.frameCount;
			MarkingMenuItem[] items = shownItems.Pop();
			foreach (var item in items) item.Hide();
			items = shownItems.Peek();
			foreach (var item in items) item.Show();
			Line.RemoveLastJoint();
			if (parentItems.Count > 0) parentItems.RemoveAt(parentItems.Count - 1);
			if (parentItems.Count > 0) ShowBackItem(parentItems[parentItems.Count - 1]);
			else if (backItem) backItem.Hide();
		}

		private float GetOptimizedDegree(float degree)
		{
			float remain = 0;
			float normalized = degree;
			while (normalized >= 180)
			{
				normalized -= 180;
				remain += 180;
			}
			while (normalized < 0)
			{
				normalized += 180;
				remain -= 180;
			}

			float d = normalized / 90f - 1;
			bool neg = d < 0;
			d = Mathf.Abs(d);
			d = Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Pow(d, 1.2f) / 2));
			d = (d * (neg ? -1 : 1) + 1) * 90f;
			d += remain;

			return d;
		}
		private float GetEllipseLength(float degree, float itemCount)
		{
			if (itemCount > 4) itemCount += itemCount % 2;
			return MathfExtension.EvaluateEllipse(degree, 60 + 10 * Mathf.Max(6, itemCount), 50 + 12 * Mathf.Max(3, itemCount));
		}

		private static void SeparateItems(Vector2 center, MarkingMenuItem[] items)
		{
			// Keep the existing directions, expanding only enough for the measured labels.
			const float gap = 12;
			float scale = 1;
			for (int i = 0; i < items.Length; i++)
				for (int j = i + 1; j < items.Length; j++)
				{
					Vector2 delta = items[i].Position - items[j].Position;
					float x = Mathf.Abs(delta.x), y = Mathf.Abs(delta.y);
					if (x < .001f && y < .001f) continue;
					Vector2 space = (items[i].ItemRectTransform.rect.size + items[j].ItemRectTransform.rect.size) / 2 + Vector2.one * gap;
					float horizontal = x < .001f ? float.PositiveInfinity : space.x / x;
					float vertical = y < .001f ? float.PositiveInfinity : space.y / y;
					scale = Mathf.Max(scale, Mathf.Min(horizontal, vertical));
				}
			if (scale <= 1) return;
			foreach (var item in items) item.Position = center + (item.Position - center) * scale;
		}

		public void ShowAt(Vector2 position, MarkingMenuItem[] items, float from = 0, float to = 360)
		{
			int itemCount = items.Length;
			float degreeDelta = (to - from) / (itemCount > 4 ? itemCount % 2 + itemCount : itemCount);
			for (int i = 0; i < itemCount; ++i)
			{
				float degree = from + degreeDelta * i + (itemCount > 4 ? 180 : 0);
				degree = GetOptimizedDegree(degree);
				float length = GetEllipseLength(degree, itemCount);
				items[i].Show();
				items[i].Position = position + new Vector2(length * Mathf.Cos(degree * Mathf.Deg2Rad), length * Mathf.Sin(degree * Mathf.Deg2Rad));
				items[i].Degree = degree;
			}
			SeparateItems(position, items);
			shownItems.Push(items);
		}
		public void ShowSubMenuAt(Vector2 position, MarkingMenuItem[] items, float from = 0, float to = 360)
		{
			int itemCount = items.Length;
			float degreeDelta = (to - from) / (itemCount - 1 == 0 ? 1 : itemCount - 1);
			for (int i = 0; i < itemCount; ++i)
			{
				float degree = from + degreeDelta * i;
				float length = MathfExtension.EvaluateEllipse(degree, 180, 100);
				items[i].Show();
				items[i].Position = position + new Vector2(length * Mathf.Cos(degree * Mathf.Deg2Rad), length * Mathf.Sin(degree * Mathf.Deg2Rad));
				items[i].Degree = degree;
			}
			shownItems.Push(items);
		}
	}
}
