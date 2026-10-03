using System.Collections.Generic;
using Arcade.Gameplay.Chart;
using UnityEngine;

namespace Arcade.Gameplay
{
    public sealed class ArcSlideManager : MonoBehaviour
    {
        public static ArcSlideManager Instance { get; private set; }
        public List<ArcSlide> Slides { get; private set; } = new List<ArcSlide>();
        public Material BodyMaterial { get; private set; }
        public Material ShadowMaterial { get; private set; }
        public Material BracketMaterial { get; private set; }
        public Mesh BracketMesh { get; private set; }
        private Material glowMaterial, floorGlowMaterial, gridMaterial, blurMaterial;
        private sealed class Feedback
        {
            public GameObject Object;
            public Renderer Renderer;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            public float Opacity;
            public Vector2 Range;
        }
        private readonly Dictionary<ArcSlide, Feedback> feedback = new Dictionary<ArcSlide, Feedback>();
        private readonly Dictionary<ArcSlide, ArcSlide> activeSegments = new Dictionary<ArcSlide, ArcSlide>();
        private readonly List<Vector2> activeRanges = new List<Vector2>();
        private readonly List<Vector2> floorRanges = new List<Vector2>();
        private ArcSlideJudgementGrid grid, floorGrid;
        private int previousTime = int.MinValue;
        private bool previousPlaying;

