using System.Collections.Generic;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arcade.Gameplay
{
    public sealed class ArcSlideVisual : MonoBehaviour
    {
        public const float RenderDistance = 100f;
        public static bool IsWithinRenderDistance(float worldZ) => Mathf.Abs(worldZ) <= RenderDistance;
        private ArcSlide note;
        private Mesh mesh;
        private MeshRenderer body, shadow;
        private readonly Transform[] brackets = new Transform[2];
        private MeshCollider hitbox;
        private GameObject shadowObject;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock shadowProperties;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv0 = new List<Vector2>(), uv1 = new List<Vector2>(), uv2 = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private int lastStart = int.MinValue, signature;
        private bool lastConsumed;
        public void Initialize(ArcSlide slide)
        {
            note = slide;
            mesh = new Mesh { name = "Slide ribbon", indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            body = GetComponent<MeshRenderer>(); body.sortingLayerName = "Arc"; body.sharedMaterial = ArcSlideManager.Instance.BodyMaterial;
            body.forceRenderingOff = true;
            hitbox = GetComponent<MeshCollider>();
            properties = new MaterialPropertyBlock();
            shadowProperties = new MaterialPropertyBlock();
            shadowObject = new GameObject("Slide shadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadowObject.layer = 9;
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = new Vector3(0, -ArcAlgorithm.ArcYToWorld(1) + 0.02f, 0);
            shadowObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadow = shadowObject.GetComponent<MeshRenderer>(); shadow.forceRenderingOff = true; shadow.sortingLayerName = "Shadow"; shadow.sharedMaterial = ArcSlideManager.Instance.ShadowMaterial;
            transform.position = new Vector3(0, note.WorldHeight, 0);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Slide entry bracket", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = 9;
                go.transform.SetParent(transform, false); brackets[i] = go.transform;
                brackets[i].localScale = Vector3.one * (8.5f / 1.62f);
                brackets[i].localRotation = Quaternion.Euler(0, i == 0 ? 180 : 0, 0);
                go.GetComponent<MeshFilter>().sharedMesh = ArcSlideManager.Instance.BracketMesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = ArcSlideManager.Instance.BracketMaterial;
                go.GetComponent<MeshRenderer>().sortingLayerName = "Arc";
                go.GetComponent<MeshRenderer>().forceRenderingOff = true;
            }
        }
        public void Invalidate() => lastStart = int.MinValue;
        public void Present(int now, bool consume)
        {
            if (!note.IsValid)
            {
                body.enabled = shadow.enabled = hitbox.enabled = false;
                foreach (var bracket in brackets) bracket.gameObject.SetActive(false);
                return;
            }
            var timing = ArcTimingManager.Instance;
            float startZ = -timing.CalculatePositionByTimingAndStart(now, note.Timing, note.TimingGroup) / 1000f;
            bool visible = !note.GroupHide() && !(consume && now >= note.EndTiming);
            body.enabled = shadow.enabled = visible;
            hitbox.enabled = visible && !ArcGameplayManager.Instance.IsPlaying;
            var headRange = note.Range(0);
            for (int i = 0; i < brackets.Length; i++)
            {
                brackets[i].gameObject.SetActive(visible && IsWithinRenderDistance(startZ) && note.IsGroupHead && (long)note.Timing - now > 5);
                brackets[i].localPosition = new Vector3(note.WorldX(i == 0 ? headRange.x : headRange.y), .026f, 0);
            }
            if (!visible) return;
            // Store geometry relative to the head. Scrolling only moves the transform.
            transform.position = new Vector3(0, note.WorldHeight, startZ);
            body.renderingLayerMask = note.Selected ? ArcGameplayManager.Instance.SelectionLayerMask : 1u;
            int hash = System.HashCode.Combine(note.Timing, note.EndTiming, note.StartCenter, note.StartWidth,
                note.EndCenter, note.EndWidth, note.LeftCurve, note.RightCurve);
            hash = System.HashCode.Combine(hash, timing.BaseBpm, timing.SettingVelocity, note.IsFloor);
            foreach (var point in timing.GetTiming(note.TimingGroup))
                hash = System.HashCode.Combine(hash, point.Timing, point.Bpm);
            int start = consume ? Mathf.Max(note.Timing, now) : note.Timing;
            if (lastStart != start || signature != hash || lastConsumed != consume)
            {
                Fill(start); lastStart = start; signature = hash; lastConsumed = consume;
            }
            // Use the whole sampled mesh, not just its endpoints: reversed BPM can bring
            // a middle section into view while both endpoints remain beyond the cutoff.
            bool inRange = mesh.bounds.max.z + startZ >= -RenderDistance
                && mesh.bounds.min.z + startZ <= RenderDistance;
            body.enabled = inRange;
            shadow.enabled = inRange && !note.IsFloor;
            body.sortingLayerName = note.IsFloor ? "Note" : "Arc";
            body.sortingOrder = note.IsFloor ? -1 : 0;
            hitbox.enabled = inRange && !ArcGameplayManager.Instance.IsPlaying;
            if (!inRange) return;
            // Cooking a MeshCollider every playback frame is expensive; selection is only needed while paused.
            if (hitbox.enabled && hitbox.sharedMesh != mesh) hitbox.sharedMesh = mesh;
            float visualStart = timing.CalculatePositionByTimingAndStart(0, note.Timing, note.TimingGroup) / 1000f * (1.62f / 8.5f);
            float visualNow = timing.CalculatePositionByTimingAndStart(0, now, note.TimingGroup) / 1000f * (1.62f / 8.5f);
            properties.SetFloat("_VisualStart", visualStart); properties.SetFloat("_VisualChartProgress", visualNow);
            properties.SetFloat("_IsFirstOfGroup", note.IsGroupHead ? 1 : 0); properties.SetFloat("_IsLastOfGroup", note.IsGroupTail ? 1 : 0);
            properties.SetVector("_MissingRange", note.Overlap);
            // Darker floor fill stays readable against the pale lanes; keep source edge/glow colors.
            properties.SetColor("_Color", note.IsFloor
                ? new Color(0.42f, 0.32f, 0.70f, 1f)
                : ArcSlideManager.Instance.BodyMaterial.GetColor("_Color"));
            properties.SetFloat("_Danger", 0);
            body.SetPropertyBlock(properties);
            shadowProperties.SetColor("_Color", ArcSkinManager.Instance.GetShadowTint());
            shadow.SetPropertyBlock(shadowProperties);
        }
        private void Fill(int start)
        {
            vertices.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); indices.Clear();
            int duration = note.EndTiming - note.Timing;
            // Bound geometry for very long editor notes, while retaining the source's 5ms sampling for normal notes.
            int step = Mathf.Max(5, Mathf.CeilToInt(duration / 8192f));
            float length = ArcTimingManager.Instance.CalculatePositionByTimingAndStart(note.Timing, note.EndTiming, note.TimingGroup) / 1000f * (1.62f / 8.5f);
            for (int t = start; t < note.EndTiming;)
            {
                int next = (int)System.Math.Min((long)t + step, note.EndTiming);
                int i = vertices.Count;
                AddRow(t); AddRow(next);
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 1);
                indices.Add(i + 1); indices.Add(i + 2); indices.Add(i + 3);
                t = next;
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetUVs(2, uv2);
            mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            hitbox.sharedMesh = null;
            void AddRow(int time)
            {
                float progress = (time - note.Timing) / (float)duration;
                Vector2 range = note.Range(progress);
                float z = -ArcTimingManager.Instance.CalculatePositionByTimingAndStart(note.Timing, time, note.TimingGroup) / 1000f;
                float distance = ArcTimingManager.Instance.CalculatePositionByTimingAndStart(note.Timing, time, note.TimingGroup) / 1000f * (1.62f / 8.5f);
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? range.x : range.y;
                    vertices.Add(new Vector3(note.WorldX(x), 0, z));
                    uv0.Add(new Vector2(x, progress)); uv1.Add(new Vector2(side, distance));
                    uv2.Add(new Vector2(range.y - range.x, length));
                }
            }
        }
        internal void Submit(ArcNoteRenderer renderer)
        {
            renderer.SubmitSlide(mesh, body, 6);
            renderer.SubmitSlide(mesh, shadow, 8);
            foreach (var bracket in brackets)
                renderer.SubmitSlide(ArcNoteMeshes.Bracket, bracket.GetComponent<MeshRenderer>(), 7);
        }
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
