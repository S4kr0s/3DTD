using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using EPOOutline;
using System;
using System.Reflection;

#if URP_OUTLINE && UNITY_2019_1_OR_NEWER
#if UNITY_2019_3_OR_NEWER
using UnityEngine.Rendering.Universal;
#else
using UnityEngine.Rendering.LWRP;
#endif
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

public class URPOutlineFeature : ScriptableRendererFeature
{
    private class SRPOutline : ScriptableRenderPass
    {
        private static List<Outlinable> temporaryOutlinables = new List<Outlinable>();

        public ScriptableRenderer Renderer;

        public bool UseColorTargetForDepth;

        public Outliner Outliner;

        public OutlineParameters Parameters = new OutlineParameters();

        private List<Outliner> outliners = new List<Outliner>();

        public SRPOutline()
        {
            Parameters.CheckInitialization();

#if UNITY_6000_0_OR_NEWER
            // The outline is composited onto the camera color, so it must not be the backbuffer.
            requiresIntermediateTexture = true;
#endif
        }

        private void RenderOutlines()
        {
            if (Outliner.RenderingStrategy == OutlineRenderingStrategy.Default)
            {
                OutlineEffect.SetupOutline(Parameters);
                Parameters.BlitMesh = null;
                Parameters.MeshPool.ReleaseAllMeshes();
            }
            else
            {
                temporaryOutlinables.Clear();
                temporaryOutlinables.AddRange(Parameters.OutlinablesToRender);

                Parameters.OutlinablesToRender.Clear();
                Parameters.OutlinablesToRender.Add(null);

                foreach (var outlinable in temporaryOutlinables)
                {
                    Parameters.OutlinablesToRender[0] = outlinable;
                    OutlineEffect.SetupOutline(Parameters);
                    Parameters.BlitMesh = null;
                }

                Parameters.MeshPool.ReleaseAllMeshes();
            }
        }

#if UNITY_6000_0_OR_NEWER
        // URP 17 (Unity 6) removed the non-render-graph Execute path, so the outline commands are
        // recorded into the frame's command buffer from an unsafe render graph pass instead.
        private class PassData
        {
            public SRPOutline Pass;
            public Outliner Outliner;
            public Camera Camera;
            public bool IsSceneViewCamera;
            public RenderTextureDescriptor TargetDescriptor;
            public TextureHandle Color;
            public TextureHandle Depth;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Outliner == null || !Outliner.enabled)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer || !resourceData.activeColorTexture.IsValid())
                return;

            using (var builder = renderGraph.AddUnsafePass<PassData>("Easy Performant Outline", out var passData))
            {
                passData.Pass = this;
                passData.Outliner = Outliner;
                passData.Camera = cameraData.camera;
                passData.IsSceneViewCamera = cameraData.isSceneViewCamera;
                passData.TargetDescriptor = cameraData.cameraTargetDescriptor;
                passData.Color = resourceData.activeColorTexture;
                passData.Depth = resourceData.activeDepthTexture;

                builder.UseTexture(passData.Color, AccessFlags.ReadWrite);
                if (passData.Depth.IsValid())
                    builder.UseTexture(passData.Depth, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    RenderTargetIdentifier color = data.Color;
                    RenderTargetIdentifier depth = data.Depth.IsValid() ? (RenderTargetIdentifier)data.Depth : color;
                    data.Pass.RecordOutline(buffer, data.Outliner, data.Camera, data.IsSceneViewCamera, data.TargetDescriptor, color, depth);
                });
            }
        }

        private void RecordOutline(CommandBuffer buffer, Outliner outliner, Camera camera, bool isSceneViewCamera, RenderTextureDescriptor targetDescriptor, RenderTargetIdentifier colorTarget, RenderTargetIdentifier depthTarget)
        {
            if (outliner == null || !outliner.enabled)
                return;

            // OutlineEffect records into Parameters.Buffer, so point it at the render graph's buffer for this pass.
            var ownBuffer = Parameters.Buffer;
            Parameters.Buffer = buffer;
            Outliner = outliner;

            try
            {
                Outlinable.GetAllActiveOutlinables(camera, Parameters.OutlinablesToRender);

                Outliner.UpdateSharedParameters(Parameters, camera, isSceneViewCamera);

                RendererFilteringUtility.Filter(camera, Parameters);

                Parameters.TargetWidth = targetDescriptor.width;
                Parameters.TargetHeight = targetDescriptor.height;

                Parameters.Antialiasing = targetDescriptor.msaaSamples;

                Parameters.Target = RenderTargetUtility.ComposeTarget(Parameters, colorTarget);
                Parameters.DepthTarget = RenderTargetUtility.ComposeTarget(Parameters, depthTarget);

                RenderOutlines();
            }
            finally
            {
                Parameters.Buffer = ownBuffer;
            }
        }
