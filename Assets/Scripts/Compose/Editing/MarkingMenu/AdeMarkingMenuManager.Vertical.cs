using System.Collections.Generic;
using Affdata.Core.Chart;
using Arcade.Compose.MarkingMenu;
using Arcade.Compose.Operation;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.Events;

namespace Arcade.Compose
{
	public partial class AdeMarkingMenuManager
	{
		private const float VerticalButtonWidth = 48;
		private MarkingMenuItem arcMoreItem;
		private MarkingMenuItem[] arcTapItems;

		// The vertical menu regroups click-to-create items: Arc applies its last settings while a
		// square button beside it opens the Arc options. ArcTap modes and 不创建/退出 share rows.
		private MarkingMenuItem[] ArrangeVerticalItems(MarkingMenuItem[] items)
		{
			var create = AdeClickToCreate.Instance;
			var list = new List<MarkingMenuItem>(items);
			foreach (var item in list) item.VerticalColumn = 0;
			var arc = create.ClickToCreateItems[3];
			int index = list.IndexOf(arc);
			if (index >= 0)
			{
				EnsureVerticalArcItems(arc);
				arc.VerticalColumn = 1;
				arcMoreItem.VerticalColumn = 2;
				list.Insert(index + 1, arcMoreItem);
			}
			var arcTap = create.ClickToCreateItems[4];
			index = list.IndexOf(arcTap);
			if (index >= 0)
			{
				if (arcTapItems == null)
				{
					arcTapItems = new[]
					{
						CloneVerticalItem(arcTap, "ContextMenuArcTap", "ArcTap"),
						CloneVerticalItem(arcTap, "ContextMenuSingleArcTap", "单 ArcTap")
					};
					for (int i = 0; i < arcTapItems.Length; i++)
					{
						var mode = (ClickToCreateArctapMode)i;
						arcTapItems[i].IconKind = () => "create-arctap";
						arcTapItems[i].OnConfirmed.AddListener(() =>
						{
							create.SetClickToCreateMode(ClickToCreateMode.ArcTap);
							create.SetClickToCreateArctapMode(mode);
						});
					}
				}
				arcTapItems[0].VerticalColumn = 1;
				arcTapItems[1].VerticalColumn = 2;
				list.RemoveAt(index);
				list.InsertRange(index, arcTapItems);
			}
			var none = create.ClickToCreateItems[0];
			var exit = create.ClickToCreateItems[5];
			if (list.Contains(none) && list.Remove(exit))
			{
				list.Insert(list.IndexOf(none) + 1, exit);
				none.VerticalColumn = 1;
				exit.VerticalColumn = 2;
			}
			return list.ToArray();
		}

		private void EnsureVerticalArcItems(MarkingMenuItem arc)
		{
			if (arcMoreItem) return;
			var create = AdeClickToCreate.Instance;
			MarkingMenuItem Find(MarkingMenuItem[] items, string name) => System.Array.Find(items, item => item && item.name == name);
			var isVoid = Find(arc.SubItems, "ArcIsVoid");
			var more = Find(arc.SubItems, "ArcType");
			var s = Find(arc.SubItems, "S");
			var si = Find(arc.SubItems, "SI");
			var so = Find(arc.SubItems, "SO");
			var b = Find(arc.SubItems, "B");
			var combined = more ? more.SubItems : System.Array.Empty<MarkingMenuItem>();

			if (isVoid)
			{
				isVoid.VerticalCaption = "黑线";
				isVoid.KeepsMenuOpen = true;
				isVoid.IconKind = () => create.ArcIsVoid ? "check" : null;
			}
			void Curve(MarkingMenuItem item, ArcCurveType type, string caption = null)
			{
				if (!item) return;
				item.VerticalCaption = caption;
				item.IconKind = () => create.ArcCurveType == type ? "check" : null;
			}
			Curve(s, ArcCurveType.S);
			Curve(b, ArcCurveType.B);
			Curve(Find(combined, "SISI"), ArcCurveType.SiSi, "Si");
			Curve(Find(combined, "SISO"), ArcCurveType.SiSo, "So");
			Curve(Find(combined, "SOSI"), ArcCurveType.SoSi, "Si");
			Curve(Find(combined, "SOSO"), ArcCurveType.SoSo, "So");
			// Si/So apply themselves on click and derive the Y easing on hover.
			if (si)
			{
				si.IconKind = () => create.ArcCurveType == ArcCurveType.Si ? "check" : null;
				si.ClickConfirms = true;
				si.VerticalSubItems = new[] { Find(combined, "SISI"), Find(combined, "SISO") };
			}
			if (so)
			{
				so.IconKind = () => create.ArcCurveType == ArcCurveType.So ? "check" : null;
				so.ClickConfirms = true;
				so.VerticalSubItems = new[] { Find(combined, "SOSI"), Find(combined, "SOSO") };
			}
			arc.VerticalLeaf = true;

			// 颜色 derives one row per color; a fresh parent keeps 切换颜色's cycling out of hover and click.
			var colors = new MarkingMenuItem[ArcColors.BlackId + 1];
			for (int id = 0; id < colors.Length; id++)
			{
				int value = id;
				colors[id] = CloneVerticalItem(arc, "ContextMenuArcColor" + id, AdeClickToCreate.ArcColorName(id));
				colors[id].OnConfirmed.AddListener(() => create.SetArcColor(value));
				colors[id].IconKind = () => create.ArcColor == value ? "check" : null;
			}
			var colorMenu = CloneVerticalItem(arc, "ContextMenuArcColors", "颜色");
			colorMenu.VerticalSubItems = colors;

			arcMoreItem = CloneVerticalItem(arc, "ContextMenuArcOptions", "");
			arcMoreItem.OnConfirmed.AddListener(arc.OnConfirmed.Invoke);
			arcMoreItem.VerticalLeaf = false;
			arcMoreItem.VerticalWidth = VerticalButtonWidth;
			var items = new List<MarkingMenuItem> { isVoid, colorMenu, s, si, so, b };
			items.RemoveAll(item => !item);
			arcMoreItem.VerticalSubItems = items.ToArray();
		}

		private static MarkingMenuItem CloneVerticalItem(MarkingMenuItem template, string name, string text)
		{
			var item = Instantiate(template, template.transform.parent);
			item.name = name;
			// Runtime decorations cloned from the template would be orphaned; the clone builds its own.
			for (int i = item.transform.childCount - 1; i >= 0; i--)
			{
				var child = item.transform.GetChild(i);
				if (child.name == "ContextMenuIcon" || child.name == "SubmenuArrow" || child.name == "ContextMenuRowDivider")
					DestroyImmediate(child.gameObject);
			}
			item.StartupText = text;
			item.HasSubMenu = false;
			item.SubItems = System.Array.Empty<MarkingMenuItem>();
			item.OnHangOver = new UnityEvent();
			item.OnConfirmed = new UnityEvent();
			item.Text = text;
			return item;
		}
	}
}
