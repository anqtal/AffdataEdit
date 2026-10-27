using Arcade.Gameplay;
using Arcade.Compose.Editing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Arcade.Compose
{
	public partial class AdeCameraManager
	{
		private float speed = 8, pressedTime;
		private static bool CanNavigate => FreeCameraEnabled && ArcadeComposeManager.Instance.IsEditorMode
			&& AdeGameplayContentInputHandler.InputActive && !AdeInputManager.Instance.IsTextInputFocused
			&& AdeInputManager.Instance.CurrentRebindingButton == null
			&& AdeInputManager.Instance.Controls.asset.enabled
			&& (!AdeDialogManager.Instance || AdeDialogManager.Instance.Opening.childCount == 0);
		public static bool FreeCameraPressing => CanNavigate && Mouse.current != null && Mouse.current.middleButton.isPressed
			&& (!AdeDragEdit.Instance || !AdeDragEdit.Instance.IsDragging);

		internal static bool ConsumesHotkey(InputAction action)
		{
			if (FreeCameraPressing) return true;
			if (!CanNavigate || HasModifier()) return false;
			foreach (var control in action.controls)
				if (control is KeyControl key && key.isPressed
					&& (key.keyCode == Key.W || key.keyCode == Key.A || key.keyCode == Key.S || key.keyCode == Key.D)) return true;
			return false;
		}

		private static bool HasModifier()
		{
			var keyboard = Keyboard.current;
			return keyboard != null && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed
				|| keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed);
		}

		private void ResetFreeCamera()
		{
			var manager = ArcCameraManager.Instance;
			var camera = manager.GameplayCamera;
			camera.orthographic = false;
			camera.transform.position = new Vector3(0, 9, Mathf.Lerp(9, 8, manager.AspectBlend));
			camera.transform.LookAt(new Vector3(0, -5.5f, -20), Vector3.up);
		}

		private void Update()
		{
			if (!CanNavigate) { pressedTime = 0; return; }
			var trans = ArcCameraManager.Instance.GameplayCamera.transform;
			if (FreeCameraPressing)
			{
				// Alpha's legacy mouse axes scale pixel deltas by 0.1 before its 8x rotation.
				Rotate(trans, Mouse.current.delta.ReadValue() * .8f);
				float scroll = Mouse.current.scroll.ReadValue().y;
				if (scroll != 0)
				{
					speed = Mathf.Clamp(speed + Mathf.Sign(scroll) * .5f, 1, 20);
					AdeToast.Instance.Show($"自由相机速度：{speed:F1}x");
				}
			}
			var gamepad = Gamepad.current;
			if (gamepad != null)
			{
				var rotation = gamepad.rightStick.ReadValue();
				Rotate(trans, new Vector2(-rotation.x, rotation.y) * (2.4f * 60 * Time.unscaledDeltaTime));
			}
			if (HasModifier()) { pressedTime = 0; return; }
			var keyboard = Keyboard.current;
			var move = Vector2.zero;
			if (keyboard != null)
			{
				move.x = keyboard.aKey.isPressed ? -1 : keyboard.dKey.isPressed ? 1 : 0;
				move.y = keyboard.wKey.isPressed ? 1 : keyboard.sKey.isPressed ? -1 : 0;
			}
			var stick = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
			bool fast = keyboard != null && keyboard.shiftKey.isPressed;
			if (stick.sqrMagnitude > .1f)
			{
				move += stick;
				fast |= stick.sqrMagnitude > .7f;
			}
			if (move == Vector2.zero) { pressedTime = 0; return; }
			// Preserve Alpha's 0.1/0.5 units per 60 Hz frame without depending on frame rate.
			float distance = (fast ? .5f : .1f) * 60 * Time.unscaledDeltaTime * (1 + pressedTime / 5) * speed / 8;
			trans.position += (trans.right * move.x + trans.forward * move.y) * distance;
			pressedTime += Time.unscaledDeltaTime;
		}

		private static void Rotate(Transform trans, Vector2 delta)
		{
			if (delta == Vector2.zero) return;
			trans.Rotate(Vector3.right, -delta.y);
			var rotation = trans.localEulerAngles;
			rotation.y += delta.x;
			trans.localEulerAngles = rotation;
		}
	}
}
