using System.Collections.Generic;
using Affdata.Core.Chart;
using Affdata.Core.Geometry;
using Arcade.Gameplay.Chart;
using Arcade.Util.Misc;
using UnityEngine;
using UnityEngine.VFX;

namespace Arcade.Gameplay
{
	public class ArcArcRenderer : MonoBehaviour
	{
		public const float OffsetNormal = 0.9f;
		public const float OffsetVoid = 0.15f;

		public Material arcMaterial;
		public Material shadowMaterial;
		public GameObject SegmentPrefab;

		public MeshCollider ArcCollider, HeadCollider;
		public MeshFilter HeadFilter;
		public MeshRenderer HeadRenderer;
		public Transform Head;
		public SpriteRenderer HeightIndicatorRenderer;
		public Transform ArcCap;
		public SpriteRenderer ArcCapRenderer;
		public VisualEffect JudgeEffect;
		public Transform JudgeEffectTransform;
		public Texture2D DefaultTexture, HighlightTexture;

		private MaterialPropertyBlock headPropertyBlock;

		private Color ArcRedHigh
		{
			get { return ArcArcManager.Instance.ArcRedHigh; }
		}
		private Color ArcBlueHigh
		{
			get { return ArcArcManager.Instance.ArcBlueHigh; }
		}
		private Color ArcGreenHigh
		{
			get { return ArcArcManager.Instance.ArcGreenHigh; }
		}
		private Color ArcUnknownHigh
		{
			get { return ArcArcManager.Instance.ArcUnknownHigh; }
		}
		private Color ArcRedLow
		{
			get { return ArcArcManager.Instance.ArcRedLow; }
		}
		private Color ArcBlueLow
		{
			get { return ArcArcManager.Instance.ArcBlueLow; }
		}
		private Color ArcGreenLow
		{
			get { return ArcArcManager.Instance.ArcGreenLow; }
		}
		private Color ArcUnknownLow
		{
			get { return ArcArcManager.Instance.ArcUnknownLow; }
		}
		private Color ArcVoid
		{
			get { return ArcArcManager.Instance.ArcVoid; }
		}
		private Color ArcDesignant
		{
			get { return ArcArcManager.Instance.ArcDesignant; }
		}



