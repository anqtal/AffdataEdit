using Arcade.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade.Compose
{
	// Settings row for ArcArcRenderer.AllowCrossTiming. Like Arcade Alpha it is not saved and
	// starts off on every launch.
	public sealed class AdeArcCrossTimingToggle : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(AdeArcCrossTimingToggle)).AddComponent<AdeArcCrossTimingToggle>();
			};
		}

		private void Start()
		{
			ArcArcRenderer.AllowCrossTiming = false;
			Toggle template = ArcadeComposeManager.Instance ? ArcadeComposeManager.Instance.PlaybackSyncToggle : null;
			Transform templateRow = template ? template.GetComponentInParent<HorizontalLayoutGroup>(true)?.transform : null;
			if (templateRow == null)
			{
				Debug.LogWarning("Arc cross-timing toggle template not found");
				return;
			}
			GameObject row = AdeUiKit.CloneRow(templateRow, templateRow.parent, "ArcCrossTiming");
			row.transform.SetSiblingIndex(templateRow.GetSiblingIndex() + 1);
			row.GetComponentInChildren<Text>(true).text = "Arc 跨 Timing 渲染";
			Toggle toggle = row.GetComponentInChildren<Toggle>(true);
			toggle.SetIsOnWithoutNotify(false);
			toggle.onValueChanged.AddListener(value =>
			{
				ArcArcRenderer.AllowCrossTiming = value;
				if (ArcArcManager.Instance) ArcArcManager.Instance.Rebuild();
			});
			Destroy(gameObject);
		}
	}
}
