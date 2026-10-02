using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcade.Compose.Command;
using Arcade.Compose.Editing;
using Arcade.Compose.MarkingMenu;
using Arcade.Compose.Operation;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Arcade.Compose.UI
{
    // Extra rows in the existing ValueEdit window. Time, time group and moving controls remain owned by AdeValueEditor.
    public sealed class AdeSlideEditor : MonoBehaviour, INoteSelectEvent
    {
        private readonly List<GameObject> rows = new List<GameObject>();
        private InputField startRange, endRange;
        private Dropdown leftCurve, rightCurve;
        private Toggle isFloor;
        private Image floorIntermediate;
        private AdeSlidePreview preview;
        private MarkingMenuItem menu;
        private bool ready;
        private static readonly string[] CurveNames = { "直线", "缓出（sin）", "缓入（cos）", "平滑（b）" };

        private void Start()
        {
            var editor = AdeValueEditor.Instance;
            if (!editor) return;
            startRange = RangeRow(editor.StartPos, "开始范围", true);
            endRange = RangeRow(editor.EndPos, "结束范围", false);
            leftCurve = CurveRow(editor.CurveType, "左侧曲线", true);
            rightCurve = CurveRow(editor.CurveType, "右侧曲线", false);
            var floorRow = Instantiate(editor.IsVoid, editor.IsVoid.parent); floorRow.name = "地面 Slide";
            floorRow.GetComponentsInChildren<Text>(true)[0].text = "地面 Slide";
            isFloor = floorRow.GetComponentInChildren<Toggle>(true);
            isFloor.onValueChanged = new Toggle.ToggleEvent();
            isFloor.onValueChanged.AddListener(value => Apply(note => note.IsFloor = value));
            floorIntermediate = floorRow.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == editor.IsVoidIntermediate.name);
            if (floorIntermediate) floorIntermediate.gameObject.SetActive(false);
            rows.Add(floorRow.gameObject);
            var shape = new GameObject("Slide shape preview", typeof(RectTransform), typeof(LayoutElement), typeof(AdeSlidePreview));
            shape.transform.SetParent(editor.Timing.parent, false);
            shape.GetComponent<LayoutElement>().preferredHeight = 112;
            shape.GetComponent<LayoutElement>().preferredWidth = editor.StartPos.rect.width;
            preview = shape.GetComponent<AdeSlidePreview>();
            preview.OnEdited = value => Apply(note =>
            {
                note.StartCenter = value.StartCenter; note.StartWidth = value.StartWidth;
                note.EndCenter = value.EndCenter; note.EndWidth = value.EndWidth;
            });
            rows.Add(shape);
            var help = Instantiate(editor.StartPos.GetComponentsInChildren<Text>(true)[0], editor.Timing.parent);
            help.name = "Slide range help";
            help.text = "范围：中心,宽度；X：-0.5～1.5\n竖线随 X 网格设置；拖动四角自动吸附";
            help.fontSize = 14; help.alignment = TextAnchor.MiddleLeft;
            help.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            rows.Add(help.gameObject);
            // Place additional properties before the shared timing-group row.
            foreach (var row in rows) row.transform.SetSiblingIndex(editor.TimingGroup.GetSiblingIndex());
            var create = AdeClickToCreate.Instance;
            if (create.ClickToCreateItems.Length > 0)
            {
                menu = Instantiate(create.ClickToCreateItems[0], create.ClickToCreateItems[0].transform.parent);
                menu.StartupText = "Slide 区域"; menu.HasSubMenu = false; menu.SubItems = new MarkingMenuItem[0];
                menu.OnConfirmed = new UnityEvent(); menu.OnConfirmed.AddListener(() =>
                {
                    create.Enable = true; create.SetClickToCreateMode(ClickToCreateMode.Slide);
                });
                menu.OnHangOver = new UnityEvent(); menu.gameObject.SetActive(false);
                create.ClickToCreateItems = create.ClickToCreateItems.Concat(new[] { menu }).ToArray();
            }
            AdeSelectionManager.Instance.NoteEventListeners.Add(this);
            AdeCommandManager.Instance.onCommandExecuted += OnCommand;
            ready = true; Refresh();
        }

        private InputField RangeRow(RectTransform template, string label, bool start)
        {
            var row = Instantiate(template, template.parent); row.name = label;
            row.GetComponentsInChildren<Text>(true)[0].text = label;
            foreach (var button in row.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
            var input = row.GetComponentInChildren<InputField>(true);
            input.onEndEdit = new InputField.EndEditEvent(); input.onValueChanged = new InputField.OnChangeEvent();
            input.contentType = InputField.ContentType.Standard;
            input.onEndEdit.AddListener(text =>
            {
                var parts = text.Split(','); float center, width;
                if (parts.Length != 2 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out center)
                    || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out width))
                { Invalid(); return; }
                Apply(note =>
                {
                    if (start) { note.StartCenter = center; note.StartWidth = width; }
                    else { note.EndCenter = center; note.EndWidth = width; }
                });
            });
            rows.Add(row.gameObject); return input;
        }
        private Dropdown CurveRow(RectTransform template, string label, bool left)
        {
            var row = Instantiate(template, template.parent); row.name = label;
            row.GetComponentsInChildren<Text>(true)[0].text = label;
            var dropdown = row.GetComponentInChildren<Dropdown>(true);
            dropdown.onValueChanged = new Dropdown.DropdownEvent(); dropdown.ClearOptions();
            dropdown.AddOptions(CurveNames.ToList());
            dropdown.onValueChanged.AddListener(value => Apply(note =>
            {
                if (left) note.LeftCurve = value; else note.RightCurve = value;
            }));
            rows.Add(row.gameObject); return dropdown;
        }
        private void Apply(Action<ArcSlide> edit)
        {
            var selected = AdeSelectionManager.Instance.SelectedNotes;
            if (selected.Count == 0 || selected.Any(n => !(n is ArcSlide))) return;
            var commands = new List<ICommand>();
            foreach (var note in selected)
            {
                var value = (ArcSlide)note.Clone(); edit(value);
                if (!value.IsValid) { Invalid(); return; }
                commands.Add(new EditArcEventCommand(note, value));
            }
            AdeCommandManager.Instance.Add(new BatchCommand(commands.ToArray(), "修改 Slide 范围"));
        }
        private void Invalid()
        {
            AdeToast.Instance.Show("Slide 左右边界须在 -0.5～1.5、宽度大于 0，左右边界不可交叉"); Refresh();
        }
        private void Refresh()
        {
            if (!ready) return;
            var selected = AdeSelectionManager.Instance.SelectedNotes;
            bool active = selected.Count > 0 && selected.All(n => n is ArcSlide);
            foreach (var row in rows) row.SetActive(active);
            if (!active) return;
            var note = (ArcSlide)selected[0];
            isFloor.SetIsOnWithoutNotify(note.IsFloor);
            if (floorIntermediate) floorIntermediate.gameObject.SetActive(selected.Cast<ArcSlide>().Any(n => n.IsFloor != note.IsFloor));
            startRange.SetTextWithoutNotify(selected.Cast<ArcSlide>().All(n => n.StartCenter == note.StartCenter && n.StartWidth == note.StartWidth)
                ? Range(note.StartCenter, note.StartWidth) : "-,-");
            endRange.SetTextWithoutNotify(selected.Cast<ArcSlide>().All(n => n.EndCenter == note.EndCenter && n.EndWidth == note.EndWidth)
                ? Range(note.EndCenter, note.EndWidth) : "-,-");
            leftCurve.SetValueWithoutNotify(note.LeftCurve); rightCurve.SetValueWithoutNotify(note.RightCurve);
            if (selected.Cast<ArcSlide>().Any(n => n.LeftCurve != note.LeftCurve)) leftCurve.captionText.text = "混合";
            if (selected.Cast<ArcSlide>().Any(n => n.RightCurve != note.RightCurve)) rightCurve.captionText.text = "混合";
            preview.gameObject.SetActive(selected.Count == 1);
            preview.Note = (ArcSlide)note.Clone(); preview.SetVerticesDirty();
        }
        private static string Range(float center, float width) => center.ToString("R", CultureInfo.InvariantCulture) + "," + width.ToString("R", CultureInfo.InvariantCulture);
        private void OnCommand(ICommand command, bool undo) => Refresh();
        public void OnNoteSelect(ArcNote note) => Refresh();
        public void OnNoteDeselect(ArcNote note) => Refresh();
        public void OnNoteDeselectAll() => Refresh();
        private void OnDestroy()
        {
            if (AdeSelectionManager.Instance) AdeSelectionManager.Instance.NoteEventListeners.Remove(this);
            if (AdeCommandManager.Instance) AdeCommandManager.Instance.onCommandExecuted -= OnCommand;
            foreach (var row in rows) if (row) Destroy(row);
            if (menu) Destroy(menu.gameObject);
        }
    }
}