#else
        private FieldInfo nameId = typeof(RenderTargetIdentifier).GetField("m_NameID", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool IsDepthTextureAvailable(ScriptableRenderer renderer)
        {
#if UNITY_2022_1_OR_NEWER
            return renderer.cameraDepthTargetHandle.rt != null;
#else
            return (int)nameId.GetValue(GetDepthTarget(renderer)) != -1;
#endif
        }

        private RenderTargetIdentifier GetDepthTarget(ScriptableRenderer renderer)
        {
            return
#if UNITY_2022_1_OR_NEWER
                Renderer.cameraDepthTargetHandle;
#elif UNITY_2020_2_OR_NEWER
                Renderer.cameraDepthTarget;
#else
                Renderer.cameraDepth;
#endif
        }

        private RenderTargetIdentifier GetColorTarget(ScriptableRenderer renderer)
        {
#if UNITY_2022_1_OR_NEWER
                return renderer.cameraColorTargetHandle;
#else
                return renderer.cameraColorTarget;
#endif
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            var outlineEffect = Outliner;
            if (outlineEffect == null || !outlineEffect.enabled)
                return;

#if UNITY_EDITOR
            Parameters.Buffer.name = renderingData.cameraData.camera.name;
#endif

            Outlinable.GetAllActiveOutlinables(renderingData.cameraData.camera, Parameters.OutlinablesToRender);

            Outliner.UpdateSharedParameters(Parameters, renderingData.cameraData.camera, renderingData.cameraData.isSceneViewCamera);

            RendererFilteringUtility.Filter(renderingData.cameraData.camera, Parameters);

            Parameters.TargetWidth = renderingData.cameraData.cameraTargetDescriptor.width;
            Parameters.TargetHeight = renderingData.cameraData.cameraTargetDescriptor.height;

            Parameters.Antialiasing = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;

            Parameters.Target = RenderTargetUtility.ComposeTarget(Parameters, Renderer.cameraColorTarget);
            Parameters.DepthTarget =
#if UNITY_2019_3_OR_NEWER && !UNITY_2019_3_0 && !UNITY_2019_3_1 && !UNITY_2019_3_2 && !UNITY_2019_3_3 && !UNITY_2019_3_4 && !UNITY_2019_3_5 && !UNITY_2019_3_6 && !UNITY_2019_3_7 && !UNITY_2019_3_8
            RenderTargetUtility.ComposeTarget(Parameters, !IsDepthTextureAvailable(Renderer) ? GetColorTarget(Renderer) :
                GetDepthTarget(Renderer));
#else
                RenderTargetUtility.ComposeTarget(Parameters, Renderer.cameraColorTarget);
#endif

            Parameters.Buffer.Clear();
            RenderOutlines();

            context.ExecuteCommandBuffer(Parameters.Buffer);
        }
#endif
    }

    private class Pool
    {
        private Stack<SRPOutline> outlines = new Stack<SRPOutline>();

        private List<SRPOutline> createdOutlines = new List<SRPOutline>();

        public SRPOutline Get()
        {
            if (outlines.Count == 0)
            {
                outlines.Push(new SRPOutline());
                createdOutlines.Add(outlines.Peek());
            }

            return outlines.Pop();
        }

        public void ReleaseAll()
        {
            outlines.Clear();
            foreach (var outline in createdOutlines)
                outlines.Push(outline);
        }
    }

    private GameObject lastSelectedCamera;

    private Pool outlinePool = new Pool();

    private List<Outliner> outliners = new List<Outliner>();

    private bool GetOutlinersToRenderWith(RenderingData renderingData, List<Outliner> outliners)
    {
        outliners.Clear();
        var camera = renderingData.cameraData.camera.gameObject;
        camera.GetComponents(outliners);
        if (outliners.Count == 0)
        {
#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                var foundObject = Array.Find(
                    Array.ConvertAll(UnityEditor.Selection.gameObjects, x => x.GetComponent<Outliner>()),
                    x => x != null);

                camera = foundObject?.gameObject ?? lastSelectedCamera;

                if (camera == null)
                    return false;
                else
                    camera.GetComponents(outliners);
            }
            else
                return false;
#else
                return false;
#endif
        }

        var hasOutliners = outliners.Count > 0;
        if (hasOutliners)
            lastSelectedCamera = camera;

        return hasOutliners;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!GetOutlinersToRenderWith(renderingData, outliners))
            return;

#if UNITY_2019_3_OR_NEWER && !UNITY_2019_3_0 && !UNITY_2019_3_1 && !UNITY_2019_3_2 && !UNITY_2019_3_3 && !UNITY_2019_3_4 && !UNITY_2019_3_5 && !UNITY_2019_3_6 && !UNITY_2019_3_7 && !UNITY_2019_3_8
        var additionalCameraData = renderingData.cameraData.camera.GetUniversalAdditionalCameraData();
        var activeStackCount = 0;
        if (additionalCameraData != null)
        {
            var stack = additionalCameraData.renderType == CameraRenderType.Overlay ? null : additionalCameraData.cameraStack;
            if (stack != null)
            {
                foreach (var camera in stack)
                {
                    if (camera != null && camera.isActiveAndEnabled)
                        activeStackCount++;
                }
            }
        }
#endif

        foreach (var outliner in outliners)
        {
            var outline = outlinePool.Get();

            outline.Outliner = outliner;

            outline.Renderer = renderer;

            outline.renderPassEvent = outliner.RenderStage == RenderStage.AfterTransparents ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;

            renderer.EnqueuePass(outline);
        }

        outlinePool.ReleaseAll();
    }

    public override void Create()
    {
    }
}
#endif
