using TMPro;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcade.Aff;
using Arcade.Compose.Command;
using Arcade.Compose.Editing;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeChartToolsPanel
    {
        private sealed class TimingRow
        {
            public RectTransform Root;
            public ArcTiming Timing;
            public TMP_InputField Time, Bpm, Beats;
            public Button Remove;
        }

        private bool Loaded()
        {
            if (ArcGameplayManager.Instance.Chart != null) return true;
            Message("请先打开谱面。", true);
            return false;
        }

        private void AddGroup()
        {
            if (!Loaded()) return;
            AdeTimingEditor.Instance.AddTimingGroup();
            dirty = true;
            Message("已添加时间组，继承当前组的 Timing。可撤销。");
        }

        private int GroupEventCount(ArcTimingGroup group)
        {
            var chart = ArcGameplayManager.Instance.Chart;
            if (chart == null) return 0;
            return chart.Taps.Count(x => x.TimingGroup == group) + chart.Holds.Count(x => x.TimingGroup == group)
                + chart.Arcs.Count(x => x.TimingGroup == group) + chart.Slides.Count(x => x.TimingGroup == group)
                + chart.Cameras.Count(x => x.TimingGroup == group) + chart.SceneControl.Count(x => x.TimingGroup == group);
        }

        private void DeleteGroup()
        {
            if (!Loaded() || lastGroup == null) return;
            pendingDelete = lastGroup;
            deletePrompt.text = $"删除 {GroupName(pendingDelete)} 及组内 {GroupEventCount(pendingDelete)} 个音符 / 事件？\nTiming 也会删除，操作可撤销。";
            deleteConfirmation.gameObject.SetActive(true);
        }

        private void ConfirmDeleteGroup()
        {
            if (pendingDelete == null || !groups.Contains(pendingDelete)) return;
            AdeCommandManager.Instance.Add(new RemoveTimingGroup(pendingDelete));
            deleteConfirmation.gameObject.SetActive(false);
            pendingDelete = null;
            Message("时间组及组内内容已删除。可撤销。");
        }

        private void RefreshGroups()
        {
            bool loaded = lastChart != null;
            while (groupRows.Count < groups.Count)
            {
                int index = groupRows.Count;
                var button = Quiet(Button(groupList, "", () => SelectGroup(index)));
                button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                button.GetComponentInChildren<TMP_Text>().fontSize = 24;
                groupRows.Add(button);
            }
            for (int i = 0; i < groupRows.Count; i++)
            {
                groupRows[i].gameObject.SetActive(i < groups.Count);
                if (i >= groups.Count) continue;
                var group = groups[i];
                groupRows[i].GetComponentInChildren<TMP_Text>().text = GroupName(group)
                    + (group == null ? "    · 基础" : "    · " + GroupEventCount(group) + " 个音符 / 事件");
                groupRows[i].interactable = loaded;
                Highlight(groupRows[i], group == lastGroup);
            }
            bool editable = loaded && lastGroup != null;
            groupCount.text = "时间组  ·  " + groups.Count;
            Size(groupScroll.transform, -1, Mathf.Min(groups.Count, 4) * 44 - 4);
            deleteGroup.interactable = editable;
            groupProperties.gameObject.SetActive(editable);
            defaultGroupHint.gameObject.SetActive(!editable);
            groupSummary.text = GroupName(lastGroup);
            noInput.SetIsOnWithoutNotify(lastGroup?.NoInput ?? false);
            fadingHolds.SetIsOnWithoutNotify(lastGroup?.FadingHolds ?? false);
            noClip.SetIsOnWithoutNotify(lastGroup?.NoClip ?? false);
            angleX.SetTextWithoutNotify(((lastGroup?.AngleX ?? 0) / 10f).ToString("0.#", CultureInfo.InvariantCulture));
            angleY.SetTextWithoutNotify(((lastGroup?.AngleY ?? 0) / 10f).ToString("0.#", CultureInfo.InvariantCulture));
            attributes.SetTextWithoutNotify(lastGroup?.Attributes ?? "");
            if (pendingDelete != lastGroup) { pendingDelete = null; deleteConfirmation.gameObject.SetActive(false); }
            RefreshTimings();
        }

        private void ApplyGroup()
        {
            if (!Loaded() || lastGroup == null) return;
            if (!ChartControlText.Float(angleX.text, out float x) || !ChartControlText.Float(angleY.text, out float y))
            { Message("角度必须是有限数值，单位为度。", true); return; }
            int ax = Mathf.RoundToInt((x % 360 + 360) % 360 * 10) % 3600;
            int ay = Mathf.RoundToInt((y % 360 + 360) % 360 * 10) % 3600;
            var tokens = lastGroup.Attributes.Split('_').Where(t => t.Length > 0 && t != "noinput" && t != "fadingholds"
                && t != "noclip" && !t.StartsWith("anglex", StringComparison.Ordinal) && !t.StartsWith("angley", StringComparison.Ordinal)).ToList();
            if (noInput.isOn) tokens.Add("noinput");
            if (fadingHolds.isOn) tokens.Add("fadingholds");
            if (noClip.isOn) tokens.Add("noclip");
            if (ax != 0) tokens.Add("anglex" + ax);
            if (ay != 0) tokens.Add("angley" + ay);
            SetAttributes(string.Join("_", tokens));
        }

        private void ApplyAttributes()
        {
            if (!Loaded() || lastGroup == null) return;
            SetAttributes(attributes.text.Trim());
        }

        private void SetAttributes(string text)
        {
            if (!ChartControlText.ValidateAttributes(text, out string error)) { Message(error, true); return; }
            if (text != lastGroup.Attributes) AdeCommandManager.Instance.Add(new EditTimingGroupAttributes(lastGroup, text));
            dirty = true;
            Message("组属性已应用。未识别的扩展属性按原文保留。");
        }

        private void AddTiming()
        {
            if (!Loaded()) return;
            var timing = new ArcTiming { Timing = ArcGameplayManager.Instance.ChartTiming,
                Bpm = ArcTimingManager.Instance.CalculateBpmByTiming(ArcGameplayManager.Instance.ChartTiming, lastGroup), BeatsPerLine = 4 };
            AdeCommandManager.Instance.Add(new AddTimingEvent(lastGroup, timing));
            Message("Timing 已添加。可撤销。");
        }

        private void RefreshTimings()
        {
            var timings = ArcTimingManager.Instance.GetTiming(lastGroup);
            int count = lastChart == null ? 0 : timings.Count;
            while (timingRows.Count < count)
            {
                var row = new TimingRow();
                row.Root = Row(timingList, "Timing", 36);
                row.Time = Input(row.Root, "Time", -1); row.Bpm = Input(row.Root, "BPM", -1); row.Beats = Input(row.Root, "Beats", 58);
                Quiet(Button(row.Root, "应用", () => ApplyTiming(row), 56));
                row.Remove = Quiet(Button(row.Root, "Remove", () =>
                {
                    if (ArcTimingManager.Instance.GetTiming(lastGroup).Count > 1)
                        AdeCommandManager.Instance.Add(new RemoveTimingEvent(lastGroup, row.Timing));
                }, 28));
                AdeUiIcons.SetText(row.Remove.GetComponentInChildren<TMP_Text>(), AdeUiIcons.Close);
                timingRows.Add(row);
            }
            for (int i = 0; i < timingRows.Count; i++)
            {
                var row = timingRows[i]; row.Root.gameObject.SetActive(i < count);
                if (i >= count) continue;
                row.Timing = timings[i];
                row.Time.SetTextWithoutNotify(row.Timing.Timing.ToString());
                row.Bpm.SetTextWithoutNotify(ChartControlText.Number(row.Timing.Bpm));
                row.Beats.SetTextWithoutNotify(ChartControlText.Number(row.Timing.BeatsPerLine));
                row.Remove.interactable = count > 1;
            }
            Size(timingScroll.transform, -1, Mathf.Clamp(count, 1, 4) * 50 - 4);
        }

        private void ApplyTiming(TimingRow row)
        {
            if (!ChartControlText.Integer(row.Time.text, out int time) || !ChartControlText.Float(row.Bpm.text, out float bpm)
                || !ChartControlText.Float(row.Beats.text, out float beats) || beats < 0 || Mathf.Abs(bpm) > 1000000)
            { Message("Timing 需要整数时间、有效 BPM（±1000000 内）和非负拍数。", true); return; }
            var next = (ArcTiming)row.Timing.Clone(); next.Timing = time; next.Bpm = bpm; next.BeatsPerLine = beats;
            AdeCommandManager.Instance.Add(new EditTimingEvent(lastGroup, row.Timing, next));
            Message("Timing 已应用。可撤销。");
        }

        private void RefreshControls(ControlPage page)
        {
            IEnumerable<ArcEvent> events = page.Camera ? ArcCameraManager.Instance.Cameras.Cast<ArcEvent>() : ArcSceneControlManager.Instance.SceneControls;
            page.Events = lastChart == null ? new List<ArcEvent>() : events
                .Where(x => page.Scope.value == 1 || ((IHasTimingGroup)x).TimingGroup == lastGroup).OrderBy(x => x.Timing).ToList();
            if (!page.Events.Contains(page.Selected)) page.Selected = page.Events.FirstOrDefault();
            while (page.Rows.Count < page.Events.Count)
            {
                int index = page.Rows.Count;
                var button = Quiet(Button(page.List, "", () =>
                {
                    page.Selected = page.Events[index];
                    FillControl(page); HighlightControlRows(page); ClearMessage();
                }));
                button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                page.Rows.Add(button);
            }
            for (int i = 0; i < page.Rows.Count; i++)
            {
                page.Rows[i].gameObject.SetActive(i < page.Events.Count);
                if (i >= page.Events.Count) continue;
                var e = page.Events[i];
                string name = e is ArcCamera c ? $"Camera · {c.Duration} ms · {c.CameraType.ToString().ToLowerInvariant()}" : ((RawAffSceneControl)((ArcSceneControl)e).IntoRawItem()).Type;
                page.Rows[i].GetComponentInChildren<TMP_Text>().text = $"{e.Timing} ms    {name}    {GroupName(((IHasTimingGroup)e).TimingGroup)}";
            }
            page.Scroll.gameObject.SetActive(page.Events.Count > 0);
            Size(page.Scroll.transform, -1, Mathf.Clamp(page.Events.Count, 1, 4) * 44 - 4);
            page.Summary.text = page.Events.Count == 0 ? "当前范围暂无事件，点击「+ 当前时间」添加。" : $"{page.Events.Count} 个事件";
            FillControl(page); HighlightControlRows(page);
        }

        private void HighlightControlRows(ControlPage page)
        {
            for (int i = 0; i < page.Events.Count; i++) Highlight(page.Rows[i], page.Selected == page.Events[i]);
        }

        private void FillControl(ControlPage page)
        {
            bool selected = page.Selected != null;
            page.Form.gameObject.SetActive(selected);
            page.Delete.interactable = page.Duplicate.interactable = selected;
            if (!selected) { page.Statement.SetTextWithoutNotify(""); return; }
            page.Time.SetTextWithoutNotify(page.Selected.Timing.ToString());
            page.Group.SetValueWithoutNotify(Mathf.Max(0, groups.IndexOf(((IHasTimingGroup)page.Selected).TimingGroup)));
            page.Group.RefreshShownValue();
            if (page.Selected is ArcCamera camera)
            {
                for (int i = 0; i < 3; i++)
                {
                    page.Vectors[i].SetTextWithoutNotify(ChartControlText.Number(camera.Move[i]));
                    page.Vectors[3 + i].SetTextWithoutNotify(ChartControlText.Number(camera.Rotate[i]));
                }
                page.Type.SetValueWithoutNotify((int)camera.CameraType); page.Type.RefreshShownValue();
                page.Duration.SetTextWithoutNotify(camera.Duration.ToString());
            }
            else
            {
                var raw = (RawAffSceneControl)((ArcSceneControl)page.Selected).IntoRawItem();
                int index = Array.IndexOf(ChartControlText.SceneTypes, raw.Type);
                page.Type.SetValueWithoutNotify(index < 0 ? ChartControlText.SceneTypes.Length : index); page.Type.RefreshShownValue();
                page.CustomType.SetTextWithoutNotify(raw.Type);
                page.Parameters.SetTextWithoutNotify(string.Join(",", raw.Params.Select(x => x is RawAffInt i ? i.data.ToString()
                    : x is RawAffFloat f ? ChartControlText.Number(f.data) : ((RawAffWord)x).data)));
                UpdateSceneType(page, false);
            }
            page.Statement.SetTextWithoutNotify(ChartControlText.Format(page.Selected));
        }

        private void UpdateSceneType(ControlPage page, bool resetParameters)
        {
            bool custom = page.Type.value == ChartControlText.SceneTypes.Length;
            page.CustomType.transform.parent.gameObject.SetActive(custom);
            string type = custom ? page.CustomType.text : ChartControlText.SceneTypes[page.Type.value];
            bool noArgs = type == "trackhide" || type == "trackshow";
            page.Parameters.interactable = !noArgs;
            page.Parameters.transform.parent.gameObject.SetActive(!noArgs);
            page.Hint.text = custom ? "扩展 SC 按原文保存；预览效果取决于当前支持。"
                : noArgs ? "此类型不需要参数。" : type == "hidegroup" ? "参数：0.00,开关（0 显示 / 1 隐藏）"
                : type == "enwidencamera" || type == "enwidenlanes" ? "参数：时长（ms）,开关（0 / 1）"
                : "参数：时长（秒）,整数值";
            if (resetParameters && !custom) page.Parameters.text = noArgs ? "" : type == "hidegroup" ? "0.00,1"
                : type == "enwidencamera" || type == "enwidenlanes" ? "1000.00,1" : type == "redline" ? "1.00,0"
                : type == "trackdisplay" ? "0.50,128" : "0.50,255";
        }

        private void AddControl(ControlPage page)
        {
            if (!Loaded()) return;
            int time = ArcGameplayManager.Instance.ChartTiming;
            ArcEvent value = page.Camera ? (ArcEvent)new ArcCamera { Timing = time, TimingGroup = lastGroup, Duration = 1000 }
                : new ArcSceneControl(new RawAffSceneControl { Timing = time, Type = "trackhide", Params = new List<IRawAffValue>() }, lastGroup);
            AdeCommandManager.Instance.Add(new ChartControlCommand(value, null, value));
            page.Selected = value; dirty = true;
            Message("事件已添加。修改参数后点击应用。");
        }

        private void DuplicateControl(ControlPage page)
        {
            if (!Loaded() || page.Selected == null) return;
            var value = page.Selected.Clone(); value.Timing = ArcGameplayManager.Instance.ChartTiming;
            AdeCommandManager.Instance.Add(new ChartControlCommand(value, null, value));
            page.Selected = value; dirty = true;
            Message("已复制到当前时间。可撤销。");
        }

        private void DeleteControl(ControlPage page)
        {
            if (!Loaded() || page.Selected == null) return;
            AdeCommandManager.Instance.Add(new ChartControlCommand(page.Selected, page.Selected, null));
            Message("事件已删除。可撤销。");
        }

        private void ApplyControl(ControlPage page, bool statement)
        {
            if (!Loaded() || page.Selected == null) return;
            string text = page.Statement.text;
            if (!statement)
            {
                if (page.Camera)
                    text = $"camera({page.Time.text},{string.Join(",", page.Vectors.Select(x => x.text))},{ChartControlText.CameraTypes[page.Type.value]},{page.Duration.text});";
                else
                {
                    string type = page.Type.value < ChartControlText.SceneTypes.Length ? ChartControlText.SceneTypes[page.Type.value] : page.CustomType.text.Trim();
                    string args = page.Parameters.interactable ? page.Parameters.text.Trim() : "";
                    text = $"scenecontrol({page.Time.text},{type}{(args.Length == 0 ? "" : "," + args)});";
                }
            }
            if (!ChartControlText.TryParse(text, page.Camera, groups[page.Group.value], out var next, out string error))
            { Message(error, true); return; }
            AdeCommandManager.Instance.Add(new ChartControlCommand(page.Selected, page.Selected, next));
            Message("修改已应用。可撤销。");
        }
    }
}
