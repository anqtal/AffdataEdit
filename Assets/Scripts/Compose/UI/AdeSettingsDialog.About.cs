using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeSettingsDialog
    {
        private readonly List<TMP_Text> aboutLabels = new List<TMP_Text>();
        private readonly List<Button> aboutButtons = new List<Button>();

        private void BuildAbout()
        {
            AboutText("AffdataEdit", true);
            AboutText($"版本 {Application.version}\n面向 AffdataPlay 与 AFF 谱面的桌面编辑器，提供谱面编辑、实时预览和 AffdataNet 云端协作。");

            AboutText("Credits · 开发与致谢", true);
            AboutText("AffdataEdit：anqtal 与项目贡献者。\n感谢所有提供反馈、测试谱面和改进建议的用户。");
            AboutText("项目来源", true);
            AboutText("Schwarzer — Arcade 原作者。\nyojohanshinwataikei 与 Arcade-plus 维护者 — 本项目的上游基础。\nArcade Alpha 贡献者 — 编辑交互、功能行为与视听资源的主要参考。\nyyyr — Arcade-One。\nAffdataPlay / Affdata.Core — 粒子、渲染、音频及共享算法。\nSpcDataPlay — Slide 渲染与资源参考。");
            AboutText("特别感谢", true);
            AboutText("LAM0578、linwenxuan04 — 技术支持。\n感谢 Arcade、Arcade-plus、Arcade Alpha 及相关开源项目的所有贡献者。");

            AboutText("第三方组件与资源", true);
            AboutText("Unity / TextMesh Pro / URP / Input System — 引擎、界面与渲染。\nUniTask、LitMotion、Newtonsoft.Json — 异步、动画与数据处理。\nBASS / BASS_FX / BASSOPUS、ManagedBass — 音频播放与处理。\nobs-websocket-dotnet、Websocket.Client — OBS 联动与网络连接。\nUnity Standalone File Browser（gkngkc 与贡献者）、Runtime OBJ Importer（Dummiesman）— 文件选择与模型导入。\nTabler Icons（Paweł Kuna）— UI 图标，MIT 许可。\nNoto Sans SC — 中文字体，SIL Open Font License 1.1。");

            AboutText("许可与资源署名", true);
            AboutText("项目代码主要采用 MIT 许可证。第三方代码、字体、图像和音频保留各自的许可与原作者权利；项目的 MIT 许可证不重新授权这些资源。\n部分游戏视听资源来自 lowiro 及参考项目的原作者，导入与适配不改变其权利归属。完整来源、致谢和许可说明见项目 README。");
            var page = pages[(int)Page.About];
            aboutButtons.Add(MakeButton(page, "项目主页与完整 Credits", () => Application.OpenURL("https://github.com/anqtal/AffdataEdit")));
            aboutButtons.Add(MakeButton(page, "问题反馈", () => Application.OpenURL("https://github.com/anqtal/AffdataEdit/issues")));
        }

        private void AboutText(string value, bool heading = false)
        {
            var rect = Rect("AboutText", pages[(int)Page.About], typeof(TextMeshProUGUI));
            var text = rect.GetComponent<TMP_Text>();
            AdeEditorText.Configure(text, heading ? AdeEditorText.HeadingSize : AdeEditorText.BodySize);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = value;
            aboutLabels.Add(text);
        }

        private void StyleAbout()
        {
            foreach (var label in aboutLabels) label.color = AdeUiTheme.Current.Text;
            foreach (var button in aboutButtons)
            {
                button.image.sprite = AdeUiTheme.RoundedSurface;
                button.image.type = Image.Type.Sliced;
                button.image.color = AdeUiTheme.Current.Input;
                button.GetComponentInChildren<TMP_Text>().color = AdeUiTheme.Current.Text;
            }
        }
    }
}
