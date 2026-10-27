using Arcade.Compose.MarkingMenu;
using Arcade.Util.UnityExtension;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public partial class AdeMarkingMenuManager
	{
		private GameObject menuPanel;

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

		public void ShowAt(Vector2 position, MarkingMenuItem[] items, float from = 0, float to = 360)
		{
			if (verticalStyle)
			{
				foreach (var item in items) item.Show(true);
				LayoutVerticalMenu(position, items);
				shownItems.Push(new MenuPage(items, position));
				return;
			}
			int itemCount = items.Length;
			float degreeDelta = (to - from) / (itemCount > 4 ? itemCount % 2 + itemCount : itemCount);
			for (int i = 0; i < itemCount; ++i)
			{
				float degree = from + degreeDelta * i + (itemCount > 4 ? 180 : 0);
				degree = GetOptimizedDegree(degree);
				float length = GetEllipseLength(degree, itemCount);
				items[i].Show(verticalStyle);
				items[i].Position = position + new Vector2(length * Mathf.Cos(degree * Mathf.Deg2Rad), length * Mathf.Sin(degree * Mathf.Deg2Rad));
				items[i].Degree = degree;
			}
			shownItems.Push(new MenuPage(items, position));
		}
		public void ShowSubMenuAt(Vector2 position, MarkingMenuItem[] items, float from = 0, float to = 360)
		{
			if (verticalStyle)
			{
				foreach (var item in items) item.Show(true);
				LayoutVerticalMenu(position, items);
				shownItems.Push(new MenuPage(items, position));
				return;
			}
			int itemCount = items.Length;
			float degreeDelta = (to - from) / (itemCount - 1 == 0 ? 1 : itemCount - 1);
			for (int i = 0; i < itemCount; ++i)
			{
				float degree = from + degreeDelta * i;
				float length = MathfExtension.EvaluateEllipse(degree, 180, 100);
				items[i].Show(verticalStyle);
				items[i].Position = position + new Vector2(length * Mathf.Cos(degree * Mathf.Deg2Rad), length * Mathf.Sin(degree * Mathf.Deg2Rad));
				items[i].Degree = degree;
			}
			shownItems.Push(new MenuPage(items, position));
		}
		private void LayoutVerticalMenu(Vector2 origin, MarkingMenuItem[] items, MarkingMenuItem back = null)
		{
			if (items.Length == 0) return;
			const float padding = 4;
			float width = 200, height = 40;
			foreach (var item in items)
			{
				width = Mathf.Max(width, item.ItemRectTransform.rect.width);
				height = Mathf.Max(height, item.ItemRectTransform.rect.height);
			}
			if (back) width = Mathf.Max(width, back.ItemRectTransform.rect.width);
			int count = items.Length + (back ? 1 : 0);
			Vector2 size = new Vector2(width + padding * 2, height * count + padding * 2);
			Vector2 available = ArcadeComposeManager.Instance.EditorCanvas.rect.size;
			Vector2 topLeft = origin + new Vector2(8, -8);
			topLeft.x = Mathf.Clamp(topLeft.x, padding, Mathf.Max(padding, available.x - size.x - padding));
			topLeft.y = Mathf.Clamp(topLeft.y, Mathf.Min(size.y + padding, available.y - padding), available.y - padding);
			int row = 0;
			if (back)
			{
				back.SetRowSize(width, height);
				back.Position = topLeft + new Vector2(padding + width / 2, -padding - height * (++row - .5f));
			}
			foreach (var item in items)
			{
				item.SetRowSize(width, height, row < count - 1);
				item.Position = topLeft + new Vector2(padding + width / 2, -padding - height * (++row - .5f));
			}
			if (!menuPanel)
			{
				var root = ArcadeComposeManager.Instance.EditorCanvas;
				menuPanel = new GameObject("VerticalContextMenuPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Shadow));
				menuPanel.layer = root.gameObject.layer;
				menuPanel.transform.SetParent(root, false);
				var canvas = menuPanel.GetComponent<Canvas>();
				canvas.overrideSorting = true;
				canvas.sortingLayerID = items[0].GetComponent<Canvas>().sortingLayerID;
				canvas.sortingOrder = MenuSortingOrder;
			}
			var rect = (RectTransform)menuPanel.transform;
			rect.anchorMin = rect.anchorMax = Vector2.zero;
			rect.pivot = new Vector2(0, 1);
			rect.anchoredPosition = topLeft;
			rect.sizeDelta = size;
			var background = menuPanel.GetComponent<Image>();
			background.sprite = AdeUiTheme.VerticalContextMenuSurface;
			background.type = Image.Type.Sliced;
			background.color = Color.white;
			var shadow = menuPanel.GetComponent<Shadow>();
			shadow.effectColor = new Color(0, 0, 0, AdeUiTheme.Dark ? .24f : .12f);
			shadow.effectDistance = new Vector2(0, -2);
			menuPanel.SetActive(true);
		}
	}
}