		public ArcArc Arc
		{
			get
			{
				return arc;
			}
			set
			{
				arc = value;
				Build();
			}
		}
		public Color HighColor
		{
			get
			{
				return currentHighColor;
			}
			set
			{
				if (currentHighColor != value)
				{
					currentHighColor = value;
					HeadRenderer.GetPropertyBlock(headPropertyBlock);
					headPropertyBlock.SetColor(highColorShaderId, currentHighColor);
					HeadRenderer.SetPropertyBlock(headPropertyBlock);
					foreach (var s in segments)
					{
						s.HighColor = SegmentColor(currentHighColor);
					}
				}
			}
		}
		public Color LowColor
		{
			get
			{
				return currentLowColor;
			}
			set
			{
				if (currentLowColor != value)
				{
					currentLowColor = value;
					HeadRenderer.GetPropertyBlock(headPropertyBlock);
					headPropertyBlock.SetColor(lowColorShaderId, currentLowColor);
					HeadRenderer.SetPropertyBlock(headPropertyBlock);
					foreach (var s in segments)
					{
						s.LowColor = SegmentColor(currentLowColor);
					}
				}
			}
		}
		public float Alpha
		{
			get
			{
				return currentHighColor.a;
			}
			set
			{
				if (currentHighColor.a != value)
				{
					currentHighColor.a = value;
					HeadRenderer.GetPropertyBlock(headPropertyBlock);
					headPropertyBlock.SetColor(highColorShaderId, currentHighColor);
					HeadRenderer.SetPropertyBlock(headPropertyBlock);
					foreach (var s in segments)
					{
						s.Alpha = value;
					}
				}
				if (currentLowColor.a != value)
				{
					currentLowColor.a = value;
					headPropertyBlock.SetColor(lowColorShaderId, currentLowColor);
				}
			}
		}
		public bool Enable
		{
			get
			{
				return enable;
			}
			set
			{
				if (enable != value)
				{
					enable = value;
					EnableHead = value;
					EnableHeightIndicator = value;
					foreach (ArcSegmentData s in segments) s.Enable = value;
					EnableArcCap = value;
					if (!value) EnableEffect = false;
				}
			}
		}
		public bool EnableHead
		{
			get
			{
				return headEnable;
			}
			set
			{
				if (headEnable != value)
				{
					headEnable = value;
					HeadRenderer.enabled = value;
				}
			}
		}
		public bool EnableHeightIndicator
		{
			get
			{
				return heightIndicatorEnable;
			}
			set
			{
				if (heightIndicatorEnable != value)
				{
					heightIndicatorEnable = value;
					HeightIndicatorRenderer.enabled = value;
				}
			}
		}
		public bool EnableArcCap
		{
			get
			{
				return arcCapEnable;
			}
			set
			{
				if (arcCapEnable != value)
				{
					arcCapEnable = value;
					ArcCapRenderer.enabled = value;
				}
			}
		}
		public bool Highlight
		{
			get
			{
				return highlighted;
			}
			set
			{
				if (highlighted != value)
				{
					highlighted = value;
					HeadRenderer.GetPropertyBlock(headPropertyBlock);
					headPropertyBlock.SetTexture(mainTexShaderId, highlighted ? HighlightTexture : DefaultTexture);
					HeadRenderer.SetPropertyBlock(headPropertyBlock);
					foreach (var s in segments) s.Highlight = value;
				}
			}
		}
		public void ReloadSkin()
		{
			if (headPropertyBlock != null)
			{
				HeadRenderer.GetPropertyBlock(headPropertyBlock);
				headPropertyBlock.SetTexture(mainTexShaderId, highlighted ? HighlightTexture : DefaultTexture);
				HeadRenderer.SetPropertyBlock(headPropertyBlock);
			}
		}

		public void ReloadColor()
		{
			float alpha = Alpha;
			if (arc != null)
			{
				HighColor = GetColor(true);
				LowColor = GetColor(false);
			}
			Alpha = alpha;
		}
        public bool EnableEffect
        {
            get => effect;
            set
            {
                // Unregister the old color before this renderer is rebound or edited.
                if (effect && (!value || arc == null || effectColor != arc.Color))
                    ArcEffectManager.Instance.SetArcEffect(this, effectColor, false);
                effect = value && arc != null;
                if (effect)
                {
                    effectColor = arc.Color;
                    ArcEffectManager.Instance.SetArcEffect(this, effectColor, true);
                }
            }
        }

        public bool Selected
		{
			get
			{
				return selected;
			}
			set
			{
				if (selected != value)
				{
					HeadRenderer.renderingLayerMask = MaskUtil.SetMask(HeadRenderer.renderingLayerMask, ArcGameplayManager.Instance.SelectionLayerMask, value);

					foreach (var s in segments) s.Selected = value;
					selected = value;
				}
			}
		}
		public bool IsHead
		{
			get
			{
				return arc.RenderHead;
			}
		}

		private void Awake()
		{
            ArcLongNoteEffect.DisableAnchor(JudgeEffect);
            // All note geometry is sorted by ArcNoteRenderer; this group has no legacy draws.
            var sortingGroup = Head.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sortingGroup) sortingGroup.enabled = false;
            HeadFilter.sharedMesh = null;
            HeadRenderer.sharedMaterial = arcMaterial;
            HeadCollider.sharedMesh = null;
            ArcCollider.sharedMesh = null;
            HeadCollider.enabled = false;
            ArcCollider.enabled = false;
            traceBodyGoldTexture = SegmentPrefab.GetComponent<ArcArcSegmentComponent>().TraceBodyGoldTexture;
            headPropertyBlock = new MaterialPropertyBlock();
			HeadRenderer.forceRenderingOff = true;
            HeightIndicatorRenderer.forceRenderingOff = true;
            ArcCapRenderer.forceRenderingOff = true;
			HeadRenderer.sortingLayerName = "Arc";
			HeadRenderer.sortingOrder = 1;
			highColorShaderId = Shader.PropertyToID("_HighColor");
			lowColorShaderId = Shader.PropertyToID("_LowColor");
			mainTexShaderId = Shader.PropertyToID("_MainTex");
		}

