using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Arcade.Compose
{
    public sealed class AdeUpdateChecker : MonoBehaviour
    {
        private static readonly Regex CommitPattern = new Regex("^[0-9a-fA-F]{40}$");

        public Font DisplayFont;
        private string message = "正在检查最新版本…";
        private bool checking;
        private bool updateRequired;

        private void Start() { StartCoroutine(CheckAtStartup()); }

        private IEnumerator CheckAtStartup()
        {
            checking = true;
            message = "正在检查最新版本…";
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
                yield return SceneManager.LoadSceneAsync("ArcEditor", LoadSceneMode.Single);
                yield break;
            }
            checking = false;
            updateRequired = true;
            message = "当前版本不是最新版，请先更新。\n\n关闭 AffdataEdit 后，运行程序目录中的\nAffdataEdit-Updater.exe 完成更新，再重新启动。";
        }

        private void FailCheck()
        {
            checking = false;
            message = "无法确认是否为最新版本，暂时不能进入编辑器。\n请检查网络后重试；如安装文件不完整，请运行 updater 修复。";
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 800f, Screen.height / 500f));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            var style = new GUIStyle(GUI.skin.label)
            {
                font = DisplayFont, fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            float left = (Screen.width / scale - 640) / 2;
            float top = (Screen.height / scale - 240) / 2;
            GUI.Box(new Rect(left, top, 640, 240), GUIContent.none);
            GUI.Label(new Rect(left + 20, top + 20, 600, 140), message, style);
            if (!checking)
            {
                if (!updateRequired && GUI.Button(new Rect(left + 160, top + 180, 140, 36), "重试", new GUIStyle(GUI.skin.button) { font = DisplayFont, fontSize = 18 }))
                    StartCoroutine(CheckAtStartup());
                if (GUI.Button(new Rect(left + 340, top + 180, 140, 36), "退出", new GUIStyle(GUI.skin.button) { font = DisplayFont, fontSize = 18 }))
                    Application.Quit();
            }
        }
    }
}
