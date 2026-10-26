using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Arcade.Compose.Command;
using Arcade.Compose.Editing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

namespace Arcade.Compose
{
    public sealed partial class AdeChartToolsPanel
    {
        private readonly List<Image> backgrounds = new List<Image>();
        private readonly List<Image> sections = new List<Image>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<TMP_Text> mutedLabels = new List<TMP_Text>();
        private readonly List<Selectable> controls = new List<Selectable>();
        private readonly HashSet<Button> quietButtons = new HashSet<Button>();
        private Image closeIcon;
        private readonly Dictionary<Button, Image> actionIcons = new Dictionary<Button, Image>();
        private Color Foreground => AdeUiTheme.Current.Text;
        private Color Muted => AdeUiTheme.Current.Muted;
        private const float ControlHeight = 44, ScrollGutter = 12, TimingBpmWidth = 96, TimingBeatsWidth = 64,
            TimingApplyWidth = 56, TimingRemoveWidth = 32;

        private void BuildPanel()
        {
            var compose = ArcadeComposeManager.Instance;
            panel = Rect("ChartToolsPanel", compose.RightBar, typeof(Image), typeof(VerticalLayoutGroup), typeof(CanvasGroup));
            panelGroup = panel.GetComponent<CanvasGroup>();
            panel.SetAsLastSibling();
            panel.anchorMin = new Vector2(1, 0); panel.anchorMax = Vector2.one; panel.pivot = Vector2.one;
            float top = compose.RightBarView.GetComponent<VerticalLayoutGroup>().padding.top;
            float bottom = compose.BottomBar.sizeDelta.y - 20;
            panel.sizeDelta = new Vector2(520, -(top + bottom + 24));
            panel.anchoredPosition = new Vector2(-compose.RightBar.sizeDelta.x + 8, -top - 12);
            backgrounds.Add(panel.GetComponent<Image>());
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            Vertical(layout); layout.padding = new RectOffset(16, 16, 16, 16); layout.spacing = 12;
            var heading = Row(panel, "工具页签", 48);
            var tabStrip = Row(heading, "Tabs", 48);
            var tabLayout = tabStrip.GetComponent<HorizontalLayoutGroup>();
            tabLayout.padding = new RectOffset(4, 4, 4, 4); tabLayout.spacing = 4;
            sections.Add(tabStrip.gameObject.AddComponent<Image>());
            for (int i = 0; i < 3; i++)
            {
                var page = (Page)i;
                tabs[i] = Quiet(Button(tabStrip, Titles[i], () => Show(page)));
                Size(tabs[i].transform, -1, 40);
                tabs[i].GetComponentInChildren<TMP_Text>().fontSize = 22;
            }
            var close = Quiet(Button(heading, "", Collapse, 42));
            close.name = "Close";
            var icon = Rect("Icon", close.transform, typeof(Image));
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.sizeDelta = new Vector2(24, 24);
            closeIcon = icon.GetComponent<Image>();
            closeIcon.sprite = Resources.Load<Sprite>("AffdataEdit/Icons/Close");
            closeIcon.raycastTarget = false;
            context = MutedLabel(panel, "", 28, 20);
            pageHost = Rect("Pages", panel, typeof(LayoutElement));
            Size(pageHost, -1, 0, 1, 1);
            for (int i = 0; i < pages.Length; i++)
            {
                var scroll = Scroll(pageHost, Titles[i], out pages[i]);
                Fill((RectTransform)scroll.transform);
                // Each page retains its own scroll position and controls while hidden.
            }
            emptyState = Rect("EmptyState", panel, typeof(VerticalLayoutGroup));
            Size(emptyState, -1, 0, 1, 1);
            var emptyLayout = emptyState.GetComponent<VerticalLayoutGroup>(); Vertical(emptyLayout);
            emptyLayout.padding = new RectOffset(18, 18, 72, 0); emptyLayout.spacing = 12;
            var title = Label(emptyState, "还没有打开谱面", 40, 30); title.alignment = TextAlignmentOptions.Center;
            var hint = MutedLabel(emptyState, "打开谱面后，在这里管理\n时间组、SC 和 Camera。", 64, 20);
            hint.alignment = TextAlignmentOptions.Center;
            var openRow = Row(emptyState, "打开谱面", 40); openRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Button(openRow, "打开谱面", () => AdeProjectManager.Instance.OpenProject(), 180);
            status = MutedLabel(panel, "", 48, 20);
            status.gameObject.SetActive(false);
        }

