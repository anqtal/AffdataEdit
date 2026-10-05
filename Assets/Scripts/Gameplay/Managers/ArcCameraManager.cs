using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Arcade.Gameplay.Chart;

namespace Arcade.Gameplay
{
	public class ArcCameraManager : MonoBehaviour
	{
		public Camera GameplayCamera;
		public Transform SkyInputLabel;
		public static ArcCameraManager Instance { get; private set; }

		[HideInInspector]
		public float CurrentTilt = 0;

		[HideInInspector]
		public float EnwidenRatio = 0;

		[HideInInspector]
		[System.NonSerialized]
		public List<ArcCamera> Cameras = new List<ArcCamera>();

		[HideInInspector]
		public bool IsReset = true;

		public Vector3 ResetPosition
		{
			get
			{
				return new Vector3(0, 9f + 4.5f * EnwidenRatio, Aspect(9f, 8f) + Aspect(4.5f, 4f) * EnwidenRatio);
			}
		}
		public Vector3 ResetRotation
		{
			get
			{
				return new Vector3(Aspect(26.565f, 27.378f), 180, 0);
			}
		}

		// Arcade Alpha scales a 1280x720 design resolution into the gameplay view: views wider
		// than 16:9 widen it, narrower ones raise it toward 960 (4:3). Camera values blend
		// between the 16:9 and 4:3 setups by that height instead of switching at one ratio.
		private const float DesignWidth = 1280, DesignHeight = 720, DesignHeight4By3 = 960;

		public Vector2 DesignResolution
		{
			get
			{
				float w = Mathf.Max(1, GameplayCamera.pixelWidth), h = Mathf.Max(1, GameplayCamera.pixelHeight);
				float scale = Mathf.Min(w / DesignWidth, h / DesignHeight);
				return new Vector2(w / scale, h / scale);
			}
		}

		// 0 at 16:9 or wider, 1 at 4:3 or narrower.
		public float AspectBlend => Mathf.Clamp01((DesignResolution.y - DesignHeight) / (DesignHeight4By3 - DesignHeight));

		private float Aspect(float wide, float narrow) => Mathf.Lerp(wide, narrow, AspectBlend);

		private void Awake()
		{
			Instance = this;
		}
		private void Start()
		{
			ResetCamera();
		}

		public void Clean()
		{
			Cameras.Clear();
		}
		public void Load(List<ArcCamera> cameras)
		{
			// Note: We replaced the inplace sort by sort to another list and reassign
			// just because we do not have stable inplace sort now in dot net
			Cameras = cameras.OrderBy(camera => camera.Timing).ToList();
			ArcGameplayManager.Instance.Chart.Cameras = Cameras;
		}

		public void ResetCamera()
		{
			// Canvas resize callbacks can arrive while scene objects are being destroyed.
			if (!GameplayCamera || !SkyInputLabel) return;
			GameplayCamera.fieldOfView = Aspect(50, 65);
			GameplayCamera.nearClipPlane = 1f / 100f;
			GameplayCamera.farClipPlane = 10000f;
			// Arcade Alpha's label position and scale.
			SkyInputLabel.localPosition = new Vector3(-(DesignResolution.x * 0.5f / 100f + Aspect(0.7f, -0.1f)), 0.13f, 0);
			SkyInputLabel.localScale = new Vector3(0.6667f, 0.6667f, 1);
			GameplayCamera.transform.position = new Vector3(0, 9, Aspect(9, 8));
			GameplayCamera.transform.LookAt(new Vector3(0, -5.5f, -20), new Vector3(0, 1, 0));
			IsReset = true;
		}

		private void Update()
		{
			if (UpdateEditorCamera()) return;
			UpdateCameraPosition();
			UpdateCameraTilt();
		}

		public bool EditorCamera { get; set; }
		public Vector3 EditorCameraPosition { get; set; }
		public Vector3 EditorCameraRotation { get; set; }
		private bool UpdateEditorCamera()
		{
			if (EditorCamera)
			{
				GameplayCamera.transform.localPosition = EditorCameraPosition;
				GameplayCamera.transform.localRotation = Quaternion.Euler(EditorCameraRotation);
				return true;
			}
			return false;
		}

		private void UpdateCameraPosition()
		{
			int currentTiming = ArcGameplayManager.Instance.ChartTiming;
			for (int i = 0; i < Cameras.Count; ++i)
			{
				ArcCamera c = Cameras[i];
				if (c.Timing > currentTiming) break;
				c.Update(currentTiming);
				if (c.CameraType == Chart.CameraEaseType.Reset)
				{
					for (int r = 0; r < i; ++r)
					{
						ArcCamera cr = Cameras[r];
						cr.Update(c.Timing);
					}
				}
			}
			Vector3 position = ResetPosition;
			Vector3 rotation = ResetRotation;
			IsReset = true;
			foreach (var c in Cameras)
			{
				if (c.Timing > currentTiming) break;
				IsReset = c.CameraType == Chart.CameraEaseType.Reset;
				if (IsReset)
				{
					position = ResetPosition;
					rotation = ResetRotation;
				}
				position += new Vector3(-c.Move.x, c.Move.y, c.Move.z) * c.Percent / 100;
				rotation += new Vector3(-c.Rotate.y, -c.Rotate.x, c.Rotate.z) * c.Percent;
			}
			GameplayCamera.transform.localPosition = position;
			GameplayCamera.transform.localRotation = Quaternion.Euler(0, 0, rotation.z) * Quaternion.Euler(rotation.x, rotation.y, 0);
		}
		private void UpdateCameraTilt()
		{
			if (!IsReset)
			{
				CurrentTilt = 0;
				return;
			}
			float currentArcPos = ArcGameplayManager.Instance.Auto && ArcGameplayManager.Instance.IsPlaying ? -ArcArcManager.Instance.ArcJudgePos : 0;
			float pos = Mathf.Clamp(currentArcPos / 4.25f, -1, 1) * 0.05f;
			float delta = pos - CurrentTilt;
			if (Mathf.Abs(delta) >= 0.001f)
			{
				float speed = 4.8f;
				if (Mathf.Abs(pos) <= 0.001f)
				{
					speed /= 2f;
				}
				CurrentTilt = CurrentTilt + speed * delta * Time.deltaTime;
			}
			else
			{
				CurrentTilt = pos;
			}
			GameplayCamera.transform.LookAt(new Vector3(0, -5.5f + 4.5f * EnwidenRatio, -20f + Aspect(4.5f, 4f) * EnwidenRatio), new Vector3(CurrentTilt, 1 - CurrentTilt, 0));
		}
	}
}

