using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcade.Compose
{
	// A Dropdown puts the blocker that closes its list on outside clicks under the nearest
	// override-sorted canvas, stretched to it. In the side bars (and the settings drawer inside
	// the left bar) that is only the bar's rect, so clicks elsewhere left the list open. The
	// blocker is moved to the root canvas to cover the whole screen.
	public sealed class AdeDropdownBlocker : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";
		private RectTransform[] bars = new RectTransform[0];

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(AdeDropdownBlocker)).AddComponent<AdeDropdownBlocker>();
			};
		}

		private void Start()
		{
			var compose = ArcadeComposeManager.Instance;
			if (compose) bars = new[] { compose.TopBar, compose.BottomBar, compose.LeftBar, compose.RightBar };
		}

		private void LateUpdate()
		{
			foreach (RectTransform bar in bars)
			{
				if (!bar) continue;
				// Dropdown.Show names it "Blocker" and destroys it on hide wherever it is.
				var blocker = bar.Find("Blocker") as RectTransform;
				if (!blocker) continue;
				var canvas = bar.GetComponentInParent<Canvas>();
				if (!canvas) continue;
				blocker.SetParent(canvas.rootCanvas.transform, false);
				blocker.anchorMin = Vector2.zero;
				blocker.anchorMax = Vector2.one;
				blocker.offsetMin = blocker.offsetMax = Vector2.zero;
			}
		}
	}
}