        private void BuildBottomBar()
        {
            var timing = AdeTimingEditor.Instance;
            var oldButton = timing.CurrentTimingGroupText.GetComponentInParent<Button>().transform;
            bottomGroup = MakeDropdown(oldButton.parent, "CurrentTimingGroup", new List<string> { "默认组" }, SelectGroup);
            bottomGroup.transform.SetSiblingIndex(oldButton.GetSiblingIndex());
            Size(bottomGroup.transform, 150, 46);
            bottomGroup.captionText.rectTransform.offsetMin = new Vector2(10, 0);
            bottomGroup.captionText.rectTransform.offsetMax = new Vector2(-25, 0);
            bottomGroup.captionText.fontSize = AdeEditorText.BodySize;
            oldButton.gameObject.SetActive(false);
            var parent = oldButton.parent.parent;
            var row = Row(parent, "ChartToolsEntries", 36);
            row.SetSiblingIndex(oldButton.parent.GetSiblingIndex() + 1);
            Size(row, 332, 46);
            for (int i = 0; i < 3; i++)
            {
                var page = (Page)i;
                entries[i] = Quiet(Button(row, Titles[i], () => Toggle(page), i == 0 ? 112 : i == 1 ? 64 : 124));
                entries[i].GetComponentInChildren<TMP_Text>().fontSize = AdeEditorText.BodySize;
            }
        }

        private void BuildGroups(RectTransform parent)
        {
            var listSection = Section(parent, "时间组列表分区");
            var actions = Row(listSection, "组操作", ControlHeight);
            groupCount = Label(actions, "时间组", ControlHeight, 24);
            Button(actions, "+ 新建", AddGroup, 88);
            deleteConfirmation = Rect("删除确认", listSection, typeof(VerticalLayoutGroup));
            Vertical(deleteConfirmation.GetComponent<VerticalLayoutGroup>());
            deletePrompt = Label(deleteConfirmation, "", 48, 16);
            var confirm = Row(deleteConfirmation, "确认操作", 36);
            Button(confirm, "取消", () => deleteConfirmation.gameObject.SetActive(false), 96);
            Button(confirm, "删除组及内容", ConfirmDeleteGroup, 180);
            deleteConfirmation.gameObject.SetActive(false);
            groupScroll = Scroll(listSection, "时间组列表", out groupList, ControlHeight);
            var detailTabs = Row(parent, "时间组编辑页签", 48);
            var tabLayout = detailTabs.GetComponent<HorizontalLayoutGroup>();
            tabLayout.padding = new RectOffset(4, 4, 4, 4); tabLayout.spacing = 4;
            sections.Add(detailTabs.gameObject.AddComponent<Image>());
            for (int i = 0; i < groupTabs.Length; i++)
            {
                int index = i;
                groupTabs[i] = Quiet(Button(detailTabs, i == 0 ? "Timing" : "属性", () => ShowGroupTab(index)));
                Size(groupTabs[i].transform, -1, 40);
                groupTabs[i].GetComponentInChildren<TMP_Text>().fontSize = 22;
            }
            var propertiesSection = groupSections[1] = Section(parent, "属性分区");
            defaultGroupHint = MutedLabel(propertiesSection, "默认组不设置附加属性", 28, 20);
            var properties = Rect("组属性", propertiesSection, typeof(VerticalLayoutGroup), typeof(CanvasGroup));
            Vertical(properties.GetComponent<VerticalLayoutGroup>());
            properties.GetComponent<VerticalLayoutGroup>().spacing = 12;
            groupProperties = properties.GetComponent<CanvasGroup>();
            noInput = Check(properties, "noinput", "noinput");
            fadingHolds = Check(properties, "fadingholds", "fadingholds");
            noClip = Check(properties, "noclip", "noclip");
            angleX = Field(properties, "anglex (°)", "AngleX");
            angleY = Field(properties, "angley (°)", "AngleY");
            var apply = Row(properties, "应用属性", 36); Spacer(apply);
            Button(apply, "应用修改", ApplyGroup, 180);
            var advanced = Disclosure(properties, "属性文本");
            attributes = Input(advanced, "Attributes", -1);
            var rawActions = Row(advanced, "属性文本操作", 34); Spacer(rawActions);
            Quiet(Button(rawActions, "应用属性文本", ApplyAttributes, 132));
            var timingSection = groupSections[0] = Section(parent, "Timing分区");
            var timingActions = Row(timingSection, "Timing操作", ControlHeight);
            Label(timingActions, "Timing", ControlHeight, 24);
            Button(timingActions, "+ 当前时间", AddTiming, 128);
            var columns = Row(timingSection, "Timing列名", 28);
            foreach (string title in new[] { "时间 / ms", "BPM", "拍数" })
                MutedLabel(columns, title, 28, 18).margin = new Vector4(10, 0, 0, 0);
            Rect("Apply", columns); Rect("Remove", columns);
            TimingColumns(columns, 28);
            columns.GetComponent<HorizontalLayoutGroup>().padding.right = (int)ScrollGutter;
            timingScroll = Scroll(timingSection, "Timing列表", out timingList, ControlHeight);
            timingList.GetComponent<VerticalLayoutGroup>().spacing = 8;
            ShowGroupTab(activeGroupTab);
        }