        private void Awake()
        {
            Instance = this;
            BodyMaterial = Make("SpcSkyArea");
            BodyMaterial.SetTexture("_ArchTexture", Resources.Load<Texture2D>("SlideOriginalReference/architect_lines"));
            BodyMaterial.SetTexture("_DotTexture", Resources.Load<Texture2D>("SlideOriginalReference/dot_grid"));
            ShadowMaterial = Make("SlideShadow");
            BracketMaterial = Make("SlideBracket");
            BracketMaterial.SetTexture("_MainTex", Resources.Load<Texture2D>("SlideOriginalReference/square_bracket"));
            BracketMesh = ArcNoteMeshes.Bracket;
            glowMaterial = Make("SpcSkyLineGlow");
            gridMaterial = Make("SpcJudgementGrid");
            blurMaterial = Make("SpcBlur");
            grid = new ArcSlideJudgementGrid();
            grid.Build(gridMaterial, blurMaterial, 1024, 576);
            glowMaterial.SetTexture("_DownscaledGrid", grid.SharpTexture);
            glowMaterial.SetTexture("_DownscaledBlurredGrid", grid.BlurredTexture);
            floorGrid = new ArcSlideJudgementGrid();
            floorGrid.Build(gridMaterial, blurMaterial, 1024, 576);
            floorGlowMaterial = Make("SpcSkyLineGlow");
            floorGlowMaterial.SetTexture("_DownscaledGrid", floorGrid.SharpTexture);
            floorGlowMaterial.SetTexture("_DownscaledBlurredGrid", floorGrid.BlurredTexture);
            gameObject.AddComponent<Arcade.Compose.UI.AdeSlideEditor>();
        }
        private static Material Make(string name)
        {
            var shader = Resources.Load<Shader>("SlideOriginalReference/" + name);
            if (!shader) throw new System.InvalidOperationException("Missing Slide shader: " + name);
            return new Material(shader) { name = "Slide " + name };
        }
        public void Load(List<ArcSlide> slides)
        {
            Clean(); Slides = slides;
            foreach (var slide in Slides) slide.Instantiate();
            RebuildGroups();
        }
        public void Add(ArcSlide slide) { Slides.Add(slide); slide.Instantiate(); RebuildGroups(); }
        public void Remove(ArcSlide slide)
        {
            Slides.Remove(slide); slide.Destroy(); RebuildGroups();
            if (feedback.TryGetValue(slide, out var hit)) { Destroy(hit.Object); feedback.Remove(slide); }
        }
        public void Clean()
        {
            foreach (var slide in Slides) slide.Destroy();
            Slides = new List<ArcSlide>(); ResetFeedback();
            foreach (var hit in feedback.Values) if (hit.Object) Destroy(hit.Object);
            feedback.Clear();
        }
        public void ResetFeedback()
        {
            previousTime = int.MinValue; previousPlaying = false; grid?.ResetState(); floorGrid?.ResetState(); activeRanges.Clear(); floorRanges.Clear();
            foreach (var hit in feedback.Values) { hit.Opacity = 0; if (hit.Object) hit.Object.SetActive(false); }
        }
        private Feedback CreateFeedback()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.layer = 9; go.name = "Slide judgement glow"; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, ArcAlgorithm.ArcYToWorld(1), -0.01f);
            go.transform.localScale = new Vector3(8.5f / 0.8f, 0.75f, 1);
            go.transform.rotation = Quaternion.Euler(0, 180, 0);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = glowMaterial; renderer.sortingLayerName = "Effect";
            return new Feedback { Object = go, Renderer = renderer };
        }
        private void LateUpdate()
        {
            var game = ArcGameplayManager.Instance;
            if (!game || !game.IsLoaded) return;
            int now = game.ChartTiming;
            bool discontinuity = previousTime == int.MinValue || now < previousTime || (long)now - previousTime > 250;
            if (discontinuity) ResetFeedback();
            activeRanges.Clear(); floorRanges.Clear(); activeSegments.Clear();
            foreach (var slide in Slides)
            {
                if (!slide.Instance) slide.Instantiate();
                bool hit = slide.IsValid && game.Auto && !slide.NoInput() && !slide.GroupHide()
                    && now >= slide.Timing && now < slide.EndTiming;
                slide.Judging = hit;
                // One sound at a group's head, including short notes crossed in one frame.
                // Paused scrubbing and discontinuous seeks must never produce hit sounds.
                if (game.Auto && slide.IsValid && slide.IsGroupHead && !slide.NoInput() && !slide.GroupHide()
                    && ShouldPlayHeadSound(slide.Timing, previousTime, now, previousPlaying, game.IsPlaying))
                {
                    if (slide.IsFloor) ArcEffectManager.Instance.PlayTapSound();
                    else ArcEffectManager.Instance.PlayArcSound();
                }
                slide.Instance.GetComponent<ArcSlideVisual>().Present(now, game.Auto && !slide.NoInput());
                if (hit)
                {
                    var root = slide.GroupRoot;
                    if (!activeSegments.TryGetValue(root, out var current) || slide.Timing > current.Timing)
                        activeSegments[root] = slide;
                }
            }
            foreach (var pair in activeSegments)
            {
                if (!feedback.ContainsKey(pair.Key)) feedback.Add(pair.Key, CreateFeedback());
            }
            foreach (var pair in feedback)
            {
                bool hit = activeSegments.TryGetValue(pair.Key, out var active);
                var slide = active ?? pair.Key;
                var effect = pair.Value;
                if (hit)
                {
                    effect.Range = slide.Range((now - slide.Timing) / (float)(slide.EndTiming - slide.Timing));
                    effect.Opacity = 1; (slide.IsFloor ? floorRanges : activeRanges).Add(effect.Range);
                }
                if (effect == null) continue;
                effect.Object.transform.position = new Vector3(0, slide.WorldHeight + (slide.IsFloor ? .025f : 0), -.01f);
                effect.Object.transform.localScale = new Vector3(slide.WorldWidth * 2f / .8f, slide.IsFloor ? 1f : .75f, 1);
                effect.Object.transform.rotation = Quaternion.Euler(slide.IsFloor ? 90 : 0, 180, 0);
                effect.Renderer.sharedMaterial = slide.IsFloor ? floorGlowMaterial : glowMaterial;
                if (!hit) effect.Opacity = game.IsPlaying && !slide.GroupHide() && !slide.NoInput()
                    ? Mathf.Max(0, effect.Opacity - Time.deltaTime / 0.3f) : 0;
                effect.Object.SetActive(effect.Opacity > 0);
                effect.Properties.SetFloat("_Opacity", effect.Opacity);
                effect.Properties.SetFloat("_CenterX", (effect.Range.x + effect.Range.y) / 2);
                effect.Properties.SetFloat("_Width", effect.Range.y - effect.Range.x);
                effect.Renderer.SetPropertyBlock(effect.Properties);
            }
            grid.SetSkyRanges(activeRanges);
            grid.SetPresentationClock(Mathf.Max(0, now / 1000f));
            grid.UpdateFrame(game.IsPlaying && !discontinuity ? Mathf.Max(0, now - previousTime) / 1000f : 0);
            floorGrid.SetSkyRanges(floorRanges);
            floorGrid.SetPresentationClock(Mathf.Max(0, now / 1000f));
            floorGrid.UpdateFrame(game.IsPlaying && !discontinuity ? Mathf.Max(0, now - previousTime) / 1000f : 0);
            previousTime = now;
            previousPlaying = game.IsPlaying;
        }

        public static bool ShouldPlayHeadSound(int start, int previous, int now, bool wasPlaying, bool playing)
        {
            if (!playing || previous == int.MinValue || now < previous || (long)now - previous > 250)
                return false;
            return previous < start && now >= start || !wasPlaying && previous == start && now >= start;
        }
        public void RebuildGroups()
        {
            UpdateConnections(Slides);
            ResetFeedback();
            foreach (var hit in feedback.Values) if (hit.Object) Destroy(hit.Object);
            feedback.Clear();
        }

        public const int ContinuityToleranceMilliseconds = 2;
        public static void UpdateConnections(IReadOnlyList<ArcSlide> slides)
        {
            // AFF saves in time order. Use stable time order here too so saving/reloading
            // cannot change group membership. Each plane/timing group has its own chain.
            var ordered = new List<(ArcSlide note, int index)>();
            for (int i = 0; i < slides.Count; i++)
            {
                var slide = slides[i];
                slide.GroupRoot = slide;
                slide.IsGroupHead = slide.IsGroupTail = true;
                slide.Overlap = new Vector4(0, 1, 0, 1);
                if (slide.IsValid) ordered.Add((slide, i));
            }
            ordered.Sort((a, b) => a.note.Timing != b.note.Timing
                ? a.note.Timing.CompareTo(b.note.Timing) : a.index.CompareTo(b.index));
            var previous = new Dictionary<(ArcTimingGroup, bool), ArcSlide>();
            foreach (var item in ordered)
            {
                var current = item.note;
                var key = (current.TimingGroup, current.IsFloor);
                if (previous.TryGetValue(key, out var before))
                {
                    // Logical continuity does not require horizontally overlapping ranges.
                    if (System.Math.Abs((long)current.Timing - before.EndTiming) <= ContinuityToleranceMilliseconds)
                    {
                        current.GroupRoot = before.GroupRoot;
                        before.IsGroupTail = current.IsGroupHead = false;
                    }
                    // Source rendering treats exact seams independently of the 2ms tolerance.
                    if (before.EndTiming == current.Timing)
                    {
                        var a = before.Range(1); var b = current.Range(0);
                        float left = Mathf.Max(a.x, b.x), right = Mathf.Min(a.y, b.y);
                        if (left > right) left = right = 0;
                        before.Overlap.z = current.Overlap.x = left;
                        before.Overlap.w = current.Overlap.y = right;
                    }
                }
                previous[key] = current;
            }
        }
        private void OnDestroy()
        {
            Clean(); grid?.Dispose(); floorGrid?.Dispose();
            foreach (var material in new[] { BodyMaterial, ShadowMaterial, BracketMaterial, glowMaterial, floorGlowMaterial, gridMaterial, blurMaterial })
                if (material) Destroy(material);
            if (Instance == this) Instance = null;
        }
    }
}
