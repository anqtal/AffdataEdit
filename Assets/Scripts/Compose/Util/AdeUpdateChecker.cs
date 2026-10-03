using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Arcade.Compose.Feature;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Arcade.Compose
{
    // Built players check R2's latest.json when the editor scene opens. Until the build is
    // confirmed current, a cloned editor dialog and a blocker cover the editor and hotkeys stop.
    public sealed class AdeUpdateChecker : MonoBehaviour
    {
        private const string EditorSceneName = "ArcEditor";
        private static readonly Regex CommitPattern = new Regex("^[0-9a-fA-F]{40}$");

        private bool updateRequired;
        private AdeDualDialog dialog;
        private Text messageText;
        private GameObject blocker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor) return;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (scene.name == EditorSceneName)
                    new GameObject(nameof(AdeUpdateChecker)).AddComponent<AdeUpdateChecker>();
            };
        }

        private void Start()
        {
            AdeDualDialog source = AdeObsManager.Instance != null ? AdeObsManager.Instance.OBSDialog : null;
            Transform inputRow = source != null ? AdeUiKit.FindRow(source, "Address") : null;
            if (inputRow == null)
            {
                Debug.LogWarning("Update dialog templates not found");
                return;
            }
            dialog = AdeUiKit.CloneDialog(source, "UpdateDialog");
            dialog.Title.text = "检查更新";
            messageText = AdeUiKit.CreateLabelRow(AdeUiKit.Content(dialog), inputRow.GetComponentInChildren<Text>(true).transform, "");
            dialog.LeftButtonText.text = "重试";
            dialog.RightButtonText.text = "退出";
            AdeUiKit.SetOnClick(dialog.LeftButton, () => StartCoroutine(CheckAtStartup()));
            AdeUiKit.SetOnClick(dialog.RightButton, Application.Quit);

            // Full-screen blocker beneath the dialog so the editor behind cannot be used.
            Transform layer = AdeDialogManager.Instance.Opening;
            blocker = new GameObject("UpdateBlocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(layer.parent, false);
            blocker.transform.SetSiblingIndex(layer.GetSiblingIndex());
            var rect = (RectTransform)blocker.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            AdeInputManager.Instance.Controls.Disable();
            dialog.Open();
            StartCoroutine(CheckAtStartup());
        }

        private void SetState(bool isChecking, string text)
        {
            messageText.text = text;
            dialog.LeftButton.gameObject.SetActive(!isChecking && !updateRequired);
            dialog.RightButton.interactable = !isChecking;
        }

        private void Pass()
        {
            AdeInputManager.Instance.Controls.Enable();
            dialog.Close();
            Destroy(blocker);
            Destroy(gameObject);
        }

        private IEnumerator CheckAtStartup()
        {
            SetState(true, "正在检查最新版本…");
            string folder = Path.GetDirectoryName(Application.dataPath);
            string versionPath = Path.Combine(folder, "build-version.txt");
            string configPath = Path.Combine(folder, "updater-config.json");
            if (!File.Exists(versionPath) || !File.Exists(configPath))
            {
                FailCheck();
                yield break;
            }

            string currentVersion;
            Uri manifestUri;
            try
            {
                currentVersion = File.ReadAllText(versionPath).Trim();
                string url = (string)JObject.Parse(File.ReadAllText(configPath))["manifestUrl"];
                if (!CommitPattern.IsMatch(currentVersion)
                    || !Uri.TryCreate(url, UriKind.Absolute, out manifestUri)
                    || manifestUri.Scheme != Uri.UriSchemeHttps)
                {
                    FailCheck();
                    yield break;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is JsonException || error is ArgumentException)
            {
                Debug.Log($"Update check skipped: {error.Message}");
                FailCheck();
                yield break;
            }

            string latestVersion;
            using (var request = UnityWebRequest.Get(manifestUri.AbsoluteUri))
            {
                request.timeout = 10;
                request.SetRequestHeader("Cache-Control", "no-cache");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    FailCheck();
                    Debug.Log($"Update check unavailable: {request.error}");
                    yield break;
                }
                try
                {
                    JObject manifest = JObject.Parse(request.downloadHandler.text);
                    if ((int?)manifest["schema"] != 1) { FailCheck(); yield break; }
                    latestVersion = (string)manifest["version"];
                }
                catch (Exception error) when (error is JsonException || error is ArgumentException
                    || error is FormatException || error is InvalidCastException || error is OverflowException)
                {
                    FailCheck();
                    Debug.Log($"Update check skipped: {error.Message}");
                    yield break;
                }
            }
            if (latestVersion == null || !CommitPattern.IsMatch(latestVersion))
            {
                FailCheck();
                yield break;
            }
            if (string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
            {
                Pass();
                yield break;
            }
            updateRequired = true;
            SetState(false, "当前版本不是最新版，请先更新。\n\n关闭 AffdataEdit 后，运行程序目录中的\nAffdataEdit-Updater.exe 完成更新，再重新启动。");
        }

        private void FailCheck()
        {
            SetState(false, "无法确认是否为最新版本，暂时不能进入编辑器。\n请检查网络后重试；如安装文件不完整，请运行 updater 修复。");
        }
    }
}
