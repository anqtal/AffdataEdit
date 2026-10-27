using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeChartToolsPanel
    {
        // The scene owns the static view; runtime only binds actions and populates chart data.
        private void BindView()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var page = (Page)i;
                AdeUiKit.SetOnClick(tabs[i], () => Show(page));
                AdeUiKit.SetOnClick(entries[i], () => Toggle(page));
            }
            Bind(panel, "工具页签/Close", Collapse);
            Bind(emptyState, "打开谱面/打开谱面", () => AdeProjectManager.Instance.OpenProject());
            bottomGroup.onValueChanged.AddListener(SelectGroup);
            groupScroll.onValueChanged.AddListener(_ => ScrollGroups());
            Bind(groupCount.transform.parent, "+ 新建", AddGroup);
            Bind(deleteConfirmation, "确认操作/取消", () => deleteConfirmation.gameObject.SetActive(false));
            Bind(deleteConfirmation, "确认操作/删除组及内容", ConfirmDeleteGroup);
            for (int i = 0; i < groupTabs.Length; i++)
            {
                int tab = i;
                AdeUiKit.SetOnClick(groupTabs[i], () => ShowGroupTab(tab));
            }
            noInput.onValueChanged.AddListener(_ => ApplyGroup());
            fadingHolds.onValueChanged.AddListener(_ => ApplyGroup());
            noClip.onValueChanged.AddListener(_ => ApplyGroup());
            angleX.onEndEdit.AddListener(_ => ApplyGroup());
            angleY.onEndEdit.AddListener(_ => ApplyGroup());
            attributes.onEndEdit.AddListener(_ => ApplyAttributes());
            BindDisclosure(attributes.transform.parent, "属性文本");
            Bind(timingScroll.transform.parent, "Timing操作/+ 当前时间", AddTiming);
            foreach (var page in controlPages)
            {
                page.Scope.onValueChanged.AddListener(_ => RefreshControls(page));
                Bind(page.Scroll.transform.parent, "事件操作/+ 当前时间", () => AddControl(page));
                AdeUiKit.SetOnClick(page.Duplicate, () => DuplicateControl(page));
                AdeUiKit.SetOnClick(page.Delete, () => DeleteControl(page));
                Bind(page.Time.transform.parent, "当前", () => page.Time.text = Gameplay.ArcGameplayManager.Instance.ChartTiming.ToString());
                if (!page.Camera) page.Type.onValueChanged.AddListener(_ => UpdateSceneType(page, true));
                Bind(page.Form.transform, "应用参数/应用参数", () => ApplyControl(page, false));
                Bind(page.Statement.transform.parent, "语句操作/应用语句", () => ApplyControl(page, true));
                BindDisclosure(page.Statement.transform.parent, "AFF 语句");
            }
        }

        private static void Bind(Transform root, string path, UnityAction action)
            => AdeUiKit.SetOnClick(root.Find(path).GetComponent<Button>(), action);

        private static void BindDisclosure(Transform content, string title)
        {
            var button = content.parent.GetChild(content.GetSiblingIndex() - 1).GetComponent<Button>();
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            AdeUiKit.SetOnClick(button, () =>
            {
                bool show = !content.gameObject.activeSelf;
                content.gameObject.SetActive(show);
                AdeUiIcons.SetText(label, (show ? AdeUiIcons.Collapse : AdeUiIcons.Expand) + "  " + title);
            });
        }
    }
}