        private RectTransform Section(Transform parent, string name)
        {
            var section = Rect(name, parent, typeof(Image), typeof(VerticalLayoutGroup));
            var layout = section.GetComponent<VerticalLayoutGroup>();
            Vertical(layout); layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 8;
            sections.Add(section.GetComponent<Image>());
            return section;
        }

        private static void TimingColumns(RectTransform row, float height)
        {
            row.GetComponent<HorizontalLayoutGroup>().spacing = 6;
            Size(row, -1, height);
            float[] widths = { -1, TimingBpmWidth, TimingBeatsWidth, TimingApplyWidth, TimingRemoveWidth };
            for (int i = 0; i < widths.Length; i++) Size(row.GetChild(i), widths[i], height);
        }

        private ControlPage BuildControls(RectTransform parent, bool camera)
        {
            var page = new ControlPage { Camera = camera };
            var listSection = Section(parent, "事件列表分区");
            var scope = Row(listSection, "事件范围", ControlHeight);
            SmallLabel(scope, "显示范围", 132);
            page.Scope = MakeDropdown(scope, "Scope", new List<string> { "当前时间组", "全部时间组" }, _ => RefreshControls(page));
            var actions = Row(listSection, "事件操作", ControlHeight);
            Button(actions, "+ 当前时间", () => AddControl(page), 132);
            Spacer(actions);
            page.Duplicate = Quiet(Button(actions, "复制", () => DuplicateControl(page), 56));
            page.Delete = Quiet(Button(actions, "删除", () => DeleteControl(page), 56));
            page.Scroll = Scroll(listSection, "事件列表", out page.List, ControlHeight);
            page.Summary = MutedLabel(listSection, "", 32, 20);
            var form = Section(parent, "参数");
            page.Form = form.gameObject.AddComponent<CanvasGroup>();
            Label(form, camera ? "Camera 参数" : "SC 参数", 36, 24);
            var time = Row(form, "事件时间", 36);
            SmallLabel(time, "时间 (ms)", 132);
            page.Time = Input(time, "Time", -1);
            Quiet(Button(time, "当前", () => page.Time.text = Gameplay.ArcGameplayManager.Instance.ChartTiming.ToString(), 56));
            var group = Row(form, "事件归属", 36);
            SmallLabel(group, "归属时间组", 132);
            page.Group = MakeDropdown(group, "Group", new List<string> { "默认组" }, _ => { });
            if (camera)
            {
                page.Vectors = new TMP_InputField[6];
                for (int v = 0; v < 2; v++)
                {
                    Label(form, v == 0 ? "位移 · X / Y / Z" : "旋转 · X / Y / Z", 34, 24);
                    var row = Row(form, v == 0 ? "位移" : "旋转", 36);
                    for (int i = 0; i < 3; i++) page.Vectors[v * 3 + i] = Input(row, (v == 0 ? "Move" : "Rotate") + "XYZ"[i], -1);
                }
                var ease = Row(form, "缓动", 36); SmallLabel(ease, "缓动", 132);
                page.Type = MakeDropdown(ease, "Easing", new List<string> { "l · 线性", "qi · 缓入", "qo · 缓出", "reset · 重置", "s · 平滑" }, _ => { });
                page.Duration = Field(form, "时长 (ms)", "Duration");
            }
            else
            {
                var type = Row(form, "SC类型", 36); SmallLabel(type, "类型", 132);
                page.Type = MakeDropdown(type, "Type", ChartControlText.SceneTypes.Concat(new[] { "自定义" }).ToList(), _ => UpdateSceneType(page, true));
                page.CustomType = Field(form, "自定义类型", "CustomType");
                page.Parameters = Field(form, "参数", "Parameters");
                page.Hint = MutedLabel(form, "", 48, 20);
            }
            var apply = Row(form, "应用参数", 36); Spacer(apply);
            Button(apply, "应用参数", () => ApplyControl(page, false), 180);
            var raw = Disclosure(form, "AFF 语句");
            page.Statement = Input(raw, "Statement", -1, 84);
            page.Statement.lineType = TMP_InputField.LineType.MultiLineNewline;
            var rawActions = Row(raw, "语句操作", 34); Spacer(rawActions);
            Quiet(Button(rawActions, "应用语句", () => ApplyControl(page, true), 116));
            return page;
        }

