using UnityEngine;

namespace Arcade.Gameplay
{
    // One CPU record per sample interval; no Transform, Renderer, or per-segment Mesh.
    internal sealed class ArcSegmentData
    {
        public int FromTiming, ToTiming;
        public Vector3 FromPos, ToPos;
        public float From, To = 1, FromHeight, ToHeight, Width;
        public bool Enable, Highlight, Selected, IsTrace, UseGoldTrace;
        public Color HighColor, LowColor;
        public float Alpha
        {
            get => HighColor.a;
            set { HighColor.a = value; LowColor.a = value; }
        }
        public void BuildSegment(Vector3 from, Vector3 to, float width, int start, int end, float startHeight, float endHeight)
        {
            From = 0; To = 1;
            FromPos = from; ToPos = to; Width = width; FromTiming = start; ToTiming = end;
            FromHeight = startHeight; ToHeight = endHeight;
        }
        public void Submit(ArcNoteRenderer renderer, Matrix4x4 parent, Texture normal, Texture highlight, Texture gold, int colorId)
        {
            if (!Enable || FromPos == ToPos) return;
            var local = Matrix4x4.identity;
            local.SetColumn(0, new Vector4(Width,0,0,0));
            local.SetColumn(1, new Vector4(0,Width,0,0));
            local.SetColumn(2, (Vector4)(ToPos - FromPos));
            local.SetColumn(3, new Vector4(FromPos.x,FromPos.y,FromPos.z,1));
            var data = ArcNoteRenderer.NoteInstance.Create(parent * local, 2, Selected);
            data.HighColor = HighColor; data.LowColor = LowColor;
            data.Options.y = IsTrace ? 1 : ArcNoteRenderer.ArcOpacityMultiplier;
            // Alpha colors a whole segment by its end height.
            data.ClipHeight = new Vector4(From,To,ToHeight,ToHeight);
            if (UseGoldTrace && !Highlight) data.UvTransform = new Vector4(-.5f,1,.5f,0);
            Texture texture = Highlight ? highlight : UseGoldTrace ? gold : normal;
            if (IsTrace)
                renderer.Submit(ArcNoteMeshes.Segment, texture, data, "Arc", 0);
            else
            {
                // AffdataPlay/Alpha: endpoint world height and color, with the right face +500.
                int order = 3 * colorId + 4 * (int)(parent.MultiplyPoint3x4(ToPos).y * 100f);
                renderer.Submit(ArcNoteMeshes.Segment, texture, data, "Arc", order, submesh: 1);
                renderer.Submit(ArcNoteMeshes.Segment, texture, data, "Arc", order + 500, submesh: 2);
            }
            local.m13 = 0; local.m12 = 0;
            data.Transform = parent * local;
            data.Options = new Vector4(4,1,0,0);
            Color color = ArcSkinManager.Instance.GetShadowTint(IsTrace);
            color.a *= Alpha;
            data.HighColor = color;
            renderer.Submit(ArcNoteMeshes.Shadow, null, data, "Shadow", 0);
        }
    }
}
