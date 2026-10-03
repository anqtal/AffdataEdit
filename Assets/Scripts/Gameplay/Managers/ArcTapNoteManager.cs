using System.Collections.Generic;
using UnityEngine;
using Arcade.Gameplay.Chart;

namespace Arcade.Gameplay
{
	public class ArcTapNoteManager : MonoBehaviour
	{
		public static ArcTapNoteManager Instance { get; private set; }
        internal readonly ArcNoteVisualPool VisualPool = new ArcNoteVisualPool();

		private void Awake()
		{
			Instance = this;
		}

		[HideInInspector]
		[System.NonSerialized]
		public List<ArcTap> Taps = new List<ArcTap>();
		[HideInInspector]
		public readonly float[] Lanes = { 10.625f, 6.375f, 2.125f, -2.125f, -6.375f, -10.625f };
		public GameObject TapNotePrefab;
        public Sprite DefaultSprite;
		public Transform NoteLayer;
		public Material ShaderdMaterial;

		public void Clean()
		{
			foreach (var t in Taps) t.Destroy();
			Taps.Clear();
            VisualPool.Clear();
		}
		public void Load(List<ArcTap> taps)
		{
			Taps = taps;
			foreach (var t in Taps)
			{
				t.Instantiate();
			}
		}

		public void Add(ArcTap tap)
		{
			tap.Instantiate();
			Taps.Add(tap);
			tap.SetupArcTapConnection();
		}
		public void Remove(ArcTap tap)
		{
			tap.Destroy();
			Taps.Remove(tap);
		}

		private void Update()
		{
			if (Taps == null) return;
			if (ArcGameplayManager.Instance.Auto) JudgeTapNotes();
			RenderTapNotes();
		}

		private void RenderTapNotes()
		{
			ArcTimingManager timing = ArcTimingManager.Instance;
            Matrix4x4 parentMatrix = NoteLayer.localToWorldMatrix;
            Quaternion rotation = TapNotePrefab.transform.localRotation;

			foreach (var t in Taps)
			{
				if (!timing.ShouldTryRender(t.Timing, t.TimingGroup) || t.Judged || t.GroupHide())
				{
					t.Enable = false;
					continue;
				}
				t.Position = timing.CalculatePositionByTiming(t.Timing, t.TimingGroup);
				if (t.Position > 100000 || t.Position < -100000)
				{
					t.Enable = false;
					continue;
				}
				t.Enable = true;
				float pos = t.Position / 1000f;
                Vector3 position = new Vector3(t.WorldX, pos, 0);
                Vector3 scale = ArcCameraManager.Instance.EditorCamera
                    ? new Vector3(1.53f, 2, 1)
                    : new Vector3(1.53f, (2f + 3.4f * Mathf.Max(0f, pos / 100f) * 1.5f) * 1.53f, 1);
                // Skin sprites face left; flip their mesh without flipping the picking/connection transform.
                t.RenderMatrix = parentMatrix * Matrix4x4.TRS(position, rotation, new Vector3(-scale.x, scale.y, scale.z));
                if (t.transform)
                {
                    t.transform.localPosition = position;
                    t.transform.localScale = scale;
                }
				t.Alpha = pos < 90 ? 1 : (100 - pos) / 10f;
			}
		}
		private void JudgeTapNotes()
		{
			ArcTimingManager timing = ArcTimingManager.Instance;
			int currentTiming = ArcGameplayManager.Instance.ChartTiming;
			foreach (var t in Taps)
			{
				if (t.NoInput())
				{
					continue;
				}
				if (t.Judged) continue;
				if (currentTiming > t.Timing && currentTiming <= t.Timing + 150)
				{
					t.Judged = true;
					if (ArcGameplayManager.Instance.IsPlaying) ArcEffectManager.Instance.PlayTapNoteEffectAt(new Vector2(t.WorldX, 0));
				}
				else if (currentTiming > t.Timing + 150)
				{
					t.Judged = true;
				}
			}
		}
		public void SetTapNoteSkin(Sprite sprite)
        {
            DefaultSprite = sprite;
            ShaderdMaterial.mainTexture = sprite.texture;
        }
		public void SetConnectionLineColor(Color color)
        {
            ArcArcManager.Instance.ConnectionColor = color;
        }
	}
}
