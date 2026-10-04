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
    // Built players installed by the updater check R2's latest.json when the editor scene
    // opens. Only a confirmed newer version blocks the editor; if the check cannot complete
    // (offline, timeout, bad manifest) the user may retry or continue with this version.
    public sealed class AdeUpdateChecker : MonoBehaviour
    {
        private const string EditorSceneName = "ArcEditor";
        private static readonly Regex CommitPattern = new Regex("^[0-9a-fA-F]{40}$");

        private bool updateRequired, entered;
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
            // Builds without the updater's files (development or macOS builds) are not checked.
            string folder = Path.GetDirectoryName(Application.dataPath);
            if (!File.Exists(Path.Combine(folder, "build-version.txt")) || !File.Exists(Path.Combine(folder, "updater-config.json")))
            {
                Destroy(gameObject);
                return;
            }
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
            AdeUiKit.SetOnClick(dialog.LeftButton, () => StartCoroutine(CheckAtStartup()));
            AdeUiKit.SetOnClick(dialog.RightButton, () =>
            {
                if (updateRequired) Application.Quit();
                else dialog.Close();
            });
            // Closing the dialog (continue or the close button) enters the editor unless an
            // update is required.
            Transform close = dialog.View.transform.Find("ThemeClose");
            if (close) AdeUiKit.SetOnClick(close.GetComponent<Button>(), dialog.Close);
            dialog.OnClose += () =>
            {
                if (!updateRequired) Enter();
            };

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
            bool failed = !isChecking && !updateRequired;
            dialog.LeftButton.gameObject.SetActive(failed);
            dialog.RightButton.interactable = !isChecking;
            dialog.RightButtonText.text = updateRequired ? "退出" : "继续使用";
            // The dark theme's close button is only offered once the check has failed.
            Transform close = dialog.View.transform.Find("ThemeClose");
            if (close) close.gameObject.SetActive(failed);
        }

        private void Pass()
        {
            dialog.Close();
            Enter();
        }

        private void Enter()
        {
            if (entered) return;
            entered = true;
            AdeInputManager.Instance.Controls.Enable();
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
            SetState(false, "无法检查更新，可能是网络不可用或服务器暂时无响应。\n可以重试，或先继续使用当前版本。");
        }
    }
}
