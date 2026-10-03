using Arcade.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Arcade.Compose
{
	// Builds runtime dialogs from the dialogs and rows already present in the editor scene.
	public static class AdeUiKit
	{
		// Clones a scene dialog without its content component, content rows or persistent callbacks.
		public static T CloneDialog<T>(T source, string name) where T : AdeDialog
		{
			bool active = source.gameObject.activeSelf;
			source.gameObject.SetActive(false);
			GameObject clone = Object.Instantiate(source.gameObject, AdeDialogManager.Instance.Closed);
			source.gameObject.SetActive(active);
			clone.name = name;
			Transform content = clone.GetComponentInChildren<AdeAutoResizeScrollViewContent>(true).transform;
			for (int i = content.childCount - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject);
			Strip(clone);
			clone.SetActive(true);
			T dialog = clone.GetComponent<T>();
			dialog.View.SetActive(false);
			if (dialog is AdeSingleDialog single) ArcSkinManager.Instance.SingleDialogs.Add(single);
			if (dialog is AdeDualDialog dual) ArcSkinManager.Instance.DualDialogs.Add(dual);
			return dialog;
		}

		public static Transform Content(AdeDialog dialog)
		{
			return dialog.GetComponentInChildren<AdeAutoResizeScrollViewContent>(true).transform;
		}

		public static void SetMaxHeight(AdeDialog dialog, float height)
		{
			dialog.GetComponentInChildren<AdeAutoResizeScrollView>(true).max = height;
		}

		// Finds a row inside a scene dialog, e.g. "Address" in the OBS dialog.
		public static Transform FindRow(AdeDialog dialog, string name)
		{
			foreach (Transform child in dialog.GetComponentsInChildren<Transform>(true))
			{
				if (child.name == name && child.GetComponent<HorizontalLayoutGroup>() != null) return child;
			}
			return null;
		}

		public static GameObject CloneRow(Transform template, Transform parent, string name)
		{
			GameObject row = Object.Instantiate(template.gameObject, parent, false);
			row.name = name;
			Strip(row);
			row.SetActive(true);
			return row;
		}

		public static void SetOnClick(Button button, UnityAction action)
		{
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(action);
		}

		private static void Strip(GameObject root)
		{
			foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				string space = behaviour.GetType().Namespace ?? "";
				if (space.StartsWith("UnityEngine") || behaviour is AdeDialog
					|| behaviour is AdeAutoResizeScrollView || behaviour is AdeAutoResizeScrollViewContent)
					continue;
				Object.DestroyImmediate(behaviour);
			}
			// Source dialogs may have disabled their buttons (e.g. OBS while disconnected).
			foreach (Button button in root.GetComponentsInChildren<Button>(true))
			{
				button.onClick = new Button.ButtonClickedEvent();
				button.interactable = true;
			}
			foreach (InputField input in root.GetComponentsInChildren<InputField>(true))
			{
				input.onEndEdit = new InputField.EndEditEvent();
				input.onValueChanged = new InputField.OnChangeEvent();
				input.onSubmit = new InputField.SubmitEvent();
			}
			foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true)) toggle.onValueChanged = new Toggle.ToggleEvent();
		}
	}
}
