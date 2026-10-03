using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arcade.Gameplay
{
    // Managers retain editor state and visibility decisions. Only visible geometry reaches these buffers.
    [DefaultExecutionOrder(32000)]
    public sealed class ArcNoteRenderer : MonoBehaviour
    {
        internal static ArcNoteRenderer Instance { get; private set; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct NoteInstance
        {
            public Matrix4x4 Transform;
            public Vector4 HighColor, LowColor, UvTransform, ClipHeight, Options, Slide, Overlap;
            public static NoteInstance Create(Matrix4x4 transform, int mode, bool selected = false) => new NoteInstance
            {
                Transform = transform, HighColor = Color.white, LowColor = Color.white,
                UvTransform = new Vector4(1,1,0,0), ClipHeight = new Vector4(0,1,0,0),
                Options = new Vector4(mode,1,0,selected ? 1 : 0)
            };
        }
        private struct Item
        {
            public Mesh Mesh;
            public Texture Texture;
            public NoteInstance Data;
            public Bounds Bounds;
            public int Submesh, Layer, LayerValue, Order, Queue, DepthTest, DepthWrite, Sequence;
            public float Distance;
            public bool SameDraw(Item other) => Mesh == other.Mesh && Texture == other.Texture && Submesh == other.Submesh
                && Layer == other.Layer && Order == other.Order && Queue == other.Queue
                && DepthTest == other.DepthTest && DepthWrite == other.DepthWrite;
        }
        private sealed class Draw
        {
            public Item Item;
            public Material Material;
            public bool Selected;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock SelectionProperties = new MaterialPropertyBlock();
        }
        private readonly List<Item> items = new List<Item>();
        private readonly List<NoteInstance> instances = new List<NoteInstance>();
        private readonly List<GraphicsBuffer.IndirectDrawIndexedArgs> commands = new List<GraphicsBuffer.IndirectDrawIndexedArgs>();
        private readonly List<Draw> draws = new List<Draw>();
        private readonly Dictionary<(int,int,int), Material> materials = new Dictionary<(int,int,int), Material>();
        private MaterialPropertyBlock sourceProperties;
        private readonly Plane[] frustum = new Plane[6];
        private GraphicsBuffer instanceBuffer, commandBuffer;
        private Camera cameraForFrame;
        private int frame = -1;
        public int VisibleInstanceCount => instances.Count;
        public int DrawCount => commands.Count;
        private static readonly int NotesId = Shader.PropertyToID("_Notes"), OffsetId = Shader.PropertyToID("_NoteOffset"),
            SelectionId = Shader.PropertyToID("_NoteSelection"), TextureId = Shader.PropertyToID("_MainTex");

        private void Awake() { Instance = this; sourceProperties = new MaterialPropertyBlock(); }
        private void LateUpdate()
        {
            frame = Time.frameCount;
            items.Clear(); instances.Clear(); commands.Clear();
            cameraForFrame = ArcCameraManager.Instance ? ArcCameraManager.Instance.GameplayCamera : null;
            if (!cameraForFrame || !ArcGameplayManager.Instance.IsLoaded) return;
            GeometryUtility.CalculateFrustumPlanes(cameraForFrame, frustum);
            foreach (var tap in ArcTapNoteManager.Instance.Taps)
                if (tap.Enable) SubmitSprite(tap.spriteRenderer, 0, tint: false);
            foreach (var hold in ArcHoldNoteManager.Instance.Holds)
                if (hold.Enable) SubmitSprite(hold.spriteRenderer, 1, tint: false);
            foreach (var arc in ArcArcManager.Instance.Arcs)
            {
                if (arc.Enable) arc.arcRenderer.Submit(this);
                foreach (var tap in arc.ArcTaps) SubmitArcTap(tap);
                if (arc.ConvertedVariousSizedArctap != null) SubmitArcTap(arc.ConvertedVariousSizedArctap);
            }
            foreach (var slide in ArcSlideManager.Instance.Slides)
                if (slide.Instance) slide.Instance.GetComponent<ArcSlideVisual>().Submit(this);
            Upload();
        }

        private void SubmitArcTap(ArcArcTap tap)
        {
            if (!tap.Enable || !tap.ModelRenderer) return;
            var renderer = tap.ModelRenderer;
            Mesh mesh = tap.Arc.IsSfx ? ArcNoteMeshes.Sfx : ArcNoteMeshes.Cube;
            var data = NoteInstance.Create(renderer.localToWorldMatrix, 3, tap.Selected);
            data.HighColor = tap.TintColor;
            data.Options.y = tap.Alpha;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var manager = ArcArcManager.Instance;
                Material material = tap.Arc.IsSfx ? (submesh == 0 ? manager.SfxArcTapNoteMaterial : manager.SfxArcTapCoreMaterial) : manager.ArcTapMaterial;
                data.UvTransform = TextureTransform(material);
                Submit(mesh, material.mainTexture, data, "ArcTap", 0, material.renderQueue, submesh, depthWrite: 1);
            }
            SubmitSprite(tap.ShadowRenderer, 0);
        }

        internal void SubmitSprite(SpriteRenderer renderer, int mode, bool always = false, bool tint = true)
        {
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.sprite) return;
            renderer.GetPropertyBlock(sourceProperties);
            var matrix = renderer.localToWorldMatrix * Matrix4x4.Scale(new Vector3(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1));
            var data = NoteInstance.Create(matrix, mode, (renderer.renderingLayerMask & ArcGameplayManager.Instance.SelectionLayerMask) != 0);
            // Tap/Hold shaders ignore SpriteRenderer.color; the Hold prefab contains a brown tint.
            data.HighColor = tint ? renderer.color : Color.white;
            data.Options.y = ReadFloat("_Alpha", 1);
            data.ClipHeight = new Vector4(ReadFloat("_From",0), ReadFloat("_To",1),0,0);
            Submit(ArcNoteMeshes.Sprite(renderer.sprite), renderer.sprite.texture, data, renderer.sortingLayerName,
                renderer.sortingOrder, renderer.sharedMaterial.renderQueue, depthTest: always ? 8 : 4);
        }

        internal void SubmitHead(ArcArcRenderer arc)
        {
            if (!arc.EnableHead) return;
            arc.HeadRenderer.GetPropertyBlock(sourceProperties);
            float width = arc.Arc.IsVoid ? ArcArcRenderer.OffsetVoid : ArcArcRenderer.OffsetNormal;
            var local = Matrix4x4.TRS(new Vector3(ArcAlgorithm.ArcXToWorld(arc.Arc.XStart), ArcAlgorithm.ArcYToWorld(arc.Arc.YStart),0),
                Quaternion.identity, Vector3.one * width);
            var data = NoteInstance.Create(arc.Head.localToWorldMatrix * local, 2, arc.Selected);
            data.HighColor = sourceProperties.HasColor("_HighColor") ? sourceProperties.GetColor("_HighColor") : arc.HighColor;
            data.LowColor = sourceProperties.HasColor("_LowColor") ? sourceProperties.GetColor("_LowColor") : arc.LowColor;
            data.ClipHeight.z = data.ClipHeight.w = arc.Arc.YStart;
            Submit(ArcNoteMeshes.Head, arc.Highlight ? arc.HighlightTexture : arc.DefaultTexture, data, "ArcTap", 1);
        }

        internal void SubmitSlide(Mesh mesh, MeshRenderer renderer, int mode)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || mesh.vertexCount == 0) return;
            renderer.GetPropertyBlock(sourceProperties);
            var data = NoteInstance.Create(renderer.localToWorldMatrix, mode,
                (renderer.renderingLayerMask & ArcGameplayManager.Instance.SelectionLayerMask) != 0);
            if (sourceProperties.HasColor("_Color")) data.HighColor = sourceProperties.GetColor("_Color");
            else if (renderer.sharedMaterial.HasColor("_Color")) data.HighColor = renderer.sharedMaterial.GetColor("_Color");
            data.Slide = new Vector4(ReadFloat("_VisualStart",0), ReadFloat("_VisualChartProgress",0),
                ReadFloat("_IsFirstOfGroup",0), ReadFloat("_IsLastOfGroup",0));
            data.Overlap = sourceProperties.GetVector("_MissingRange");
            Submit(mesh, mode == 7 ? renderer.sharedMaterial.mainTexture : null, data,
                renderer.sortingLayerName, renderer.sortingOrder, renderer.sharedMaterial.renderQueue);
        }
        private float ReadFloat(string name, float fallback) => sourceProperties.HasFloat(name) ? sourceProperties.GetFloat(name) : fallback;
        private static Vector4 TextureTransform(Material material)
        {
            Vector2 scale = material.mainTextureScale, offset = material.mainTextureOffset;
            return new Vector4(scale.x,scale.y,offset.x,offset.y);
        }

        internal void Submit(Mesh mesh, Texture texture, NoteInstance data, string layer, int order,
            int queue = 3000, int submesh = 0, int depthWrite = 0, int depthTest = 4)
        {
            Bounds bounds = TransformBounds(mesh.bounds, data.Transform);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds)) return;
            int layerId = SortingLayer.NameToID(layer);
            items.Add(new Item { Mesh = mesh, Texture = texture ? texture : Texture2D.whiteTexture, Data = data, Bounds = bounds,
                Layer = layerId, LayerValue = SortingLayer.GetLayerValueFromID(layerId), Order = order, Queue = queue,
                Submesh = submesh, DepthWrite = depthWrite, DepthTest = depthTest, Sequence = items.Count,
                Distance = cameraForFrame.orthographic ? Vector3.Dot(bounds.center - cameraForFrame.transform.position, cameraForFrame.transform.forward)
                    : (bounds.center - cameraForFrame.transform.position).sqrMagnitude });
        }
        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 x = matrix.MultiplyVector(new Vector3(bounds.extents.x,0,0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0,bounds.extents.y,0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0,0,bounds.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center), 2 * new Vector3(
                Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
        }
        private static int Compare(Item a, Item b)
        {
            int c = a.LayerValue.CompareTo(b.LayerValue);
            if (c == 0) c = a.Order.CompareTo(b.Order);
            if (c == 0) c = a.Queue.CompareTo(b.Queue);
            if (c == 0) c = b.Distance.CompareTo(a.Distance);
            return c == 0 ? a.Sequence.CompareTo(b.Sequence) : c;
        }
        private Material GetMaterial(Item item)
        {
            var key = (item.Queue,item.DepthWrite,item.DepthTest);
            if (materials.TryGetValue(key, out var material)) return material;
            material = new Material(Resources.Load<Shader>("NoteIndirect")) { name = "Indirect notes", enableInstancing = true, renderQueue = item.Queue };
            material.SetInt("_ZWrite", item.DepthWrite); material.SetInt("_ZTest", item.DepthTest);
            material.SetTexture("_ArchTexture", Resources.Load<Texture2D>("SlideOriginalReference/architect_lines"));
            material.SetTexture("_DotTexture", Resources.Load<Texture2D>("SlideOriginalReference/dot_grid"));
            materials.Add(key, material);
            return material;
        }
        private void Upload()
        {
            if (items.Count == 0) return;
            items.Sort(Compare);
            // Keep transparent order: only adjacent compatible items share a draw.
            for (int start = 0; start < items.Count;)
            {
                Item item = items[start];
                int command = commands.Count;
                if (command == draws.Count) draws.Add(new Draw());
                Draw draw = draws[command];
                draw.Item = item; draw.Material = GetMaterial(item); draw.Selected = false;
                int end = start;
                do
                {
                    var next = items[end];
                    instances.Add(next.Data); draw.Selected |= next.Data.Options.w > .5f;
                    end++;
                } while (end < items.Count && item.SameDraw(items[end]));
                commands.Add(new GraphicsBuffer.IndirectDrawIndexedArgs {
                    indexCountPerInstance = item.Mesh.GetIndexCount(item.Submesh), instanceCount = (uint)(end-start),
                    startIndex = item.Mesh.GetIndexStart(item.Submesh), baseVertexIndex = item.Mesh.GetBaseVertex(item.Submesh), startInstance = 0 });
                draw.Properties.SetInt(OffsetId, start);
                draw.SelectionProperties.SetInt(OffsetId, start);
                start = end;
            }
            EnsureBuffer(ref instanceBuffer, GraphicsBuffer.Target.Structured, instances.Count, Marshal.SizeOf<NoteInstance>());
            EnsureBuffer(ref commandBuffer, GraphicsBuffer.Target.IndirectArguments, commands.Count, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            instanceBuffer.SetData(instances);
            commandBuffer.SetData(commands);
            for (int i = 0; i < commands.Count; i++)
            {
                var draw = draws[i];
                SetProperties(draw.Properties, draw.Item.Texture, false);
                SetProperties(draw.SelectionProperties, draw.Item.Texture, true);
            }
        }
        private void SetProperties(MaterialPropertyBlock properties, Texture texture, bool selection)
        {
            properties.SetBuffer(NotesId, instanceBuffer); properties.SetTexture(TextureId, texture);
            properties.SetFloat(SelectionId, selection ? 1 : 0);
        }
        private static void EnsureBuffer(ref GraphicsBuffer buffer, GraphicsBuffer.Target target, int count, int stride)
        {
            if (buffer != null && buffer.count >= count) return;
            buffer?.Dispose();
            buffer = new GraphicsBuffer(target, Mathf.NextPowerOfTwo(Mathf.Max(16,count)), stride);
        }
        internal static void DrawSelection(RasterCommandBuffer command, Camera camera)
        {
            var owner = Instance;
            if (!owner || owner.frame != Time.frameCount || owner.cameraForFrame != camera) return;
            for (int i = 0; i < owner.commands.Count; i++)
            {
                Draw draw = owner.draws[i];
                if (draw.Selected) command.DrawMeshInstancedIndirect(draw.Item.Mesh, draw.Item.Submesh, draw.Material, 0,
                    owner.commandBuffer, i * GraphicsBuffer.IndirectDrawIndexedArgs.size, draw.SelectionProperties);
            }
        }
        internal static void DrawLayer(RasterCommandBuffer command, Camera camera, int layer)
        {
            var owner = Instance;
            if (!owner || owner.frame != Time.frameCount || owner.cameraForFrame != camera) return;
            for (int i = 0; i < owner.commands.Count; i++)
            {
                Draw draw = owner.draws[i];
                if (draw.Item.LayerValue == layer) command.DrawMeshInstancedIndirect(draw.Item.Mesh, draw.Item.Submesh, draw.Material, 0,
                    owner.commandBuffer, i * GraphicsBuffer.IndirectDrawIndexedArgs.size, draw.Properties);
            }
        }
        private void OnDestroy()
        {
            instanceBuffer?.Dispose(); commandBuffer?.Dispose();
            foreach (var material in materials.Values) Destroy(material);
            ArcNoteMeshes.Release();
            if (Instance == this) Instance = null;
        }
    }
}