		private Texture2D traceBodyGoldTexture;
		private int highColorShaderId;
		private int lowColorShaderId;
		private int mainTexShaderId;
		private int segmentCount = 0;
		private bool enable;
		private bool selected;
		private bool headEnable;
		private bool heightIndicatorEnable;
        private HeightIndicatorClipState heightIndicatorClip;
		private bool arcCapEnable;
		private bool highlighted;
		private bool effect;
        private int effectColor;
		private ArcArc arc;
		private Color currentHighColor;
		private Color currentLowColor;
		private List<ArcSegmentData> segments = new List<ArcSegmentData>();
        private readonly List<double> segmentProgress = new List<double>();
        internal int BuiltRenderEndTiming { get; private set; }
		private int zeroLengthVoidArcDisappearTime = int.MaxValue;

        private bool UsesGoldTrace => arc != null && arc.LineType == ArcLineType.TrueIsVoid
            && arc.TimingGroup != null && arc.TimingGroup.TraceBodyGold;

        private Color SegmentColor(Color color)
        {
            return UsesGoldTrace ? new Color(1, 1, 1, color.a) : color;
        }

		private Color GetColor(bool high)
		{
			if (arc.LineType == ArcLineType.TrueIsVoid)
			{
				if (arc.TraceColor.HasValue) return arc.TraceColor.Value;
				if (arc.TimingGroup != null)
                {
                    if (arc.TimingGroup.TraceBodyGold)
                    {
                        var color = Affdata.Core.Chart.ArcColors.GoldTrace;
                        return new Color32(color.R, color.G, color.B, color.A);
                    }
                    if (arc.TimingGroup.UseTraceColor) return arc.TimingGroup.TraceColor;
                }
                return ArcVoid;
			}
			else if (arc.LineType == ArcLineType.Designant)
			{
				return ArcDesignant;
			}
			else
			{
				if (arc.Color == ArcColors.BlueId)
				{
					return high ? ArcBlueHigh : ArcBlueLow;
				}
				else if (arc.Color == ArcColors.RedId)
				{
					return high ? ArcRedHigh : ArcRedLow;
				}
				else if (arc.Color == ArcColors.GreenId)
				{
					return high ? ArcGreenHigh : ArcGreenLow;
				}
				else if (arc.Color == ArcColors.GrayId)
				{
					return high ? ArcUnknownHigh : ArcUnknownLow;
				}
				else
				{
					return ArcColors.Fallback;
				}
			}
		}

        private void InstantiateSegment(int quantity)
        {
            while (segments.Count < quantity)
                segments.Add(new ArcSegmentData { Enable = Enable, Alpha = Alpha, Highlight = Highlight, Selected = Selected });
            if (segments.Count > quantity) segments.RemoveRange(quantity, segments.Count - quantity);
        }

        internal void Submit(ArcNoteRenderer renderer)
        {
            if (!enable) return;
            var matrix = transform.localToWorldMatrix;
            var gold = traceBodyGoldTexture;
            int noteKey = ArcRenderOrder.NoteKey(arc.TimingGroup?.Id ?? 0, arc.Timing, arc.EndTiming, arc.Color);
            for (int i = 0; i < segments.Count; i++)
                segments[i].Submit(renderer, matrix, DefaultTexture, HighlightTexture, gold, arc.Color,
                    ArcRenderOrder.SegmentKey(noteKey, i, arc.IsVoid), arc);
            renderer.SubmitHead(this);
            renderer.SubmitSprite(HeightIndicatorRenderer, 0, true);
            renderer.SubmitSprite(ArcCapRenderer, 0);
        }

