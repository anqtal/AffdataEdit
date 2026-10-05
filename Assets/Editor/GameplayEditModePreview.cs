using Arcade.Compose;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// In edit mode the Game view shows the gameplay area as play mode lays it out: the gameplay
// camera renders into a temporary texture sized to the Game view, with the same camera rect
// as editor mode at runtime. Saving, entering play mode and reloading scripts restore the
// serialized texture and rect first, so the scene never references the temporary texture.
[InitializeOnLoad]
public static class GameplayEditModePreview
{
	private static RenderTexture preview;
	private static Camera camera;
	private static RawImage content;
	private static RenderTexture originalTexture;
	private static Rect originalRect;
	private static double nextCheck;

	static GameplayEditModePreview()
	{
		EditorApplication.update += Update;
		EditorSceneManager.sceneSaving += (scene, path) => Restore();
		// Entering play mode keeps the computed rect, so the frames shown while Unity imports
		// and reloads line up with the bars; only the temporary texture must not be carried over.
		EditorApplication.playModeStateChanged += state =>
		{
			if (state == PlayModeStateChange.ExitingEditMode) Restore(false);
		};
		AssemblyReloadEvents.beforeAssemblyReload += Restore;
		EditorSceneManager.sceneClosing += (scene, removing) => Restore();
	}

	private static void Update()
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextCheck) return;
		nextCheck = EditorApplication.timeSinceStartup + 0.2;
		var compose = Object.FindAnyObjectByType<ArcadeComposeManager>();
		var resizer = Object.FindAnyObjectByType<AdeGameplayContentResizer>();
		if (!compose || !resizer || !compose.GameplayCamera || !resizer.EditorCamera) return;

		var size = new Vector2Int(resizer.EditorCamera.pixelWidth, resizer.EditorCamera.pixelHeight);
		if (size.x <= 0 || size.y <= 0) return;
		if (camera != compose.GameplayCamera)
		{
			Restore();
			camera = compose.GameplayCamera;
			content = resizer.GetComponent<RawImage>();
			originalTexture = camera.targetTexture;
			originalRect = camera.rect;
		}
		bool changed = false;
		RenderTexture old = null;
		if (!preview || preview.width != size.x || preview.height != size.y)
		{
			RenderTextureDescriptor descriptor = resizer.GameplayRenderTexture.descriptor;
			descriptor.width = size.x;
			descriptor.height = size.y;
			old = preview;
			preview = new RenderTexture(descriptor) { name = "GameplayEditModePreview", hideFlags = HideFlags.HideAndDontSave };
			preview.Create();
			changed = true;
		}
		if (camera.targetTexture != preview)
		{
			camera.targetTexture = preview;
			camera.ResetAspect();
			changed = true;
		}
		if (content && content.texture != preview)
		{
			content.texture = preview;
			changed = true;
		}
		// The camera and content now use the new texture, so the old one can go.
		if (old)
		{
			old.Release();
			Object.DestroyImmediate(old);
		}
		Rect rect = compose.EditorModeGameplayCameraRect;
		if (camera.rect != rect)
		{
			camera.rect = rect;
			changed = true;
		}
		if (!changed) return;
		var cameraManager = Object.FindAnyObjectByType<Arcade.Gameplay.ArcCameraManager>();
		if (cameraManager) cameraManager.ResetCamera();
		UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
	}

	private static void Restore() => Restore(true);

	private static void Restore(bool rect)
	{
		// Entering play mode: give the serialized texture the preview size so the frames
		// before the runtime resizer runs are not stretched. It is only the runtime template.
		if (!rect && preview && originalTexture
			&& (originalTexture.width != preview.width || originalTexture.height != preview.height))
		{
			originalTexture.Release();
			originalTexture.width = preview.width;
			originalTexture.height = preview.height;
			originalTexture.Create();
		}
		if (camera)
		{
			camera.targetTexture = originalTexture;
			if (rect) camera.rect = originalRect;
			camera.ResetAspect();
		}
		if (content) content.texture = originalTexture;
		camera = null;
		content = null;
		Release();
	}

	private static void Release()
	{
		if (!preview) return;
		preview.Release();
		Object.DestroyImmediate(preview);
		preview = null;
	}
}
