using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

namespace Arcade.Compose.Feature
{
    public partial class AdeObsManager
    {
        private Text recordingStatus, recordingHint;

        private void ConfigureRecordingDialog()
        {
            if (recordingStatus)
            {
                UpdateRecordingView();
                return;
            }
            ((RectTransform)OBSDialog.View.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 920);
            AdeUiKit.SetMaxHeight(OBSDialog, 640);
            var content = AdeUiKit.Content(OBSDialog);
            var form = content.GetComponent<VerticalLayoutGroup>();
            form.padding = new RectOffset(32, 32, 24, 28);
            form.spacing = 18;
            form.childControlWidth = form.childControlHeight = true;
            form.childForceExpandWidth = true;
            form.childForceExpandHeight = false;
            var template = ObsServerIpInput.transform.parent.Find("Text").GetComponent<Text>();
            var fields = ObsServerIpInput.transform.parent.parent;
            var fieldsLayout = fields.GetComponent<VerticalLayoutGroup>();
            fieldsLayout.padding = new RectOffset();
            fieldsLayout.spacing = 16;
            fieldsLayout.childControlWidth = fieldsLayout.childControlHeight = true;
            fieldsLayout.childForceExpandWidth = true;
            fieldsLayout.childForceExpandHeight = false;

            foreach (Transform row in fields)
            {
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                if (!layout) continue;
                layout.padding = new RectOffset();
                layout.spacing = 16;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var label = row.Find("Text")?.GetComponent<Text>();
                if (label)
                {
                    label.fontSize = 22;
                    label.alignment = TMPro.TextAlignmentOptions.Left;
                    var labelSize = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
                    labelSize.minWidth = labelSize.preferredWidth = 156;
                    labelSize.flexibleWidth = 0;
                }
                else layout.padding.left = 172;
                Transform value = row.GetChild(row.childCount - 1);
                var valueSize = value.GetComponent<LayoutElement>() ?? value.gameObject.AddComponent<LayoutElement>();
                valueSize.minWidth = valueSize.preferredWidth = 0;
                valueSize.flexibleWidth = 1;
                valueSize.minHeight = valueSize.preferredHeight = 50;
            }
            foreach (InputField input in new[] { ObsServerIpInput, ObsServerPortInput, ObsServerPasswordInput })
            {
                input.textComponent.fontSize = 24;
                input.textViewport.offsetMin = new Vector2(14, 0);
                input.textViewport.offsetMax = new Vector2(-14, 0);
                input.textComponent.rectTransform.offsetMin = input.textComponent.rectTransform.offsetMax = Vector2.zero;
                var size = input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 0;
                size.flexibleWidth = 1;
                size.minHeight = size.preferredHeight = 50;
            }
            var downloadRow = fields.Find("Download");
            var connectionRow = ConnectionButton.transform.parent;
            var actions = new GameObject("ConnectionActions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            actions.transform.SetParent(fields, false);
            actions.GetComponent<LayoutElement>().preferredHeight = 46;
            var actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
            actionsLayout.padding.left = 172;
            actionsLayout.spacing = 12;
            actionsLayout.childAlignment = TextAnchor.MiddleLeft;
            actionsLayout.childControlWidth = actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = actionsLayout.childForceExpandHeight = false;
            var download = Instantiate(downloadRow.GetComponentInChildren<Button>(true), actions.transform, false);
            AdeUiKit.SetOnClick(download, OpenOBSWebsite);
            SizeConnectionButton(download, 156);
            ConnectionButton = Instantiate(ConnectionButton, actions.transform, false);
            ConnectionButtonLabel = ConnectionButton.GetComponentInChildren<Text>(true);
            AdeUiKit.SetOnClick(ConnectionButton, OnConnectionButton);
            SizeConnectionButton(ConnectionButton, 184);
            // Keep the original Download row intact: cloud dialogs use it as their button template.
            downloadRow.gameObject.SetActive(false);
            connectionRow.gameObject.SetActive(false);
            recordingStatus = RecordingLabel(content, template, "OBS 未连接", 26, 40);
            recordingStatus.fontStyle = TMPro.FontStyles.Bold;
            recordingStatus.transform.SetAsFirstSibling();
            var introduction = RecordingLabel(content, template, "连接 OBS，同步录制谱面播放。", 22, 34);
            introduction.transform.SetSiblingIndex(1);
            var guide = RecordingLabel(content, template, "在 OBS 的工具菜单中启用 WebSocket 服务器，并填写下方连接信息。", 20, 58);
            guide.textWrappingMode = TMPro.TextWrappingModes.Normal;
            guide.transform.SetSiblingIndex(2);
            recordingHint = RecordingLabel(content, template, "", 20, 42);
            OBSDialog.Title.text = "OBS 快速录制";
            ConnectionButton.interactable = !hasOngoingConnectionTask;
            UpdateRecordingView();
        }

        private static void SizeConnectionButton(Button button, float width)
        {
            var size = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.minHeight = size.preferredHeight = 46;
            size.flexibleWidth = size.flexibleHeight = 0;
            button.GetComponentInChildren<Text>(true).fontSize = 22;
        }

        private static Text RecordingLabel(Transform parent, Text template, string caption, float fontSize, float height)
        {
            var text = Instantiate(template, parent, false);
            text.name = "RecordingHelp";
            AdeEditorText.Configure(text, fontSize);
            text.text = caption;
            text.color = new Color(AdeUiTheme.Current.Text.r, AdeUiTheme.Current.Text.g, AdeUiTheme.Current.Text.b, .75f);
            var size = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 0;
            size.flexibleWidth = 1;
            size.preferredHeight = height;
            return text;
        }

        private void UpdateRecordingView()
        {
            OBSDialog.RightButtonText.color = OBSDialog.RightButton.IsInteractable() ? AdeUiTheme.Current.AccentText : AdeUiTheme.Current.Muted;
            if (!recordingStatus) return;
            recordingStatus.text = hasOngoingConnectionTask ? (obs.IsConnected ? "正在断开 OBS…" : "正在连接 OBS…")
                : obs.IsConnected ? "OBS 已连接" : "OBS 未连接";
            recordingHint.text = !obs.IsConnected ? "连接 OBS 并打开谱面后，即可开始录制。"
                : !Arcade.Gameplay.ArcGameplayManager.Instance.IsLoaded ? "连接成功，请先打开要录制的谱面。"
                : obsIsRecording ? "正在录制，录制结束后会自动停止。" : "准备就绪，点击开始录制将同步播放谱面。";
        }
    }
}
