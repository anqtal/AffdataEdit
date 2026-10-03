using System.Collections.Generic;
using Arcade.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Arcade.Util.Rendering
{
    // Replaces the renderer asset's transparent pass (its transparent layer mask is zero).
    // Indirect mesh draws have no effective SortingGroup, so interleave them explicitly
    // with the existing track, editor guides and effects in sorting-layer order.
    public sealed class NoteLayerRendering : ScriptableRendererFeature
    {
        private LayerPass pass;
        public override void Create() => pass = new LayerPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) => renderer.EnqueuePass(pass);

        private sealed class LayerPass : ScriptableRenderPass
        {
            private readonly List<ShaderTagId> tags = new List<ShaderTagId> {
                new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };
            private sealed class PassData
            {
                public RendererListHandle Renderers;
                public Camera Camera;
                public int Layer;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
            {
                var resources = context.Get<UniversalResourceData>();
                var camera = context.Get<UniversalCameraData>();
                var rendering = context.Get<UniversalRenderingData>();
                var lights = context.Get<UniversalLightData>();
                var settings = RenderingUtils.CreateDrawingSettings(tags, rendering, camera, lights, SortingCriteria.CommonTransparent);
                foreach (var layer in SortingLayer.layers)
                {
                    var filter = new FilteringSettings(RenderQueueRange.transparent, -1) {
                        sortingLayerRange = new SortingLayerRange((short)layer.value, (short)layer.value) };
                    using (var builder = graph.AddRasterRenderPass<PassData>("Transparent " + layer.name, out var data))
                    {
                        data.Renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, settings, filter));
                        data.Camera = camera.camera; data.Layer = layer.value;
                        builder.UseRendererList(data.Renderers);
                        // A layer can contain only indirect notes and an empty renderer list.
                        builder.AllowPassCulling(false);
                        builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext graphContext) => {
                            graphContext.cmd.DrawRendererList(passData.Renderers);
                            ArcNoteRenderer.DrawLayer(graphContext.cmd, passData.Camera, passData.Layer);
                        });
                    }
                }
            }
        }
    }
}
