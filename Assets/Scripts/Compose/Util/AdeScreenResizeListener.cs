using Arcade.Compose;
using UnityEngine;

namespace Arcade.Compose.UI
{
	[ExecuteAlways]
	public class AdeScreenResizeListener : MonoBehaviour
	{
		protected void OnRectTransformDimensionsChange()
		{
			if (!Application.isPlaying || !isActiveAndEnabled) return;
			var manager = ArcadeComposeManager.Instance;
			if (manager && manager.isActiveAndEnabled) manager.UpdateResolution();
		}

	}
}