        internal void Bind(ArcArc note)
        {
            var source = ArcArcManager.Instance.ArcNotePrefab.GetComponent<ArcArcRenderer>();
            DefaultTexture = source.DefaultTexture;
            HighlightTexture = source.HighlightTexture;
            ArcCapRenderer.sprite = source.ArcCapRenderer.sprite;
            HeightIndicatorRenderer.sprite = source.HeightIndicatorRenderer.sprite;
            JudgeEffect.SetVector4("StartColor", source.JudgeEffect.GetVector4("StartColor"));
            JudgeEffect.SetVector4("EndColor", source.JudgeEffect.GetVector4("EndColor"));
            JudgeEffect.SetTexture("Texture", source.JudgeEffect.GetTexture("Texture"));
            arc = note;
            Build();
            ReloadSkin();
        }

        internal void Unbind()
        {
            Enable = false;
            EnableEffect = false;
            arc = null;
        }

		public void Build()
		{
            heightIndicatorClip = default;
			BuildHeightIndicator();
			BuildSegments();
		}
		public void BuildHeightIndicator()
		{
			if (arc.IsVoid || arc.IsVariousSizedArctap || arc.HiddenSegments)
			{
				EnableHeightIndicator = false;
				return;
			}
            SetHeightIndicatorPosition(new Vector3(ArcAlgorithm.ArcXToWorld(arc.XStart),
                ArcAlgorithm.ArcYToWorld(arc.YStart), 0));
			HeightIndicatorRenderer.color = GetColor(true);
		}
        private void SetHeightIndicatorPosition(Vector3 point)
        {
            Sprite sprite = HeightIndicatorRenderer.sprite;
            if (!sprite) return;
            float height = point.y - OffsetNormal / 2 - NoteMeshDefinitions.HeightIndicatorFloor;
            float yScale = height / sprite.bounds.size.y;
            HeightIndicatorRenderer.transform.localPosition = new Vector3(point.x,
                NoteMeshDefinitions.HeightIndicatorFloor - sprite.bounds.min.y * yScale, point.z);
            HeightIndicatorRenderer.transform.localScale = new Vector3(
                NoteMeshDefinitions.HeightIndicatorWidth / sprite.bounds.size.x, yScale, 1);
        }
		// Off by default like Arcade Alpha: an arc body is laid out rigidly with the speed of the
		// timing at its start. On, every segment point follows later timing changes.
		public static bool AllowCrossTiming;

