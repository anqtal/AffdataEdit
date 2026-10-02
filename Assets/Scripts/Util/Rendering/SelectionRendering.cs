using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;


namespace Arcade.Util.Rendering
{

	public class SelectionRendering : ScriptableRendererFeature
	{
		class SelectionPass : ScriptableRenderPass
		{
			public static readonly int SampleDistanceShaderId = Shader.PropertyToID("_SampleDistance");
			public static readonly int BlitTextureSizeShaderId = Shader.PropertyToID("_BlitTextureSize");
			public static readonly int SelectionColorShaderId = Shader.PropertyToID("_SelectionColor");
			public static readonly int OutlineShaderId = Shader.PropertyToID("_OutlineColor");
			public SelectionRendering feature;
			private List<ShaderTagId> shaderTagIdList = new List<ShaderTagId> {
				new ShaderTagId("UniversalForward"),
				new ShaderTagId("UniversalForwardOnly"),
				new ShaderTagId("LightweightForward"),
				new ShaderTagId("SRPDefaultUnlit")
			};

			public Material selectionBlitMaterial;

			private static readonly ShaderTagId SelectionShaderTagId = new ShaderTagId("Selection");

			public SelectionPass(SelectionRendering feature)
			{
				this.feature = feature;
				this.selectionBlitMaterial = CoreUtils.CreateEngineMaterial(feature.SelectionBlitShader);
				profilingSampler = new ProfilingSampler("SelectionPass");
			}

			class MaskPassData
			{
				public RendererListHandle objectsInSelection;
				public Rect viewport;
			}
			class BlitPassData
			{
				public TextureHandle src;
				public Rect viewport;
				public Material material;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
			{
				UniversalRenderingData renderingData = frameContext.Get<UniversalRenderingData>();
				UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
				UniversalLightData lightData = frameContext.Get<UniversalLightData>();
				SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
				// TODO: Use a specific shader pass to draw selection buffer
				DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
					shaderTagIdList, renderingData, cameraData, lightData, sortFlags
				);
				RendererListParams rendererListParams = new RendererListParams(
					renderingData.cullResults,
					drawingSettings,
					new FilteringSettings(RenderQueueRange.all, -1, feature.RenderingLayerMask)
				);
				RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
				descriptor.colorFormat = RenderTextureFormat.ARGB32;
				descriptor.depthBufferBits = 32;
				TextureHandle selectionMaskDepth = UniversalRenderer.CreateRenderGraphTexture(
					renderGraph, descriptor, "selection depth", false
				);
				descriptor.depthBufferBits = 0;
				TextureHandle selectionMaskColor = UniversalRenderer.CreateRenderGraphTexture(
					renderGraph, descriptor, "selection color", false
				);

				UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();
				TextureHandle activeColorTexture = resourceData.activeColorTexture;

				using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Selection Mask", out var passData))
				{
					passData.objectsInSelection = renderGraph.CreateRendererList(rendererListParams);
					// URP's intermediate descriptor already has the camera viewport dimensions.
					passData.viewport = new Rect(0, 0, descriptor.width, descriptor.height);

					builder.UseRendererList(passData.objectsInSelection);
					builder.SetRenderAttachment(selectionMaskColor, 0);
					builder.SetRenderAttachmentDepth(selectionMaskDepth, AccessFlags.ReadWrite);
					builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) => ExecuteMaskPass(data, context));
				}

				selectionBlitMaterial.SetFloat(SampleDistanceShaderId, feature.SampleDistance * ((float)descriptor.width) / feature.SampleDistanceReferenceWidth);
				selectionBlitMaterial.SetVector(BlitTextureSizeShaderId, new Vector4(descriptor.width, descriptor.height));
				selectionBlitMaterial.SetColor(SelectionColorShaderId, feature.SelectionColor);
				selectionBlitMaterial.SetColor(OutlineShaderId, feature.OutlineColor);

				using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Selection Outline", out var passData))
				{
					passData.src = selectionMaskColor;
					// After the final blit, the destination is the full target again. Place the
					// viewport-sized mask in the same pixel rectangle as the camera's image.
					passData.viewport = resourceData.isActiveTargetBackBuffer
						? cameraData.camera.pixelRect : new Rect(0, 0, descriptor.width, descriptor.height);
					passData.material = selectionBlitMaterial;
					builder.UseTexture(selectionMaskColor, AccessFlags.Read);
					// Alpha blending reads the existing camera color.
					builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.ReadWrite);
					builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) => ExecuteBlitPass(data, context));
				}
			}

			static void ExecuteMaskPass(MaskPassData data, RasterGraphContext context)
			{
				context.cmd.ClearRenderTarget(true, true, Color.clear);
				context.cmd.SetViewport(data.viewport);
				context.cmd.DrawRendererList(data.objectsInSelection);
			}

			static void ExecuteBlitPass(BlitPassData data, RasterGraphContext context)
			{
				context.cmd.SetViewport(data.viewport);
				Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
			}
		}

		SelectionPass selectionPass;
		public Shader SelectionBlitShader;
		public Color OutlineColor;
		public Color SelectionColor;
		public float SampleDistance;
		public float SampleDistanceReferenceWidth;

		// TODO: Good editor
		public uint RenderingLayerMask = uint.MaxValue;

		public override void Create()
		{
			if (SelectionBlitShader == null)
			{
				SelectionBlitShader = Shader.Find("SelectionRendering/SelectionBlit");
			}

			selectionPass = new SelectionPass(this);

			selectionPass.renderPassEvent = RenderPassEvent.AfterRendering;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (renderingData.cameraData.cameraType == CameraType.Game)
			{
				renderer.EnqueuePass(selectionPass);
			}
		}
	}
}


