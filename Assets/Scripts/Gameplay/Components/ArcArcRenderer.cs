using UnityEngine;
using UnityEngine.VFX;

namespace Arcade.Gameplay
{
	// Skin template on the Arc prefab. The prefab is never instantiated: each visible Arc keeps its
	// state in an ArcArcVisual, and ArcNoteRenderer draws it with these assets.
	public class ArcArcRenderer : MonoBehaviour
	{
		public const float OffsetNormal = 0.9f;
		public const float OffsetVoid = 0.15f;

		public Material arcMaterial;
		public Material shadowMaterial;
		public GameObject SegmentPrefab;

		public MeshFilter HeadFilter;
		public MeshRenderer HeadRenderer;
		public Transform Head;
		public SpriteRenderer HeightIndicatorRenderer;
		public Transform ArcCap;
		public SpriteRenderer ArcCapRenderer;
		public VisualEffect JudgeEffect;
		public Transform JudgeEffectTransform;
		public Texture2D DefaultTexture, HighlightTexture;

		// Off by default like Arcade Alpha: an arc body is laid out rigidly with the speed of the
		// timing at its start. On, every segment point follows later timing changes.
		public static bool AllowCrossTiming;

		private Texture2D traceBodyGoldTexture;
		internal Texture2D TraceBodyGoldTexture => traceBodyGoldTexture
			? traceBodyGoldTexture
			: traceBodyGoldTexture = SegmentPrefab.GetComponent<ArcArcSegmentComponent>().TraceBodyGoldTexture;
	}
}