		public void BuildSegments()
		{
			if (arc == null) return;


			ArcTimingManager timingManager = ArcTimingManager.Instance;
			int duration = arc.EndTiming - arc.Timing;

			if (duration == 0 && (arc.IsVoid || arc.NoInput()))
			{
				zeroLengthVoidArcDisappearTime = timingManager.CalculateZeroLengthVoidArcDisappearTime(arc.Timing, arc.TimingGroup);
			}
			else
			{
				zeroLengthVoidArcDisappearTime = int.MaxValue;
			}

            BuiltRenderEndTiming = Mathf.Max(arc.EndTiming, arc.RenderEndTiming);
            int renderDuration = BuiltRenderEndTiming - arc.Timing;
            ArcSampling.FillProgress(segmentProgress, duration, renderDuration, arc.EffectiveSmoothness,
                arc.IsVoid && arc.CurveType == Arcade.Gameplay.Chart.ArcCurveType.S && !AllowCrossTiming);
            segmentCount = renderDuration == 0 && arc.XStart == arc.XEnd && arc.YStart == arc.YEnd
                ? 0 : segmentProgress.Count - 1;
			InstantiateSegment(segmentCount);

			float startVelocity = timingManager.CalculateVelocityAtTiming(arc.Timing, arc.TimingGroup);
            float SegmentZ(float offset)
            {
                if (!AllowCrossTiming) return -offset * startVelocity / 1000f;
                float time = arc.Timing + offset;
                int whole = Mathf.FloorToInt(time);
                float position = timingManager.CalculatePositionByTimingAndStart(arc.Timing, whole, arc.TimingGroup)
                    + (time - whole) * timingManager.CalculateVelocityAtTiming(whole, arc.TimingGroup);
                return -position / 1000f;
            }

			float startHeight = 0;
			float endHeight = arc.YStart;
			Vector3 start = new Vector3();
			Vector3 end = new Vector3(ArcAlgorithm.ArcXToWorld(arc.XStart),
										ArcAlgorithm.ArcYToWorld(arc.YStart));
			for (int i = 0; i < segmentCount; ++i)
			{
                float progress = (float)segmentProgress[i + 1];
                float fromOffset = (float)(segmentProgress[i] * renderDuration);
                float endOffset = (float)(segmentProgress[i + 1] * renderDuration);
                bool last = i == segmentCount - 1;
				startHeight = endHeight;
				start = end;
                endHeight = last ? arc.YEnd : ArcAlgorithm.Y(arc.YStart, arc.YEnd, progress, arc.CurveType);
                float x = last ? arc.XEnd : ArcAlgorithm.X(arc.XStart, arc.XEnd, progress, arc.CurveType);
                end = new Vector3(ArcAlgorithm.ArcXToWorld(x), ArcAlgorithm.ArcYToWorld(endHeight), SegmentZ(endOffset));
                // Extend geometry to the successor while preserving zero-duration slam timing.
                segments[i].BuildSegment(start, end, arc.IsVoid ? OffsetVoid : OffsetNormal,
                    arc.Timing + fromOffset, last ? arc.EndTiming : arc.Timing + endOffset, startHeight, endHeight);
			}
			HighColor = GetColor(true);
			LowColor = GetColor(false);
		}