        private RectTransform Disclosure(Transform parent, string title)
        {
            RectTransform content = null;
            var button = Quiet(Button(parent, title, () =>
            {
                bool show = !content.gameObject.activeSelf;
                content.gameObject.SetActive(show);
            }));
            var label = button.GetComponentInChildren<TMP_Text>(); label.alignment = TextAlignmentOptions.Left;
            AdeUiIcons.SetText(label, AdeUiIcons.Expand + "  " + title);
            button.onClick.AddListener(() => AdeUiIcons.SetText(label,
                (content.gameObject.activeSelf ? AdeUiIcons.Collapse : AdeUiIcons.Expand) + "  " + title));
            content = Rect(title, parent, typeof(VerticalLayoutGroup));
            Vertical(content.GetComponent<VerticalLayoutGroup>()); content.GetComponent<VerticalLayoutGroup>().spacing = 8;
            content.gameObject.SetActive(false);
            return content;
        }

        private TMP_Text MutedLabel(Transform parent, string text, float height = 34, int fontSize = 18)
        {
            var label = Label(parent, text, height, fontSize);
            mutedLabels.Add(label); label.color = Muted;
            return label;
        }

        private Button Quiet(Button button) { quietButtons.Add(button); Style(button); return button; }

        private RectTransform Rect(string name, Transform parent, params Type[] types)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            foreach (var type in types) go.AddComponent(type);
            return (RectTransform)go.transform;
        }
        private static void Fill(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
        private static void Vertical(VerticalLayoutGroup layout)
        {
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        }
        private static void Size(Transform transform, float width, float height, float flexWidth = 0, float flexHeight = 0)
        {
            var size = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
            // A flexible cell starts at zero; its text must not change the column width.
            size.minWidth = width < 0 ? 0 : width; size.preferredWidth = width < 0 ? 0 : width;
            size.minHeight = size.preferredHeight = height;
            size.flexibleWidth = width < 0 ? 1 : flexWidth; size.flexibleHeight = flexHeight;
        }
        private RectTransform Row(Transform parent, string name, float height)
        {
            var row = Rect(name, parent, typeof(HorizontalLayoutGroup));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft; layout.spacing = 8;
            Size(row, -1, Mathf.Max(ControlHeight, height));
            return row;
        }
        private void Spacer(Transform parent) => Size(Rect("Space", parent), -1, 0);
        private TMP_Text Label(Transform parent, string text, float height = 40, int fontSize = 20)
        {
            var rect = Rect(text.Length == 0 ? "Label" : text, parent, typeof(TextMeshProUGUI));
            var label = rect.GetComponent<TMP_Text>();
            label.font = font; label.fontSize = fontSize; label.text = text; label.color = Foreground;
            label.alignment = TextAlignmentOptions.Left; label.raycastTarget = false;
            label.richText = false; label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Truncate;
            Size(rect, -1, Mathf.Max(height, Mathf.Ceil(fontSize * 1.45f))); labels.Add(label);
            return label;
        }
        private void SmallLabel(Transform parent, string text, float width) => Size(Label(parent, text, 40, 24).transform, width, 40);
        private Button Button(Transform parent, string text, UnityAction action, float width = -1)
        {
            var rect = Rect(text, parent, typeof(Image), typeof(Button));
            var button = rect.GetComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var source = AdeTimingEditor.Instance.TimingPrefab.GetComponentInChildren<TMPro.TMP_InputField>(true);
            rect.GetComponent<Image>().sprite = ((Image)source.targetGraphic).sprite;
            rect.GetComponent<Image>().type = Image.Type.Sliced;
            button.onClick.AddListener(action); Size(rect, width, ControlHeight);
            var label = Label(rect, text); Fill(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            controls.Add(button); Style(button);
            return button;
        }
        private Button IconButton(Transform parent, string name, string resource, UnityAction action)
        {
            var button = Quiet(Button(parent, "", action, ControlHeight));
            button.name = name;
            var icon = Rect("Icon", button.transform, typeof(Image));
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.sizeDelta = new Vector2(24, 24);
            var image = icon.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("AffdataEdit/Icons/" + resource);
            image.raycastTarget = false;
            actionIcons.Add(button, image);
            Style(button);
            return button;
        }

        private TMP_DefaultControls.Resources ControlResources()
        {
            var source = AdeTimingEditor.Instance.TimingPrefab.GetComponentInChildren<TMPro.TMP_InputField>(true);
            var sprite = ((Image)source.targetGraphic).sprite;
            var old = AdeTimingEditor.Instance.CurrentTimingGroupDropdown;
            return new TMP_DefaultControls.Resources { standard = sprite, inputField = sprite,
                background = old.template.GetComponent<Image>().sprite,
                checkmark = Resources.Load<Sprite>("AffdataEdit/Icons/Checkmark"),
                dropdown = old.transform.Find("Arrow").GetComponent<Image>().sprite };
        }
        private TMP_InputField Input(Transform parent, string name, float width, float height = ControlHeight)
        {
            var root = TMP_DefaultControls.CreateInputField(ControlResources());
            root.transform.SetParent(parent, false); root.name = name;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = parent.gameObject.layer;
            var input = root.GetComponent<TMP_InputField>();
            input.fontAsset = font; input.pointSize = 22;
            AdeEditorText.Configure(input.textComponent, 22);
            ((TMP_Text)input.placeholder).text = "";
            input.textViewport.offsetMin = new Vector2(10, 0); input.textViewport.offsetMax = new Vector2(-10, 0);
            input.SetTextWithoutNotify(""); Size(root.transform, width, height);
            labels.Add(input.textComponent); controls.Add(input); Style(input);
            return input;
        }
        private TMP_InputField Field(Transform parent, string label, string name)
        { var row = Row(parent, label, ControlHeight); SmallLabel(row, label, 132); return Input(row, name, -1); }
        private TMP_Dropdown MakeDropdown(Transform parent, string name, List<string> options, UnityAction<int> action)
        {
            var root = TMP_DefaultControls.CreateDropdown(ControlResources());
            root.transform.SetParent(parent, false); root.name = name;
            // The popup gets its own Canvas; the editor camera must render its layer too.
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = parent.gameObject.layer;
            var dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.onValueChanged.AddListener(action); dropdown.ClearOptions(); dropdown.AddOptions(options);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            { AdeEditorText.Configure(text, AdeEditorText.BodySize); labels.Add(text); }
            var item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(item.sizeDelta.x, 44);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 220);
            Size(root.transform, -1, ControlHeight); controls.Add(dropdown); Style(dropdown);
            return dropdown;
        }
        private Toggle Check(Transform parent, string text, string name)
        {
            var row = Row(parent, name, 40);
            Label(row, text, ControlHeight, 24);
            var toggle = Instantiate(ArcadeComposeManager.Instance.PlaybackSyncToggle, row, false);
            toggle.name = name; toggle.onValueChanged = new Toggle.ToggleEvent(); toggle.group = null;
            Size(toggle.transform, 36, 36);
            ((Image)toggle.graphic).sprite = Resources.Load<Sprite>("AffdataEdit/Icons/Checkmark");
            controls.Add(toggle); Style(toggle);
            return toggle;
        }
        private ScrollRect Scroll(Transform parent, string name, out RectTransform content, float height = -1)
        {
            var root = Rect(name, parent, typeof(Image), typeof(ScrollRect));
            root.GetComponent<Image>().color = Color.clear;
            if (height >= 0) Size(root, -1, height);
            var viewport = Rect("Viewport", root, typeof(RectMask2D)); Fill(viewport);
            viewport.offsetMax = new Vector2(-ScrollGutter, 0);
            content = Rect("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); Vertical(layout); layout.spacing = height < 0 ? 12 : 4;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            var bar = Rect("Scrollbar", root, typeof(Image), typeof(Scrollbar));
            bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.pivot = Vector2.one;
            bar.sizeDelta = new Vector2(8, 0); bar.GetComponent<Image>().color = Color.clear;
            var handle = Rect("Handle", bar); Fill(handle);
            var thumb = Rect("Thumb", handle, typeof(Image)); Fill(thumb);
            thumb.offsetMin = new Vector2(3, 0); thumb.offsetMax = new Vector2(-2, 0);
            thumb.GetComponent<Image>().color = new Color(.45f, .45f, .45f, 1);
            var scrollbar = bar.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = thumb.GetComponent<Image>();
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        private static void SetOptions(TMP_Dropdown dropdown, List<string> names, int value)
        {
            dropdown.options = names.Select(x => new TMP_Dropdown.OptionData(x)).ToList();
            dropdown.SetValueWithoutNotify(Mathf.Max(0, value)); dropdown.RefreshShownValue();
        }
        private void Style(Selectable control)
        {
            if (!control) return;
            // Inputs, dropdowns and checkboxes use the same templates and theme as SettingsDrawer.
            if (control is Toggle toggle) toggle.graphic.color = AdeUiTheme.Dark ? Color.white : Foreground;
            var button = control as Button;
            if (!button || !quietButtons.Contains(button)) return;
            ((Image)button.targetGraphic).sprite = AdeUiTheme.RoundedSurface;
            ((Image)button.targetGraphic).type = Image.Type.Sliced;
            control.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock; colors.normalColor = colors.selectedColor = Color.clear;
            colors.fadeDuration = 0;
            colors.highlightedColor = AdeUiTheme.Current.Selection;
            colors.pressedColor = Color.Lerp(AdeUiTheme.Current.Selection, Foreground, .12f);
            colors.disabledColor = Color.clear;
            control.colors = colors;
            control.targetGraphic.color = Color.white;
            button.GetComponentInChildren<TMP_Text>().color = button.interactable ? Foreground : Muted;
            if (actionIcons.TryGetValue(button, out var icon))
                icon.color = button.interactable ? Foreground : new Color(Muted.r, Muted.g, Muted.b, .35f);
        }
        private void Highlight(Button button, bool selected)
        {
            Style(button); var colors = button.colors;
            if (selected) colors.normalColor = colors.selectedColor = AdeUiTheme.Current.Selection;
            button.colors = colors;
            button.GetComponentInChildren<TMP_Text>().color = button.interactable ? Foreground : Muted;
        }
        private void ApplyTheme()
        {
            themeRevision = AdeUiTheme.Revision;
            foreach (var background in backgrounds) if (background)
            {
                background.sprite = AdeUiTheme.PanelSurface;
                background.type = Image.Type.Sliced; background.color = Color.white;
            }
            foreach (var section in sections)
            {
                section.sprite = AdeUiTheme.ReadOnlySurface;
                section.type = Image.Type.Sliced; section.color = Color.white;
            }
            closeIcon.color = Foreground;
            labels.RemoveAll(x => !x); controls.RemoveAll(x => !x);
            foreach (var label in labels) label.color = Foreground;
            foreach (var label in mutedLabels) if (label) label.color = Muted;
            foreach (var control in controls) Style(control);
            AdeUiTheme.Refresh();
            UpdateHighlights();
            for (int i = 0; i < groupRows.Count; i++) if (groupRows[i].gameObject.activeSelf) Highlight(groupRows[i], i == groups.IndexOf(lastGroup));
            foreach (var page in controlPages)
                for (int i = 0; i < page.Events.Count; i++) Highlight(page.Rows[i], page.Events[i] == page.Selected);
        }
    }
}
