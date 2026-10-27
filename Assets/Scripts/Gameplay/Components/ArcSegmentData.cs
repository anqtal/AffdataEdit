using Affdata.Core.Chart;
using UnityEngine;

namespace Arcade.Gameplay
{
    // One CPU record per sample interval; no Transform, Renderer, or per-segment Mesh.
    internal sealed class ArcSegmentData
    {
        public float FromTiming, ToTiming;
        public Vector3 FromPos, ToPos;
        public float From, To = 1, FromHeight, ToHeight, Width;
        public bool Enable, Highlight, Selected, IsTrace, UseGoldTrace;
        public Color HighColor, LowColor;
        public bool TraceHidden, DrawShadow = true;
        public Vector3? TraceClippedStart;
        public float Alpha
        {
            get => HighColor.a;
            set { HighColor.a = value; LowColor.a = value; }
        }
        public void BuildSegment(Vector3 from, Vector3 to, float width, float start, float end, float startHeight, float endHeight)
        {
            From = 0; To = 1;
            FromPos = from; ToPos = to; Width = width; FromTiming = start; ToTiming = end;
            FromHeight = startHeight; ToHeight = endHeight;
            TraceHidden = false; TraceClippedStart = null; DrawShadow = true;
        }
        public void Submit(ArcNoteRenderer renderer, Matrix4x4 parent, Texture normal, Texture highlight, Texture gold,
            int colorId, int sortKey, Arcade.Gameplay.Chart.ArcNote note)
        {
            Vector3 start = IsTrace ? TraceClippedStart ?? FromPos : FromPos;
            if (!Enable || start == ToPos) return;
            var local = Matrix4x4.identity;
            local.SetColumn(0, new Vector4(Width,0,0,0));
            local.SetColumn(1, new Vector4(0,Width,0,0));
            local.SetColumn(2, (Vector4)(ToPos - start));
            local.SetColumn(3, new Vector4(start.x,start.y,start.z,1));
            var data = ArcNoteRenderer.NoteInstance.Create(parent * local, 2, Selected);
            data.HighColor = HighColor; data.LowColor = LowColor;
            data.Options.y = IsTrace ? 1 : ArcNoteRenderer.ArcOpacityMultiplier;
            // Alpha colors a whole segment by its end height.
            data.ClipHeight = new Vector4(From,To,ToHeight,ToHeight);
            if (UseGoldTrace && !Highlight) data.UvTransform = new Vector4(-.5f,1,.5f,0);
            Texture texture = Highlight ? highlight : UseGoldTrace ? gold : normal;
            // Trace and solid Arc halves interleave in the same order as Play.
            int order = ArcRenderOrder.SegmentOrder(colorId, ToPos.y);
            renderer.Submit(ArcNoteMeshes.Segment, texture, data, "Arc", order,
                submesh: 1, arcSortKey: sortKey, note: note);
            renderer.Submit(ArcNoteMeshes.Segment, texture, data, "Arc", order + ArcRenderOrder.RightHalfOffset,
                submesh: 2, arcSortKey: sortKey, note: note);
            if (!DrawShadow) return;
            local.m13 = 0; local.m12 = 0;
            // Core supplies the .05 floor offset; Arc width must not scale that height.
            local.m11 = 1;
            data.Transform = parent * local;
            data.Options = new Vector4(4,1,0,0);
            Color color = ArcSkinManager.Instance.GetShadowTint(IsTrace);
            color.a *= Alpha;
            data.HighColor = color;
            renderer.Submit(ArcNoteMeshes.Shadow, null, data, "Shadow", 0);
        }
    }
}