		public void UpdateArc()
		{
            if (!enable) return;
            UpdateHead();
			UpdateSegments();
			UpdateHeightIndicator();
			UpdateArcCap();
		}
		private void UpdateSegments()
		{
			int currentTiming = ArcGameplayManager.Instance.ChartTiming;
			float z = arc.transform.localPosition.z;

			foreach (ArcSegmentData s in segments)
			{
                s.IsTrace = arc.IsVoid;
                s.UseGoldTrace = UsesGoldTrace;
                s.HighColor = SegmentColor(currentHighColor);
                s.LowColor = SegmentColor(currentLowColor);
				if (arc.IsVariousSizedArctap || arc.HiddenSegments)
				{
					s.Enable = false;
					continue;
				}
				if (s.ToTiming == s.FromTiming && currentTiming > zeroLengthVoidArcDisappearTime)
				{
					s.Enable = false;
					continue;
				}
				float pos = -(z + s.FromPos.z);
				float endPos = -(z + s.ToPos.z);
				if (endPos > 100 && pos < 100)
				{
					s.To = (100 - pos) / (endPos - pos);
				}
				else
				{
					s.To = 1;
				}

				if ((s.ToTiming < currentTiming && s.ToTiming != s.FromTiming) || (Mathf.Max(pos, endPos) < 0 && s.ToTiming == s.FromTiming))
				{
					if (arc.Judging || arc.IsVoid || arc.NoInput())
					{
						s.Enable = false;
						continue;
					}
					else
					{
						s.Enable = true;
						continue;
					}
				}
				if (s.FromTiming < currentTiming && s.ToTiming >= currentTiming)
				{
					s.Enable = true;
					s.Alpha = currentHighColor.a;
					if (arc.Judging || arc.IsVoid || arc.NoInput())
					{
						s.From = (z + s.FromPos.z) / (-s.ToPos.z + s.FromPos.z);
					}
					else
					{
						s.From = 0;
					}
					continue;
				}
				if (pos > 90 && pos < 100 && !arc.IsVoid)
				{
					s.Enable = true;
					s.Alpha = currentHighColor.a * (100 - pos) / 10f;
					s.From = 0;
				}
				else if (Mathf.Min(pos, endPos) > 100 || Mathf.Max(pos, endPos) < -100)
				{
					s.Enable = false;
				}
				else
				{
					s.Enable = true;
					s.Alpha = currentHighColor.a;
					s.From = 0;
				}
			}
		}
		private void UpdateHead()
		{
			if (!IsHead || arc.IsVariousSizedArctap || arc.HiddenSegments)
			{
				EnableHead = false;
				return;
			}

			int currentTiming = ArcGameplayManager.Instance.ChartTiming;
			if (arc.Timing == arc.EndTiming && currentTiming > zeroLengthVoidArcDisappearTime)
			{
				EnableHead = false;
				return;
			}


			if (arc.Position > 100000 || arc.Position < -100000)
			{
				EnableHead = false;
				return;
			}
			EnableHead = true;
			if (arc.Position > 90000 && arc.Position <= 100000)
			{
				Head.localPosition = new Vector3();
				HeadRenderer.sharedMaterial = arcMaterial;
				Color highC = currentHighColor;
				highC.a = currentHighColor.a * (100000 - arc.Position) / 100000;
				Color lowC = currentLowColor;
				lowC.a = currentLowColor.a * (100000 - arc.Position) / 100000;
				HeadRenderer.GetPropertyBlock(headPropertyBlock);
				headPropertyBlock.SetColor(highColorShaderId, highC);
				headPropertyBlock.SetColor(lowColorShaderId, lowC);
				HeadRenderer.SetPropertyBlock(headPropertyBlock);
			}
			if ((arc.Timing < currentTiming && arc.Timing != arc.EndTiming) || (Arc.Position < 0 && arc.Timing == arc.EndTiming))
			{
				HeadRenderer.GetPropertyBlock(headPropertyBlock);
				headPropertyBlock.SetColor(highColorShaderId, currentHighColor);
				headPropertyBlock.SetColor(lowColorShaderId, currentLowColor);
				HeadRenderer.SetPropertyBlock(headPropertyBlock);
				if (arc.Judging || arc.IsVoid || arc.NoInput())
				{
					if (segmentCount >= 1)
					{
						ArcSegmentData s = segments[0];
						float duration = s.ToTiming - s.FromTiming;
						if (duration == 0)
						{
							EnableHead = false;
							return;
						}
						if (s.ToTiming < currentTiming)
						{
							EnableHead = false;
							return;
						}
						float t = duration == 0 ? 0 : ((-arc.Position / 1000f) / (-s.ToPos.z));
						if (float.IsNaN(t))
						{
							t = duration == 0 ? 0 : ((float)(currentTiming - s.FromTiming)) / ((float)(duration));
						}
						if (t > 1)
						{
							EnableHead = false;
							return;
						}
						else if (t < 0) t = 0;
						Head.localPosition = (s.ToPos - s.FromPos) * t;
					}
				}
				else
				{
					Head.localPosition = new Vector3();
				}
			}
			else
			{
				HeadRenderer.GetPropertyBlock(headPropertyBlock);
				headPropertyBlock.SetColor(highColorShaderId, currentHighColor);
				headPropertyBlock.SetColor(lowColorShaderId, currentLowColor);
				HeadRenderer.SetPropertyBlock(headPropertyBlock);
				Head.localPosition = new Vector3();
			}
		}
		private void UpdateHeightIndicator()
		{
			if (arc.IsVoid || (arc.YEnd == arc.YStart && !IsHead) || arc.IsVariousSizedArctap || arc.HiddenSegments || segments.Count == 0)
			{
				EnableHeightIndicator = false;
				return;
			}

            int currentTiming = ArcGameplayManager.Instance.ChartTiming;
            float z = arc.transform.localPosition.z;
            Vector3 judgementPoint = segments[0].FromPos;
            foreach (ArcSegmentData segment in segments)
            {
                float fromZ = z + segment.FromPos.z;
                float toZ = z + segment.ToPos.z;
                float t = fromZ / (fromZ - toZ);
                if (float.IsNaN(t)) t = 0;
                judgementPoint = Vector3.Lerp(segment.FromPos, segment.ToPos, t);
                if (t >= 0 && t <= 1) break;
            }
            bool clipping = (arc.Judging || arc.NoInput()) && arc.Timing < currentTiming;
            float velocity = ArcTimingManager.Instance.CalculateVelocityAtTiming(arc.Timing, arc.TimingGroup);
            float firstEndZ = z + segments[0].ToPos.z;
            if (!heightIndicatorClip.TryGetPosition(currentTiming, currentTiming > arc.Timing, clipping, velocity,
                segments[0].FromPos + Vector3.forward * z, firstEndZ,
                judgementPoint, out Vector3 position))
            {
                EnableHeightIndicator = false;
                return;
            }
            position.z -= z;
            SetHeightIndicatorPosition(position);
            EnableHeightIndicator = true;
            HeightIndicatorRenderer.color = HeightIndicatorStyle.GetColor(GetColor(true), Alpha,
                firstEndZ, arc.Timing == arc.EndTiming);
		}
		private void UpdateArcCap()
		{
			int currentTiming = ArcGameplayManager.Instance.ChartTiming;
			int duration = arc.EndTiming - arc.Timing;

			if (arc.IsVariousSizedArctap)
			{
				EnableArcCap = false;
				return;
			}

			Vector3 arcCapScaleFactor = new Vector3(256 / ArcCapRenderer.sprite.rect.width, 256 / ArcCapRenderer.sprite.rect.height, 1);

			if (arc.Timing < currentTiming && arc.EndTiming >= currentTiming)
			{
				EnableArcCap = true;
				ArcCapRenderer.color = new Color(1, 1, 1, arc.IsVoid ? 0.5f : 1f);
				ArcCap.localScale = Vector3.Scale(new Vector3(arc.IsVoid ? 0.21f : 0.35f, arc.IsVoid ? 0.21f : 0.35f), arcCapScaleFactor);
				ArcCapRenderer.sortingOrder = -1;

				foreach (var s in segments)
				{
					if (s.FromTiming < currentTiming && s.ToTiming >= currentTiming)
					{
						float t = (s.FromPos.z - arc.Position / 1000f) / (s.FromPos.z - s.ToPos.z);
						if (float.IsNaN(t))
						{
							t = ((float)(currentTiming - s.FromTiming)) / ((float)(s.ToTiming - s.FromTiming));
						}
						ArcCap.position = new Vector3(s.FromPos.x + (s.ToPos.x - s.FromPos.x) * t,
													  s.FromPos.y + (s.ToPos.y - s.FromPos.y) * t);
						JudgeEffectTransform.position = ArcEffectManager.Instance.EffectPlane.GetPositionOnPlane(ArcCap.position);
						if (!arc.IsVoid) ArcArcManager.Instance.ArcJudgePos += ArcCap.position.x;
						break;
					}
				}
			}
			else if (arc.Timing >= currentTiming && IsHead && !arc.IsVoid)
			{
				float p = 1 - Mathf.Abs(arc.Position) / 100000;
				float scale = 0.35f + 0.5f * (1 - p);
				EnableArcCap = true;
				ArcCapRenderer.color = new Color(1, 1, 1, p);
				ArcCap.localScale = Vector3.Scale(new Vector3(scale, scale), arcCapScaleFactor);
				ArcCap.position = new Vector3(ArcAlgorithm.ArcXToWorld(arc.XStart), ArcAlgorithm.ArcYToWorld(arc.YStart));
				JudgeEffectTransform.position = ArcCap.position;
				//TODO: arccap position should be decided by z instead of time
				ArcCapRenderer.sortingOrder = arc.Position < 0 ? -1 : 2;
			}
			else
			{
				EnableArcCap = false;
			}
		}


	}
}
