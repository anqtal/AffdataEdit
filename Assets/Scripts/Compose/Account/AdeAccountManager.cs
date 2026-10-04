using System;
using System.Collections;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public sealed class AdeAccountManager : MonoBehaviour
	{
		private const string LoginUrl = "https://api.affdata.net/v2/game/login";
		private const string PrefsKey = "AffdataNetAccount";
		private const string EditorSceneName = "ArcEditor";
		private const string RightBarPath = "ArcaeaEditor/EditorCanvas/Bars/Right/View";

		private sealed class Session
		{
			public string Token;
			public string Username;
			public DateTime ExpiresAt;
		}

		public static AdeAccountManager Instance { get; private set; }

		public static bool IsLoggedIn => Instance != null && Instance.session != null && Instance.session.ExpiresAt > DateTime.UtcNow;
		public static string Token => IsLoggedIn ? Instance.session.Token : null;
		public static string Username => IsLoggedIn ? Instance.session.Username : null;

		private Session session;
		private bool loggingIn;
		private AdeDualDialog dialog;
		private GameObject usernameRow, pinRow;
		private InputField usernameInput, pinInput;
		private Text statusText;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName && Instance == null)
					new GameObject(nameof(AdeAccountManager)).AddComponent<AdeAccountManager>();
			};
		}

		private void Awake()
		{
			Instance = this;
			LoadSession();
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		private void Start()
		{
			GameObject rightBar = GameObject.Find(RightBarPath);
			if (rightBar != null) AdeUiKit.CreateIconButton(rightBar.transform, "Account", "Account", ShowLoginWindow);
			BuildDialog();
			UpdateView();
		}

		// The login dialog reuses the OBS dialog and its "label + input" rows.
		private void BuildDialog()
		{
			AdeDualDialog source = Feature.AdeObsManager.Instance != null ? Feature.AdeObsManager.Instance.OBSDialog : null;
			Transform inputRow = source != null ? AdeUiKit.FindRow(source, "Address") : null;
			if (inputRow == null)
			{
				Debug.LogWarning("Account dialog templates not found");
				return;
			}
			dialog = AdeUiKit.CloneDialog(source, "AccountDialog");
			dialog.Title.text = "AffdataNet 账号";
			Transform content = AdeUiKit.Content(dialog);

			usernameRow = AdeUiKit.CloneRow(inputRow, content, "Username");
			usernameRow.GetComponentInChildren<Text>(true).text = "用户名";
			usernameInput = usernameRow.GetComponentInChildren<InputField>(true);
			usernameInput.characterLimit = 64;

			pinRow = AdeUiKit.CloneRow(inputRow, content, "Pin");
			pinRow.GetComponentInChildren<Text>(true).text = "PIN";
			pinInput = pinRow.GetComponentInChildren<InputField>(true);
			pinInput.contentType = InputField.ContentType.Pin;
			pinInput.characterLimit = 6;
			pinInput.onEndEdit.AddListener(_ =>
			{
				if (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
					OnPrimary();
			});

			statusText = AdeUiKit.CreateLabelRow(content, inputRow.GetComponentInChildren<Text>(true).transform, "");

			AdeUiKit.SetOnClick(dialog.LeftButton, OnPrimary);
			AdeUiKit.SetOnClick(dialog.RightButton, dialog.Close);
			dialog.RightButtonText.text = "关闭";
			dialog.OnClose += () =>
			{
				if (pinInput != null) pinInput.text = "";
			};
		}

		public void ShowLoginWindow()
		{
			if (dialog == null) return;
			SetStatus(null);
			UpdateView();
			dialog.Open();
		}

		private void OnPrimary()
		{
			if (loggingIn) return;
			if (IsLoggedIn)
			{
				Logout();
				return;
			}
			if (usernameInput.text.Trim().Length == 0 || pinInput.text.Length != 6)
			{
				SetStatus("请输入用户名和 6 位 PIN");
				return;
			}
			StartCoroutine(Login());
		}

		private void SetStatus(string text)
		{
			if (statusText == null) return;
			if (text == null && IsLoggedIn)
				text = $"已登录：{session.Username}\n有效期至：{session.ExpiresAt.ToLocalTime():yyyy-MM-dd HH:mm}";
			statusText.text = text ?? "";
			statusText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));
		}

		private void UpdateView()
		{
			if (dialog == null) return;
			usernameRow.SetActive(!IsLoggedIn);
			pinRow.SetActive(!IsLoggedIn);
			if (!IsLoggedIn && session == null && string.IsNullOrEmpty(usernameInput.text)) usernameInput.text = PlayerPrefs.GetString(PrefsKey + "User", "");
			dialog.LeftButtonText.text = IsLoggedIn ? "退出登录" : (loggingIn ? "登录中…" : "登录");
			dialog.LeftButton.interactable = !loggingIn;
		}

		private void LoadSession()
		{
			try
			{
				session = JsonConvert.DeserializeObject<Session>(PlayerPrefs.GetString(PrefsKey, ""));
			}
			catch (JsonException)
			{
				session = null;
			}
			if (session != null && (string.IsNullOrEmpty(session.Token) || session.ExpiresAt <= DateTime.UtcNow))
				session = null;
		}

		private void SaveSession()
		{
			if (session == null) PlayerPrefs.DeleteKey(PrefsKey);
			else
			{
				PlayerPrefs.SetString(PrefsKey, JsonConvert.SerializeObject(session));
				PlayerPrefs.SetString(PrefsKey + "User", session.Username);
			}
			PlayerPrefs.Save();
			UpdateView();
		}

		private IEnumerator Login()
		{
			loggingIn = true;
			UpdateView();
			SetStatus("正在登录…");
			string payload = JsonConvert.SerializeObject(new { username = usernameInput.text.Trim(), password = pinInput.text });
			using (var request = new UnityWebRequest(LoginUrl, UnityWebRequest.kHttpVerbPOST))
			{
				request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "application/json");
				request.timeout = 15;
				yield return request.SendWebRequest();
				loggingIn = false;

				JObject body = null;
				try
				{
					body = JObject.Parse(request.downloadHandler.text ?? "");
				}
				catch (JsonException)
				{
				}

				string token = (string)body?["token"];
				string expiresAt = (string)body?["expiresAt"];
				if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(token)
					&& DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture,
						DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime expires))
				{
					session = new Session
					{
						Token = token,
						Username = (string)body["user"]?["username"] ?? usernameInput.text.Trim(),
						ExpiresAt = expires
					};
					pinInput.text = "";
					SaveSession();
					dialog.Close();
					AdeToast.Instance?.Show($"已登录 AffdataNet：{session.Username}");
				}
				else
				{
					UpdateView();
					string error = (string)body?["error"];
					SetStatus(request.responseCode == 423
						? "PIN 已锁定，请在 AffdataNet 网站通过邮箱验证后重置 PIN。"
						: "登录失败：" + (string.IsNullOrEmpty(error) ? request.error : error));
				}
			}
		}

		private void Logout()
		{
			session = null;
			SaveSession();
			SetStatus(null);
			AdeToast.Instance?.Show("已退出 AffdataNet 登录");
		}
	}
}
