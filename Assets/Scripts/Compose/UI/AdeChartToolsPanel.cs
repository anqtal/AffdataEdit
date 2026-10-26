using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Arcade.Compose.Command;
using Arcade.Compose.Editing;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LitMotion;

namespace Arcade.Compose
{
    public sealed partial class AdeChartToolsPanel : MonoBehaviour
    {
        public enum Page { Groups, SceneControl, Camera }
        public static AdeChartToolsPanel Instance { get; private set; }
        private static readonly string[] Titles = { "时间组", "SC", "Camera" };
        private readonly RectTransform[] pages = new RectTransform[3];
        private readonly Button[] tabs = new Button[3];
        private readonly Button[] entries = new Button[3];
        private readonly List<ArcTimingGroup> groups = new List<ArcTimingGroup>();
        private readonly List<Button> groupRows = new List<Button>();
        private readonly List<TMP_Text> groupDetails = new List<TMP_Text>();
        private readonly List<Button> groupCopies = new List<Button>(), groupDeletes = new List<Button>();
        private readonly Button[] groupTabs = new Button[2];
        private readonly RectTransform[] groupSections = new RectTransform[2];
        private int activeGroupTab;
        private readonly List<TimingRow> timingRows = new List<TimingRow>();
        private RectTransform panel, pageHost, emptyState, groupList, timingList;
        private ScrollRect groupScroll, timingScroll;
        private TMP_Dropdown bottomGroup;
        private TMP_Text context, status, groupCount, defaultGroupHint;
        private Toggle noInput, fadingHolds, noClip;
        private TMP_InputField angleX, angleY, attributes;
        private CanvasGroup groupProperties;
        private RectTransform deleteConfirmation;
        private TMP_Text deletePrompt;
        private ArcTimingGroup pendingDelete;
        private readonly ControlPage[] controlPages = new ControlPage[2];
        private Page activePage;
        private ArcTimingGroup lastGroup;
        private ArcChart lastChart;
        private bool dirty = true, ready;
        private int themeRevision = -1;
        private TMP_FontAsset font;
        private CanvasGroup panelGroup;
        private MotionHandle panelMotion;
        private bool expanded;

        private sealed class ControlPage
        {
            public bool Camera;
            public RectTransform List;
            public ScrollRect Scroll;
            public readonly List<Button> Rows = new List<Button>();
            public readonly List<TMP_Text> Descriptions = new List<TMP_Text>();
            public readonly List<TMP_Text> Groups = new List<TMP_Text>();
            public List<ArcEvent> Events = new List<ArcEvent>();
            public ArcEvent Selected;
            public TMP_Dropdown Scope, Group, Type;
            public TMP_InputField Time, Duration, CustomType, Parameters, Statement;
            public TMP_InputField[] Vectors;
            public TMP_Text Hint, Summary;
            public CanvasGroup Form;
            public Button Delete, Duplicate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (scene.name == "ArcEditor") new GameObject(nameof(AdeChartToolsPanel)).AddComponent<AdeChartToolsPanel>();
            };
        }

        private void Start()
        {
            Instance = this;
            var timing = AdeTimingEditor.Instance;
            font = AdeEditorText.Font;
            BuildPanel();
            BuildBottomBar();
            BuildGroups(pages[0]);
            controlPages[0] = BuildControls(pages[1], false);
            controlPages[1] = BuildControls(pages[2], true);
            timing.View.SetActive(false);
            var timingEntry = ArcadeComposeManager.Instance.RightBarView.Find("Timing");
            if (timingEntry)
            {
                timingEntry.gameObject.SetActive(true);
                timingEntry.SetAsFirstSibling();
                AdeUiKit.SetOnClick(timingEntry.GetComponent<Button>(), () => Toggle(Page.Groups));
            }
            ArcGameplayManager.Instance.OnChartLoad.AddListener(OnChartLoad);
            AdeCommandManager.Instance.onCommandExecuted += OnCommand;
            ready = true;
            Refresh();
            panel.gameObject.SetActive(false);
            ApplyTheme();
        }

