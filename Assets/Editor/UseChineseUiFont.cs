using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Serializes Noto Sans SC into UI text that used the built-in font or Noto Sans, which have no
// CJK glyphs and fell back to a Japanese OS font. Updates every prefab, then the open scenes.
public static class UseChineseUiFont
{
	private const string FontPath = "Assets/Resources/AffdataEdit/Fonts/NotoSansSC-Regular.otf";

	[MenuItem("AffdataEdit/Use Noto Sans SC for UI Text")]
	private static void Run()
	{
		var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
		if (!font)
		{
			Debug.LogError("Font not found: " + FontPath);
			return;
		}
		int prefabs = 0, texts = 0;
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			int changed = 0;
			foreach (Text text in root.GetComponentsInChildren<Text>(true))
				if (Replace(text, font)) changed++;
			if (changed > 0)
			{
				PrefabUtility.SaveAsPrefabAsset(root, path);
				prefabs++;
				texts += changed;
			}
			PrefabUtility.UnloadPrefabContents(root);
		}
		int sceneTexts = 0;
		foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include))
		{
			if (!Replace(text, font)) continue;
			EditorUtility.SetDirty(text);
			EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
			sceneTexts++;
		}
		EditorSceneManager.SaveOpenScenes();
		Debug.Log($"Noto Sans SC: {texts} texts in {prefabs} prefabs, {sceneTexts} scene texts.");
	}

	private static bool Replace(Text text, Font font)
	{
		Font current = text.font;
		if (current && current.name != "LegacyRuntime" && current.name != "Arial" && current.name != "NotoSans-Regular")
			return false;
		Undo.RecordObject(text, "Use Noto Sans SC");
		text.font = font;
		// Its taller lines would otherwise be truncated in short fields.
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return true;
	}
}
