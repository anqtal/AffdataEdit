using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeSettingsDialog
    {
        private readonly List<TMP_Text> storageLabels = new List<TMP_Text>();
        private readonly List<Button> storageButtons = new List<Button>();

        private void BuildStorage()
        {
            AddStorageLocation("编辑器设置", AdeStorage.SettingsPath, "打开设置所在文件夹");
            AddStorageLocation("云端谱面", AdeStorage.CloudChartsFolder, "打开云端谱面文件夹");
            AddStorageLocation("账号登录状态", AdeStorage.AccountPath, "打开账号数据文件夹");
        }

        private void AddStorageLocation(string title, string path, string action)
        {
            var parent = pages[(int)Page.Storage];
            var rect = Rect(title, parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var label = rect.GetComponent<TMP_Text>();
            AdeEditorText.Configure(label, AdeEditorText.BodySize);
            label.text = title + "\n" + path;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            rect.GetComponent<LayoutElement>().minHeight = 74;
            storageLabels.Add(label);
            storageButtons.Add(MakeButton(parent, action, () =>
            {
                try
                {
                    string folder = Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;
                    Directory.CreateDirectory(folder);
                    Arcade.Util.Shell.FileBrowser.OpenExplorer(folder);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception)
                {
                    AdeToast.Instance?.Show("无法打开文件夹：" + error.Message);
                }
            }));
        }

        private void StyleStorage()
        {
            foreach (var label in storageLabels) label.color = AdeUiTheme.Current.Text;
            foreach (var button in storageButtons)
            {
                button.image.sprite = AdeUiTheme.RoundedSurface;
                button.image.type = Image.Type.Sliced;
                button.image.color = AdeUiTheme.Current.Input;
                button.GetComponentInChildren<TMP_Text>().color = AdeUiTheme.Current.Text;
            }
        }
    }
}
