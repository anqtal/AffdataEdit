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
        // AffdataPlay's Arc shader applies this after the skin and note alpha.
        internal const float ArcOpacityMultiplier = 0.9f;
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
            public int Submesh, Layer, LayerValue, Order, Queue, DepthTest, DepthWrite;
            public bool SameDraw(Item other) => Mesh == other.Mesh && Texture == other.Texture && Submesh == other.Submesh
                && Layer == other.Layer && Queue == other.Queue
                && DepthTest == other.DepthTest && DepthWrite == other.DepthWrite;
        }
        private struct SortKey
        {
            public int Index, LayerValue, Order, Queue;
            public float Distance, PrimaryDistance;
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
        private readonly List<SortKey> sortKeys = new List<SortKey>();
        private readonly List<NoteInstance> instances = new List<NoteInstance>();
        private readonly List<GraphicsBuffer.IndirectDrawIndexedArgs> commands = new List<GraphicsBuffer.IndirectDrawIndexedArgs>();
        private readonly List<Draw> draws = new List<Draw>();
        private readonly Dictionary<(int,int,int), Material> materials = new Dictionary<(int,int,int), Material>();
        private MaterialPropertyBlock sourceProperties;
        private readonly Plane[] frustum = new Plane[6];
        private readonly Dictionary<Mesh, Bounds> meshBounds = new Dictionary<Mesh, Bounds>();
        private readonly Dictionary<string, (int id, int value)> sortingLayers = new Dictionary<string, (int, int)>();
        private Vector3 cameraPosition, cameraForward;
        private bool orthographic;
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
            items.Clear(); sortKeys.Clear(); instances.Clear(); commands.Clear();
            meshBounds.Clear(); sortingLayers.Clear();
            cameraForFrame = ArcCameraManager.Instance ? ArcCameraManager.Instance.GameplayCamera : null;
            if (!cameraForFrame || !ArcGameplayManager.Instance.IsLoaded) return;
            GeometryUtility.CalculateFrustumPlanes(cameraForFrame, frustum);
            cameraPosition = cameraForFrame.transform.position;
            cameraForward = cameraForFrame.transform.forward;
            orthographic = cameraForFrame.orthographic;
            foreach (var tap in ArcTapNoteManager.Instance.Taps)
            {
                if (!tap.Enable) continue;
                SubmitGroundNote(tap.RenderMatrix, ArcTapNoteManager.Instance.DefaultSprite,
                    tap.Selected, tap.Alpha, 0, 1, 0, ArcTapNoteManager.Instance.ShaderdMaterial.renderQueue);
                SubmitConnections(tap);
            }
            foreach (var hold in ArcHoldNoteManager.Instance.Holds)
                if (hold.Enable) SubmitGroundNote(hold.RenderMatrix,
                    hold.Highlight ? ArcHoldNoteManager.Instance.HighlightSprite : ArcHoldNoteManager.Instance.DefaultSprite,
                    hold.Selected, hold.Alpha, hold.From, hold.To, 1, ArcHoldNoteManager.Instance.HoldNoteMatrial.renderQueue);
            foreach (var arc in ArcArcManager.Instance.RenderingArcs)
                if (arc.Enable && arc.arcRenderer) arc.arcRenderer.Submit(this);
            foreach (var tap in ArcArcManager.Instance.RenderingArcTaps) SubmitArcTap(tap);
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

        private void SubmitConnections(ArcTap tap)
        {
            if (tap.ConnectedArcTaps.Count == 0) return;
            // Undo the skin's X flip and width scale, as the former connection prefab did.
            Matrix4x4 parent = tap.RenderMatrix * Matrix4x4.Scale(new Vector3(-1f / 1.53f, 1, 1));
            Vector3 start = parent.MultiplyPoint3x4(Vector3.zero);
            Color color = ArcArcManager.Instance.ConnectionColor;
            color.a = tap.Alpha * 0.8f;
            foreach (var arcTap in tap.ConnectedArcTaps)
            {
                Vector2 position = arcTap.GetConnectionPosition();
                Vector3 end = parent.MultiplyPoint3x4(new Vector3(position.x - tap.WorldX, 0, position.y));
                Vector3 direction = end - start;
                float length = direction.magnitude;
                if (length <= 0.000001f) continue;
                Vector3 up = Mathf.Abs(direction.y) > length * 0.999f ? Vector3.forward : Vector3.up;
                var data = NoteInstance.Create(Matrix4x4.TRS(start, Quaternion.LookRotation(direction, up), new Vector3(1, 1, length)), 0);
                data.HighColor = color;
                Submit(ArcNoteMeshes.Connection, null, data, "Arc", 3);
            }
        }

        private void SubmitGroundNote(Matrix4x4 matrix, Sprite sprite, bool selected, float alpha,
            float from, float to, int mode, int queue)
        {
            if (!sprite) return;
            var data = NoteInstance.Create(matrix, mode, selected);
            data.Options.y = alpha;
            data.ClipHeight = new Vector4(from, to, 0, 0);
            Submit(ArcNoteMeshes.Sprite(sprite), sprite.texture, data, "Note", 0, queue);
        }

        internal void SubmitSprite(SpriteRenderer renderer, int mode, bool always = false)
        {
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.sprite) return;
            renderer.GetPropertyBlock(sourceProperties);
            var matrix = renderer.localToWorldMatrix * Matrix4x4.Scale(new Vector3(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1));
            var data = NoteInstance.Create(matrix, mode, (renderer.renderingLayerMask & ArcGameplayManager.Instance.SelectionLayerMask) != 0);
            data.HighColor = renderer.color;
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
            data.Options.y = arc.Arc.IsVoid ? 1 : ArcOpacityMultiplier;
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
            // Shared meshes and sorting layers need one native lookup per frame, not per segment.
            if (!meshBounds.TryGetValue(mesh, out var localBounds))
            {
                localBounds = mesh.bounds;
                meshBounds.Add(mesh, localBounds);
            }
            Bounds bounds = TransformBounds(localBounds, data.Transform);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds)) return;
            if (!sortingLayers.TryGetValue(layer, out var sortingLayer))
            {
                int id = SortingLayer.NameToID(layer);
                sortingLayer = (id, SortingLayer.GetLayerValueFromID(id));
                sortingLayers.Add(layer, sortingLayer);
            }
            float distance = orthographic ? Vector3.Dot(bounds.center - cameraPosition, cameraForward)
                : (bounds.center - cameraPosition).sqrMagnitude;
            sortKeys.Add(new SortKey { Index = items.Count, LayerValue = sortingLayer.value, Order = order, Queue = queue,
                Distance = distance, PrimaryDistance = layer == "Arc" ? distance : 0 });
            items.Add(new Item { Mesh = mesh, Texture = texture ? texture : Texture2D.whiteTexture, Data = data,
                Layer = sortingLayer.id, LayerValue = sortingLayer.value, Order = order, Queue = queue,
                Submesh = submesh, DepthWrite = depthWrite, DepthTest = depthTest });
        }
        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 x = matrix.MultiplyVector(new Vector3(bounds.extents.x,0,0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0,bounds.extents.y,0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0,0,bounds.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center), 2 * new Vector3(
                Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
        }
        private static int Compare(SortKey a, SortKey b)
        {
            int c = a.LayerValue.CompareTo(b.LayerValue);
            // Within the Arc layer, draw far segments first; height/color/face order breaks ties.
            if (c == 0) c = b.PrimaryDistance.CompareTo(a.PrimaryDistance);
            if (c == 0) c = a.Order.CompareTo(b.Order);
            if (c == 0) c = a.Queue.CompareTo(b.Queue);
            if (c == 0) c = b.Distance.CompareTo(a.Distance);
            return c == 0 ? a.Index.CompareTo(b.Index) : c;
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
            // Sort compact keys, keeping matrices and instance data out of the sort's copies.
            sortKeys.Sort(Compare);
            // Keep transparent order: adjacent compatible items can share a draw even across sort orders.
            for (int start = 0; start < items.Count;)
            {
                Item item = items[sortKeys[start].Index];
                int command = commands.Count;
                if (command == draws.Count) draws.Add(new Draw());
                Draw draw = draws[command];
                draw.Item = item; draw.Material = GetMaterial(item); draw.Selected = false;
                int end = start;
                do
                {
                    var next = items[sortKeys[end].Index];
                    instances.Add(next.Data); draw.Selected |= next.Data.Options.w > .5f;
                    end++;
                } while (end < items.Count && item.SameDraw(items[sortKeys[end].Index]));
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
