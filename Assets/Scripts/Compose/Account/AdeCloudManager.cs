using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Arcade.Compose.Dialog;
using Arcade.Gameplay;
using Arcade.Util.Loader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade.Compose
{
	// Opens AffdataNet cloud charts as local projects and pushes chart edits back to the cloud.
	public sealed class AdeCloudManager : MonoBehaviour
	{
		private const string ApiUrl = "https://api.affdata.net";
		private const string EditorSceneName = "ArcEditor";
		private const string ButtonTemplatePath = "ArcaeaEditor/EditorCanvas/Bars/Left/View/Top/SaveMode";
		private const string RealtimePrefsKey = "AdeCloudRealtime";
		private static readonly string[] DifficultyNames = { "Past", "Present", "Future", "Beyond", "Eternal" };

		private sealed class CloudSide
		{
			[JsonProperty("enabled")] public bool Enabled;
			[JsonProperty("mode")] public string Mode;
			[JsonProperty("name")] public string Name;
			[JsonProperty("trackSkin")] public string TrackSkin;
		}

		private sealed class CloudSkin
		{
			[JsonProperty("light")] public CloudSide Light;
			[JsonProperty("dark")] public CloudSide Dark;
		}

		private sealed class CloudDiff
		{
			[JsonProperty("difficulty")] public int Difficulty;
			[JsonProperty("difficultyLabel")] public string DifficultyLabel;
			[JsonProperty("chartDifficulty")] public string ChartDifficulty;
		}

		private sealed class CloudChart
		{
			[JsonProperty("archiveId")] public int ArchiveId;
			[JsonProperty("title")] public string Title;
			[JsonProperty("artist")] public string Artist;
			[JsonProperty("bpm")] public float Bpm;
			[JsonProperty("isPrivate")] public bool IsPrivate;
			[JsonProperty("updatedAt")] public string UpdatedAt;
			[JsonProperty("diffs")] public CloudDiff[] Diffs = new CloudDiff[0];
			[JsonProperty("enabledDiffs")] public int[] EnabledDiffs = new int[0];
			[JsonProperty("rankedDiffs")] public int[] RankedDiffs = new int[0];
			[JsonProperty("skin")] public CloudSkin Skin;
			[JsonProperty("sfxArctap")] public string[] SfxArctap = new string[0];
		}

		private sealed class AppliedSkin
		{
			public Side Side = Side.Light;
			public string BackgroundUrl;
			public string TrackUrl;
		}

		public static AdeCloudManager Instance { get; private set; }

		public static string CloudRoot => Path.Combine(ArcadeComposeManager.ArcadePersistentFolder, "Cloud");

		private bool busy;
		private List<CloudChart> charts = new List<CloudChart>();
		private AdeDualDialog chooserDialog;
		private AdeSingleDialog listDialog;
		private Transform listContent;
		private Text listStatus;
		private Transform labelTemplate, buttonRowTemplate;
		private readonly List<Button> openButtons = new List<Button>();
		private Text realtimeLabel;
		private Color realtimeOffColor;
		private bool realtime;

		private string skinProjectFolder;
		private readonly List<UnityEngine.Object> skinObjects = new List<UnityEngine.Object>();
		private readonly Dictionary<int, CloudChart> openedCharts = new Dictionary<int, CloudChart>();

		private object syncedChart;
		private string syncedKey;
		private string syncedHash;
		private bool uploading;
		private float nextSyncTime;
		private bool realtimeErrorShown;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName && Instance == null)
					new GameObject(nameof(AdeCloudManager)).AddComponent<AdeCloudManager>();
			};
		}

		private void Awake()
		{
			Instance = this;
			realtime = PlayerPrefs.GetInt(RealtimePrefsKey, 0) == 1;
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
			DestroySkinObjects();
		}

		private void Start()
		{
			GameObject template = GameObject.Find(ButtonTemplatePath);
			if (template == null)
			{
				Debug.LogWarning("Cloud button template not found");
				return;
			}
			CreateButton(template, "CloudSave", "云存", () => StartCoroutine(Upload(true)));
			realtimeLabel = CreateButton(template, "CloudRealtime", "实时", ToggleRealtime);
			if (realtimeLabel != null) realtimeOffColor = realtimeLabel.color;
			UpdateRealtimeLabel();
			BuildDialogs();
		}

		private static Text CreateButton(GameObject template, string name, string text, UnityEngine.Events.UnityAction onClick)
		{
			GameObject entry = Instantiate(template, template.transform.parent);
			entry.name = name;
			entry.transform.SetAsLastSibling();
			Button button = entry.GetComponent<Button>();
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(onClick);
			Text label = entry.GetComponentInChildren<Text>(true);
			if (label != null) label.text = text;
			return label;
		}

		private void ToggleRealtime()
		{
			realtime = !realtime;
			PlayerPrefs.SetInt(RealtimePrefsKey, realtime ? 1 : 0);
			PlayerPrefs.Save();
			UpdateRealtimeLabel();
			realtimeErrorShown = false;
			AdeToast.Instance?.Show(realtime ? "已开启实时保存到云端" : "已关闭实时保存到云端");
		}

		private void UpdateRealtimeLabel()
		{
			if (realtimeLabel != null)
				realtimeLabel.color = realtime && AdeProjectManager.Instance != null ? AdeProjectManager.Instance.EnableColor : realtimeOffColor;
		}

		public void ShowOpenChooser()
		{
			if (chooserDialog == null)
			{
				AdeProjectManager.Instance.OpenLocalProject();
				return;
			}
			chooserDialog.SwitchOpenState();
		}

		// The chooser reuses the OBS dual dialog; the chart list reuses the common message dialog.
		private void BuildDialogs()
		{
			AdeDualDialog obs = Feature.AdeObsManager.Instance != null ? Feature.AdeObsManager.Instance.OBSDialog : null;
			AdeSingleDialog basic = AdeBasicSingleDialogContent.Instance != null ? AdeBasicSingleDialogContent.Instance.Dialog : null;
			Transform inputRow = obs != null ? AdeUiKit.FindRow(obs, "Address") : null;
			buttonRowTemplate = obs != null ? AdeUiKit.FindRow(obs, "Download") : null;
			if (inputRow == null || buttonRowTemplate == null || basic == null)
			{
				Debug.LogWarning("Cloud dialog templates not found");
				return;
			}
			labelTemplate = inputRow.GetComponentInChildren<Text>(true).transform;

			chooserDialog = AdeUiKit.CloneDialog(obs, "OpenChartDialog");
			chooserDialog.Title.text = "打开谱面";
			AdeUiKit.CreateLabelRow(AdeUiKit.Content(chooserDialog), labelTemplate, "从本地文件夹或 AffdataNet 云端打开谱面");
			chooserDialog.LeftButtonText.text = "本地谱面";
			chooserDialog.RightButtonText.text = "云端谱面";
			AdeUiKit.SetOnClick(chooserDialog.LeftButton, () =>
			{
				chooserDialog.Close();
				AdeProjectManager.Instance.OpenLocalProject();
			});
			AdeUiKit.SetOnClick(chooserDialog.RightButton, () =>
			{
				chooserDialog.Close();
				if (!AdeAccountManager.IsLoggedIn)
				{
					AdeToast.Instance?.Show("请先登录 AffdataNet");
					AdeAccountManager.Instance?.ShowLoginWindow();
					return;
				}
				listDialog.Title.text = $"云端谱面（{AdeAccountManager.Username}）";
				listDialog.Open();
				StartCoroutine(LoadList());
			});

			listDialog = AdeUiKit.CloneDialog(basic, "CloudChartsDialog");
			AdeUiKit.SetMaxHeight(listDialog, 480);
			listContent = AdeUiKit.Content(listDialog);
			listStatus = AdeUiKit.CreateLabelRow(listContent, labelTemplate, "");
			listDialog.ButtonText.text = "关闭";
			AdeUiKit.SetOnClick(listDialog.CompleteButton, () =>
			{
				if (!busy) listDialog.Close();
			});
		}

		private void SetMessage(string text)
		{
			if (listStatus == null) return;
			listStatus.text = text;
			listStatus.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
		}

		private void SetBusy(bool value)
		{
			busy = value;
			foreach (Button button in openButtons)
			{
				if (button != null) button.interactable = !value;
			}
			if (listDialog != null) listDialog.CompleteButton.interactable = !value;
		}

		private void RebuildList()
		{
			foreach (Button button in openButtons)
			{
				if (button != null) Destroy(button.transform.parent.gameObject);
			}
			openButtons.Clear();
			foreach (CloudChart chart in charts)
			{
				GameObject row = AdeUiKit.CloneRow(buttonRowTemplate, listContent, $"Chart{chart.ArchiveId}");
				string diffs = string.Join("  ", chart.EnabledDiffs.Select(d =>
				{
					CloudDiff diff = chart.Diffs?.FirstOrDefault(x => x.Difficulty == d);
					string name = string.IsNullOrWhiteSpace(diff?.DifficultyLabel) ? DifficultyNames[d] : diff.DifficultyLabel;
					return $"{name} {diff?.ChartDifficulty}{(chart.RankedDiffs.Contains(d) ? " (Ranked)" : "")}";
				}));
				Text label = AdeUiKit.CreateLabel(row.transform, labelTemplate, $"#{chart.ArchiveId} {chart.Title} - {chart.Artist}{(chart.IsPrivate ? "（私密）" : "")}\n<size=18>{diffs}</size>", 520);
				label.alignment = TextAnchor.MiddleLeft;
				label.supportRichText = true;
				label.transform.SetAsFirstSibling();
				Button open = row.GetComponentInChildren<Button>(true);
				open.GetComponentInChildren<Text>(true).text = "打开";
				LayoutElement openLayout = open.GetComponent<LayoutElement>();
				if (openLayout != null) openLayout.preferredWidth = 120;
				AdeUiKit.SetOnClick(open, () => StartCoroutine(OpenChart(chart)));
				openButtons.Add(open);
			}
		}

		private void Update()
		{
			AdeProjectManager project = AdeProjectManager.Instance;
			if (skinProjectFolder != null && (project == null || project.CurrentProjectFolder != skinProjectFolder))
			{
				skinProjectFolder = null;
				DestroySkinObjects();
				AdeSkinDialogContent.Instance?.ReapplySkin();
			}
			if (realtime && !uploading && Time.unscaledTime >= nextSyncTime)
			{
				nextSyncTime = Time.unscaledTime + 1f;
				StartCoroutine(Upload(false));
			}
		}

		private static bool TryGetCurrentArchive(out int archiveId)
		{
			archiveId = 0;
			string folder = AdeProjectManager.Instance?.CurrentProjectFolder;
			if (folder == null || AdeProjectManager.Instance.CurrentProjectMetadata == null) return false;
			string parent = Path.GetDirectoryName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar));
			return string.Equals(parent, Path.GetFullPath(CloudRoot), StringComparison.Ordinal)
				&& int.TryParse(Path.GetFileName(folder), out archiveId);
		}

		private static string Sha256(byte[] data)
		{
			using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data));
		}

		private IEnumerator Upload(bool manual)
		{
			ArcGameplayManager gameplay = ArcGameplayManager.Instance;
			AdeProjectManager project = AdeProjectManager.Instance;
			if (!TryGetCurrentArchive(out int archiveId) || gameplay?.Chart == null || project.IsLoading)
			{
				if (manual) AdeToast.Instance?.Show("当前打开的不是云端谱面");
				yield break;
			}
			if (!manual && gameplay.IsPlaying) yield break;
			if (!AdeAccountManager.IsLoggedIn)
			{
				if (manual) AdeAccountManager.Instance?.ShowLoginWindow();
				yield break;
			}

			int difficulty = project.CurrentDifficulty;
			string key = $"{archiveId}:{difficulty}";
			byte[] data;
			using (var stream = new MemoryStream())
			{
				gameplay.Chart.Serialize(stream, ArcadeComposeManager.Instance.ArcadePreference.ChartSortMode);
				data = stream.ToArray();
			}
			string hash = Sha256(data);
			// A newly loaded chart becomes the baseline so loading alone never uploads.
			if (!ReferenceEquals(syncedChart, gameplay.Chart) || syncedKey != key)
			{
				syncedChart = gameplay.Chart;
				syncedKey = key;
				syncedHash = hash;
				if (!manual) yield break;
			}
			if (!manual && hash == syncedHash) yield break;
			if (openedCharts.TryGetValue(archiveId, out CloudChart chart))
			{
				if (!chart.EnabledDiffs.Contains(difficulty))
				{
					if (manual) AdeToast.Instance?.Show($"云端谱面未启用 {DifficultyNames[difficulty]} 难度，无法保存");
					yield break;
				}
				if (chart.RankedDiffs.Contains(difficulty))
				{
					if (manual) AdeToast.Instance?.Show($"{DifficultyNames[difficulty]} 难度已 Ranked 冻结，无法保存到云端");
					yield break;
				}
			}

			uploading = true;
			try
			{
				File.WriteAllBytes(Path.Combine(project.CurrentProjectFolder, $"{difficulty}.aff"), data);
			}
			catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
			{
				Debug.LogWarning($"Cloud chart local write failed: {error.Message}");
			}
			using (var request = new UnityWebRequest($"{ApiUrl}/v2/game/editor/charts/{archiveId}/difficulties/{difficulty}", UnityWebRequest.kHttpVerbPUT))
			{
				request.uploadHandler = new UploadHandlerRaw(data);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "text/plain; charset=utf-8");
				request.SetRequestHeader("Authorization", "Bearer " + AdeAccountManager.Token);
				request.timeout = 30;
				yield return request.SendWebRequest();
				uploading = false;
				if (request.result == UnityWebRequest.Result.Success)
				{
					if (syncedKey == key) syncedHash = hash;
					realtimeErrorShown = false;
					if (manual) AdeToast.Instance?.Show($"已保存到云端：{DifficultyNames[difficulty]}");
				}
				else
				{
					nextSyncTime = Time.unscaledTime + 5f;
					if (manual || !realtimeErrorShown)
					{
						realtimeErrorShown = !manual;
						AdeToast.Instance?.Show("保存到云端失败：" + ErrorText(request));
					}
				}
			}
		}

		private static string ErrorText(UnityWebRequest request)
		{
			if (request.responseCode == 401) return "登录已失效，请重新登录";
			try
			{
				string error = (string)JObject.Parse(request.downloadHandler?.text ?? "")["error"];
				if (!string.IsNullOrEmpty(error)) return error;
			}
			catch (JsonException)
			{
			}
			return request.error;
		}

		private static UnityWebRequest Get(string url)
		{
			var request = UnityWebRequest.Get(url.StartsWith("/") ? ApiUrl + url : url);
			request.SetRequestHeader("Authorization", "Bearer " + AdeAccountManager.Token);
			request.timeout = 120;
			return request;
		}

		private IEnumerator LoadList()
		{
			SetBusy(true);
			SetMessage("正在获取云端谱面…");
			using (UnityWebRequest request = Get("/v2/game/editor/charts"))
			{
				yield return request.SendWebRequest();
				SetBusy(false);
				if (request.result != UnityWebRequest.Result.Success)
				{
					SetMessage("获取失败：" + ErrorText(request));
					yield break;
				}
				try
				{
					charts = JObject.Parse(request.downloadHandler.text)["items"]?.ToObject<List<CloudChart>>() ?? new List<CloudChart>();
					SetMessage(charts.Count == 0 ? "还没有上传过谱面" : "");
					RebuildList();
				}
				catch (JsonException error)
				{
					SetMessage("解析失败：" + error.Message);
				}
			}
		}

		private IEnumerator Download(string url, string path, bool required, Action<bool> done)
		{
			using (UnityWebRequest request = Get(url))
			{
				yield return request.SendWebRequest();
				string error = null;
				if (request.result == UnityWebRequest.Result.Success)
				{
					try
					{
						File.WriteAllBytes(path, request.downloadHandler.data);
						done(true);
						yield break;
					}
					catch (Exception writeError) when (writeError is IOException || writeError is UnauthorizedAccessException)
					{
						error = writeError.Message;
					}
				}
				if (required) SetMessage($"下载 {Path.GetFileName(path)} 失败：{error ?? ErrorText(request)}");
				done(false);
			}
		}

		private IEnumerator OpenChart(CloudChart chart)
		{
			SetBusy(true);
			string folder = Path.Combine(CloudRoot, chart.ArchiveId.ToString());
			string arcadeFolder = Path.Combine(folder, "Arcade");
			Directory.CreateDirectory(arcadeFolder);
			string files = $"/v2/game/editor/charts/{chart.ArchiveId}/files/";
			bool ok = true;

			SetMessage("正在下载音乐…");
			yield return Download(files + "base.opus", Path.Combine(folder, "base.opus"), true, result => ok = result);
			if (!ok) { SetBusy(false); yield break; }
			SetMessage("正在下载封面…");
			yield return Download(files + "base.jpg", Path.Combine(folder, "base.jpg"), false, _ => { });
			foreach (int difficulty in chart.EnabledDiffs)
			{
				SetMessage($"正在下载 {DifficultyNames[difficulty]} 谱面…");
				yield return Download(files + $"{difficulty}.aff", Path.Combine(folder, $"{difficulty}.aff"), true, result => ok = result);
				if (!ok) { SetBusy(false); yield break; }
			}
			foreach (string sfx in chart.SfxArctap ?? new string[0])
			{
				SetMessage($"正在下载音效 {sfx}…");
				yield return Download(files + Uri.EscapeDataString(sfx), Path.Combine(folder, Path.GetFileName(sfx)), false, _ => { });
			}

			AppliedSkin skin = ResolveSkin(chart);
			if (skin.TrackUrl == null && skin.BackgroundUrl != null && skin.BackgroundUrl.StartsWith("/v2/game/backgrounds/", StringComparison.Ordinal))
			{
				SetMessage("正在获取皮肤信息…");
				yield return ResolvePresetTrack(skin);
			}
			string backgroundPath = Path.Combine(arcadeFolder, "CloudBackground.jpg");
			string trackPath = Path.Combine(arcadeFolder, "CloudTrack.png");
			bool hasBackground = false, hasTrack = false;
			if (skin.BackgroundUrl != null)
			{
				SetMessage("正在下载背景…");
				yield return Download(skin.BackgroundUrl, backgroundPath, false, result => hasBackground = result);
			}
			if (skin.TrackUrl != null)
			{
				SetMessage("正在下载轨道…");
				yield return Download(skin.TrackUrl, trackPath, false, result => hasTrack = result);
			}

			WriteMetadata(folder, chart);
			openedCharts[chart.ArchiveId] = chart;
			while (AdeProjectManager.Instance.IsLoading) yield return null;
			AdeProjectManager.Instance.OpenProjectFolder(folder);
			while (AdeProjectManager.Instance.IsLoading) yield return null;
			ApplySkin(folder, skin.Side, hasBackground ? backgroundPath : null, hasTrack ? trackPath : null);
			SetBusy(false);
			SetMessage("");
			listDialog.Close();
			AdeToast.Instance?.Show($"已打开云端谱面：{chart.Title}");
		}

		// Mirrors AffdataPlay: the light side when enabled, otherwise the dark (conflict) side.
		private static AppliedSkin ResolveSkin(CloudChart chart)
		{
			var skin = new AppliedSkin();
			CloudSide side = chart.Skin?.Light;
			string sideName = "light";
			if (side == null || !side.Enabled)
			{
				side = chart.Skin?.Dark;
				sideName = "dark";
			}
			if (side == null || !side.Enabled) return skin;
			skin.Side = sideName == "dark" ? Side.Conflict : Side.Light;
			if (string.Equals(side.Mode, "custom", StringComparison.OrdinalIgnoreCase))
			{
				skin.BackgroundUrl = $"/v2/game/editor/charts/{chart.ArchiveId}/files/bg_{sideName}.jpg";
				if (!string.IsNullOrWhiteSpace(side.TrackSkin)) skin.TrackUrl = $"/v2/game/track-skins/{side.TrackSkin.Trim()}.png";
			}
			else if (!string.IsNullOrWhiteSpace(side.Name))
			{
				skin.BackgroundUrl = "/v2/game/backgrounds/" + Uri.EscapeDataString(side.Name.Trim());
			}
			return skin;
		}

		private IEnumerator ResolvePresetTrack(AppliedSkin skin)
		{
			string background = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(skin.BackgroundUrl.Substring("/v2/game/backgrounds/".Length)));
			using (UnityWebRequest request = Get("/v2/game/skin"))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success) yield break;
				try
				{
					string track = (string)JObject.Parse(request.downloadHandler.text)[background]?["track"];
					if (!string.IsNullOrWhiteSpace(track)) skin.TrackUrl = $"/v2/game/track-skins/{track.Trim()}.png";
				}
				catch (Exception error) when (error is JsonException || error is InvalidCastException)
				{
					Debug.LogWarning($"Cloud skin metadata ignored: {error.Message}");
				}
			}
		}

		private static void WriteMetadata(string folder, CloudChart chart)
		{
			string path = Path.Combine(folder, "Arcade", "Project.arcade");
			ArcadeProjectMetadata metadata = null;
			try
			{
				if (File.Exists(path)) metadata = JsonConvert.DeserializeObject<ArcadeProjectMetadata>(File.ReadAllText(path));
			}
			catch (JsonException)
			{
			}
			metadata = metadata ?? new ArcadeProjectMetadata();
			if (metadata.Difficulties == null || metadata.Difficulties.Length < AdeProjectManager.DIFFICULTY_COUNT)
			{
				var difficulties = new AdeChartDifficultyMetadata[AdeProjectManager.DIFFICULTY_COUNT];
				metadata.Difficulties?.CopyTo(difficulties, 0);
				metadata.Difficulties = difficulties;
			}
			metadata.Title = chart.Title;
			metadata.Artist = chart.Artist;
			metadata.BaseBpm = chart.Bpm;
			foreach (CloudDiff diff in chart.Diffs ?? new CloudDiff[0])
			{
				if (diff.Difficulty < 0 || diff.Difficulty >= AdeProjectManager.DIFFICULTY_COUNT) continue;
				var difficulty = metadata.Difficulties[diff.Difficulty] ?? new AdeChartDifficultyMetadata();
				difficulty.Rating = diff.ChartDifficulty;
				metadata.Difficulties[diff.Difficulty] = difficulty;
			}
			if (!chart.EnabledDiffs.Contains(metadata.LastWorkingDifficulty) && chart.EnabledDiffs.Length > 0)
				metadata.LastWorkingDifficulty = chart.EnabledDiffs.Contains(2) ? 2 : chart.EnabledDiffs[0];
			File.WriteAllText(path, JsonConvert.SerializeObject(metadata));
		}

		private void ApplySkin(string folder, Side side, string backgroundPath, string trackPath)
		{
			DestroySkinObjects();
			Sprite background = null, track = null;
			if (backgroundPath != null)
			{
				Texture2D texture = Loader.LoadTexture2D(backgroundPath);
				if (texture != null)
				{
					skinObjects.Add(texture);
					background = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
					skinObjects.Add(background);
				}
			}
			ArcSkinManager skinManager = ArcSkinManager.Instance;
			if (trackPath != null && skinManager.TrackComponents.Length > 0)
			{
				Texture2D texture = Loader.LoadTexture2D(trackPath);
				if (texture != null)
				{
					skinObjects.Add(texture);
					// Match the current track sprite's world size and pivot so the lane geometry is unchanged.
					Sprite current = skinManager.TrackComponents[0].sprite;
					float pixelsPerUnit = current != null ? texture.width / (current.rect.width / current.pixelsPerUnit) : 100f;
					Vector2 pivot = current != null ? new Vector2(current.pivot.x / current.rect.width, current.pivot.y / current.rect.height) : new Vector2(0.5f, 0.5f);
					track = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
					skinObjects.Add(track);
				}
			}
			AdeSkinDialogContent.Instance?.ApplyCloudSkin(side, background, track);
			skinProjectFolder = folder;
		}

		private void DestroySkinObjects()
		{
			foreach (UnityEngine.Object item in skinObjects)
			{
				if (item != null) Destroy(item);
			}
			skinObjects.Clear();
		}
	}
}
