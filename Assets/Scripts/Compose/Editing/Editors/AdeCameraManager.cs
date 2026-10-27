using UnityEngine;
using Arcade.Gameplay;
using System;
using UnityEngine.EventSystems;

namespace Arcade.Compose
{
	[Serializable]
	public class AdeCameraProperty
	{
		public bool Orthographic;
		public float OrthographicSize;
		public Vector3 Position;
		public Vector3 Rotation;
	}
	public partial class AdeCameraManager : MonoBehaviour, IPointerClickHandler
	{
		public AdeCameraProperty[] Properties;
		public static AdeCameraManager Instance { get; private set; }
		public static bool FreeCameraEnabled => Instance && Instance.currentCameraStatus == Instance.Properties.Length;
		private int currentCameraStatus;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState() => Instance = null;

		private void Awake() => Instance = this;

		public void SwitchCameraStatus()
		{
			currentCameraStatus = (currentCameraStatus + 1) % (Properties.Length + 1);
			var manager = ArcCameraManager.Instance;
			manager.EditorCamera = !FreeCameraEnabled && currentCameraStatus != 0;
			if (FreeCameraEnabled)
			{
				ResetFreeCamera();
				AdeToast.Instance.Show("自由相机：WASD 移动，Shift 加速，中键转向，中键＋滚轮调速；右键视角按钮打开设置");
			}
			else if (currentCameraStatus != 0)
			{
				var properties = Properties[currentCameraStatus];
				manager.EditorCameraPosition = properties.Position;
				manager.EditorCameraRotation = properties.Rotation;
				manager.GameplayCamera.orthographic = properties.Orthographic;
				manager.GameplayCamera.orthographicSize = properties.OrthographicSize;
			}
			else
			{
				manager.GameplayCamera.orthographic = false;
				manager.ResetCamera();
			}
			pressedTime = 0;
		}

		public void OnPointerClick(PointerEventData data)
		{
			if (data.button == PointerEventData.InputButton.Right) OpenFreeCameraSettings();
		}
	}
}
