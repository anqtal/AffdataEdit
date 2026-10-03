using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public class AdeGameplayContentResizer : MonoBehaviour
	{
		// Template for the runtime texture; the asset itself is never resized.
		public RenderTexture GameplayRenderTexture;
		public Camera EditorCamera;

		// Layout and camera rects can lag a window change (fullscreen toggles, interrupted drags),
		// so the viewport is recomputed for a couple of frames after every size change.
		private const int SettleFrames = 2;

		private RenderTexture target;
		private RawImage content;
		private Vector2Int screenSize;
		private int settleFrames;

		private void Awake()
		{
			content = GetComponent<RawImage>();
		}

		private void LateUpdate()
		{
			var screen = new Vector2Int(Screen.width, Screen.height);
			if (screen != screenSize)
			{
				screenSize = screen;
				settleFrames = SettleFrames;
			}
			var size = new Vector2Int(EditorCamera.pixelWidth, EditorCamera.pixelHeight);
			if (size.x > 0 && size.y > 0 && (target == null || target.width != size.x || target.height != size.y))
			{
				Recreate(size);
				settleFrames = SettleFrames;
			}
			if (settleFrames > 0)
			{
				settleFrames--;
				Canvas.ForceUpdateCanvases();
				if (ArcadeComposeManager.Instance) ArcadeComposeManager.Instance.UpdateResolution();
			}
		}

		// A new texture (instead of resizing the bound one) makes the gameplay camera and its
		// screen-space canvases pick up the new size and aspect immediately.
		private void Recreate(Vector2Int size)
		{
			RenderTextureDescriptor descriptor = GameplayRenderTexture.descriptor;
			descriptor.width = size.x;
			descriptor.height = size.y;
			var next = new RenderTexture(descriptor) { name = GameplayRenderTexture.name, filterMode = GameplayRenderTexture.filterMode };
			next.Create();
			Camera gameplayCamera = ArcadeComposeManager.Instance ? ArcadeComposeManager.Instance.GameplayCamera : null;
			if (gameplayCamera)
			{
				gameplayCamera.targetTexture = next;
				gameplayCamera.ResetAspect();
			}
			if (content) content.texture = next;
			if (target)
			{
				target.Release();
				Destroy(target);
			}
			target = next;
		}

		private void OnDestroy()
		{
			if (target)
			{
				target.Release();
				Destroy(target);
			}
		}
	}
}