        private void OnDestroy()
        {
            panelMotion.TryCancel();
            if (ArcGameplayManager.Instance) ArcGameplayManager.Instance.OnChartLoad.RemoveListener(OnChartLoad);
            if (AdeCommandManager.Instance) AdeCommandManager.Instance.onCommandExecuted -= OnCommand;
            if (Instance == this) Instance = null;
        }

        private void OnChartLoad()
        {
            foreach (var page in controlPages) page.Selected = null;
            dirty = true;
        }
        private void OnCommand(ICommand command, bool undo) => dirty = true;

        private void LateUpdate()
        {
            if (!ready) return;
            if (lastChart != ArcGameplayManager.Instance.Chart || lastGroup != AdeTimingEditor.Instance.currentTimingGroup) dirty = true;
            if (dirty) Refresh();
            if (themeRevision != AdeUiTheme.Revision) ApplyTheme();
            if (!ArcadeComposeManager.Instance.IsEditorMode && expanded) Collapse();
        }

        public void Toggle(Page page)
        {
            if (expanded && page == activePage) Collapse();
            else Show(page);
        }

        public void Show(Page page)
        {
            if (!ready) return;
            bool opening = !expanded;
            activePage = page;
            if (!panel.gameObject.activeSelf) AdeDialogManager.Pose(panelGroup, panel, 0);
            expanded = true;
            panelGroup.blocksRaycasts = true;
            panel.gameObject.SetActive(true);
            for (int i = 0; i < pages.Length; i++) pages[i].parent.parent.gameObject.SetActive(i == (int)page);
            ClearMessage();
            if (dirty) Refresh();
            else UpdateHighlights();
            if (opening)
            {
                panelMotion.TryCancel();
                panelMotion = AdeDialogManager.AnimateView(panelGroup, panel, panelGroup.alpha, 1, () => { });
            }
        }

        public void Collapse()
        {
            if (!expanded) return;
            expanded = false;
            panelGroup.blocksRaycasts = false;
            panelMotion.TryCancel();
            panelMotion = AdeDialogManager.AnimateView(panelGroup, panel, panelGroup.alpha, 0,
                () => panel.gameObject.SetActive(false));
            UpdateHighlights();
        }

        private void Refresh()
        {
            dirty = false;
            lastChart = ArcGameplayManager.Instance.Chart;
            lastGroup = AdeTimingEditor.Instance.currentTimingGroup;
            groups.Clear(); groups.Add(null); groups.AddRange(ArcTimingManager.Instance.timingGroups);
            var names = groups.Select(GroupName).ToList();
            SetOptions(bottomGroup, names, groups.IndexOf(lastGroup));
            bottomGroup.interactable = lastChart != null;
            bool loaded = lastChart != null;
            pageHost.gameObject.SetActive(loaded);
            emptyState.gameObject.SetActive(!loaded);
            context.gameObject.SetActive(loaded);
            context.text = "当前时间组：" + GroupName(lastGroup);
            RefreshGroups();
            foreach (var page in controlPages)
            {
                SetOptions(page.Group, names, page.Selected is IHasTimingGroup e ? groups.IndexOf(e.TimingGroup) : groups.IndexOf(lastGroup));
                RefreshControls(page);
            }
            UpdateHighlights();
        }

        private static string GroupName(ArcTimingGroup group) => group == null ? "默认组" : "组 " + group.Id;
        private void SelectGroup(int index)
        {
            if (index < 0 || index >= groups.Count || lastChart == null) return;
            AdeTimingEditor.Instance.SetCurrentTimingGroup(groups[index]);
            ClearMessage();
            dirty = true;
        }

        private void UpdateHighlights()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                Highlight(tabs[i], i == (int)activePage);
                Highlight(entries[i], expanded && i == (int)activePage);
            }
            for (int i = 0; i < groupTabs.Length; i++) Highlight(groupTabs[i], i == activeGroupTab);
        }

        private void ShowGroupTab(int index)
        {
            activeGroupTab = index;
            for (int i = 0; i < groupSections.Length; i++) groupSections[i].gameObject.SetActive(i == index);
            UpdateHighlights();
            ClearMessage();
        }

        private void Message(string text, bool error = false)
        {
            status.text = text;
            status.gameObject.SetActive(text.Length > 0);
            status.color = error ? new Color32(232, 113, 113, 255) : Foreground;
        }
        private void ClearMessage() => Message("");
    }
}
