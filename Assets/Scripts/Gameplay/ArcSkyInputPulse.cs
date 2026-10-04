using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcade.Gameplay
{
	// Arcade Alpha's ArcSkyInputFade: while playing, the sky input line and label pulse
	// between 200/255 and full opacity once every 120/BPM seconds (30% rising, 70%
	// falling); in editor mode they fade back to full opacity. The label rests at 0.784.
	public sealed class ArcSkyInputPulse : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";
		private const float LabelAlpha = 0.78431374f, LowAlpha = 200f / 255f, ReturnSeconds = 0.5f;

		private float pulse = 1;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(ArcSkyInputPulse)).AddComponent<ArcSkyInputPulse>();
			};
		}

		private void Update()
		{
			var skin = ArcSkinManager.Instance;
			var game = ArcGameplayManager.Instance;
			if (!skin || !game || !skin.SkyInputLine || !skin.SkyInputLabel) return;
			if (game.IsLoaded && game.IsPlaying && ArcTimingManager.Instance)
			{
				float bpm = Mathf.Max(50, Mathf.Abs(ArcTimingManager.Instance.CalculateBpmByTiming(game.ChartTiming, null)));
				float interval = 120f / bpm;
				float phase = Mathf.Repeat(Time.time, interval) / interval;
				float rise = 0.3f;
				pulse = phase < rise
					? Mathf.Lerp(LowAlpha, 1, phase / rise)
					: Mathf.Lerp(1, LowAlpha, (phase - rise) / (1 - rise));
			}
			else pulse = Mathf.MoveTowards(pulse, 1, Time.unscaledDeltaTime / ReturnSeconds);
			SetAlpha(skin.SkyInputLine, pulse);
			SetAlpha(skin.SkyInputLabel, pulse * LabelAlpha);
		}

		private static void SetAlpha(SpriteRenderer renderer, float alpha)
		{
			Color color = renderer.color;
			if (Mathf.Approximately(color.a, alpha)) return;
			color.a = alpha;
			renderer.color = color;
		}
	}
}
