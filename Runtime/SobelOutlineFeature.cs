using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class SobelOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

        [Header("Outline")]
        [ColorUsage(true, true)] public Color color = Color.white;
        [Range(0.1f, 8f)] public float width = 1.2f;
        [Range(0f, 2f)] public float weight = 1f;

        [Header("Depth")]
        [Range(0f, 20f)] public float depthMultiplier = 1f;
        [Range(0.1f, 20f)] public float depthBias = 1f;

        [Header("Normals")]
        [Range(0f, 20f)] public float normalMultiplier = 1f;
        [Range(0.1f, 20f)] public float normalBias = 10f;
    }

    public Settings settings = new Settings();

    [SerializeField] Shader shader;
    [SerializeField] Shader coverageShader;

    private Material _material;
    private Material _coverageMaterial;
    private SobelOutlinePass _pass;

    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineWeightId = Shader.PropertyToID("_OutlineWeight");
    private static readonly int OutlineDepthMultiplierId = Shader.PropertyToID("_OutlineDepthMultiplier");
    private static readonly int OutlineDepthBiasId = Shader.PropertyToID("_OutlineDepthBias");
    private static readonly int OutlineNormalMultiplierId = Shader.PropertyToID("_OutlineNormalMultiplier");
    private static readonly int OutlineNormalBiasId = Shader.PropertyToID("_OutlineNormalBias");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    private static readonly int SobelDepthTexId = Shader.PropertyToID("_SobelDepthTex");
    private static readonly int SobelNormalTexId = Shader.PropertyToID("_SobelNormalTex");
    private static readonly int SobelIncludeTexId = Shader.PropertyToID("_SobelIncludeTex");

    public override void Create()
    {
        _pass = new SobelOutlinePass
        {
            renderPassEvent = settings != null
                ? settings.injectionPoint
                : RenderPassEvent.BeforeRenderingPostProcessing
        };
        _pass.requiresIntermediateTexture = true;
        AssignDefaultShaders();
    }

    public void AssignDefaultShaders()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Cubus/SobelOutlinePost");
        }

        if (coverageShader == null)
        {
            coverageShader = Shader.Find("Hidden/Cubus/SobelCoverage");
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection ||
            UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
        {
            return;
        }

        if (!EnsureMaterials())
        {
            return;
        }

        ApplySettings();
        _pass.renderPassEvent = settings.injectionPoint;
        _pass.Setup(_material, _coverageMaterial);
        _pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        CoreUtils.Destroy(_coverageMaterial);
        _material = null;
        _coverageMaterial = null;
    }

    private bool EnsureMaterials()
    {
        if (_material == null)
        {
            Shader foundShader = shader != null ? shader : Shader.Find("Hidden/Cubus/SobelOutlinePost");
            if (foundShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(foundShader);
            }
        }

        if (_coverageMaterial == null)
        {
            Shader foundCoverage = coverageShader != null ? coverageShader : Shader.Find("Hidden/Cubus/SobelCoverage");
            if (foundCoverage != null)
            {
                _coverageMaterial = CoreUtils.CreateEngineMaterial(foundCoverage);
            }
        }

        return _material != null && _coverageMaterial != null;
    }

    private void ApplySettings()
    {
        _material.SetFloat(OutlineThicknessId, settings.width);
        _material.SetFloat(OutlineWeightId, settings.weight);
        _material.SetFloat(OutlineDepthMultiplierId, settings.depthMultiplier);
        _material.SetFloat(OutlineDepthBiasId, settings.depthBias);
        _material.SetFloat(OutlineNormalMultiplierId, settings.normalMultiplier);
        _material.SetFloat(OutlineNormalBiasId, settings.normalBias);
        _material.SetColor(OutlineColorId, settings.color);
        _material.SetTexture(SobelIncludeTexId, Texture2D.blackTexture);
    }

    private sealed class SobelOutlinePass : ScriptableRenderPass
    {
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private Material _material;
        private Material _coverageMaterial;
        private readonly List<Renderer> _includedRenderers = new List<Renderer>();

        public SobelOutlinePass()
        {
            profilingSampler = new ProfilingSampler("Sobel Outline");
        }

        public void Setup(Material material, Material coverageMaterial)
        {
            _material = material;
            _coverageMaterial = coverageMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();

            if (!resources.cameraColor.IsValid() || _material == null || _coverageMaterial == null)
            {
                return;
            }

            TextureDesc targetDesc = renderGraph.GetTextureDesc(resources.cameraColor);
            targetDesc.name = "_SobelOutlineColorCopy";
            targetDesc.clearBuffer = false;

            TextureHandle copiedColor = renderGraph.CreateTexture(targetDesc);
            TextureHandle source = resources.activeColorTexture;
            renderGraph.AddBlitPass(source, copiedColor, Vector2.one, Vector2.zero, passName: "Sobel Outline Copy Color");

            TextureHandle depthSource = resources.cameraDepthTexture.IsValid()
                ? resources.cameraDepthTexture
                : resources.cameraDepth;
            TextureHandle copiedDepth = depthSource;
            if (depthSource.IsValid())
            {
                TextureDesc depthCopyDesc = targetDesc;
                depthCopyDesc.name = "_SobelOutlineDepthCopy";
                depthCopyDesc.format = GraphicsFormat.R32_SFloat;
                depthCopyDesc.colorFormat = GraphicsFormat.R32_SFloat;
                copiedDepth = renderGraph.CreateTexture(depthCopyDesc);
                renderGraph.AddBlitPass(depthSource, copiedDepth, Vector2.one, Vector2.zero, passName: "Sobel Outline Copy Depth");
            }

            TextureHandle copiedNormals = resources.cameraNormalsTexture;
            if (resources.cameraNormalsTexture.IsValid())
            {
                TextureDesc normalCopyDesc = targetDesc;
                normalCopyDesc.name = "_SobelOutlineNormalCopy";
                copiedNormals = renderGraph.CreateTexture(normalCopyDesc);
                renderGraph.AddBlitPass(resources.cameraNormalsTexture, copiedNormals, Vector2.one, Vector2.zero, passName: "Sobel Outline Copy Normals");
            }

            TextureHandle includeMask = CreateMaskTexture(renderGraph, targetDesc, "_SobelIncludeMask", GraphicsFormat.R8G8B8A8_UNorm);
            RecordIncludeMaskPass(renderGraph, includeMask);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Sobel Outline", out PassData passData, profilingSampler))
            {
                passData.material = _material;
                passData.source = copiedColor;
                passData.depth = copiedDepth;
                passData.normals = copiedNormals;
                passData.includeMask = includeMask;
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);

                if (passData.depth.IsValid())
                {
                    builder.UseTexture(passData.depth, AccessFlags.Read);
                }

                if (passData.normals.IsValid())
                {
                    builder.UseTexture(passData.normals, AccessFlags.Read);
                }

                builder.UseTexture(passData.includeMask, AccessFlags.Read);
                builder.UseGlobalTexture(SobelIncludeTexId, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    PropertyBlock.Clear();
                    PropertyBlock.SetTexture(BlitTextureId, data.source);
                    PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    if (data.depth.IsValid())
                    {
                        PropertyBlock.SetTexture(SobelDepthTexId, data.depth);
                    }

                    if (data.normals.IsValid())
                    {
                        PropertyBlock.SetTexture(SobelNormalTexId, data.normals);
                    }

                    PropertyBlock.SetTexture(SobelIncludeTexId, data.includeMask);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, PropertyBlock);
                });
            }
        }

        private void RecordIncludeMaskPass(
            RenderGraph renderGraph,
            TextureHandle includeMask)
        {
            SobelOutlineInclude.CopyActiveRenderers(_includedRenderers);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<IncludePassData>("Sobel Include Mask", out IncludePassData passData, profilingSampler))
            {
                passData.coverageMaterial = _coverageMaterial;
                passData.includedRenderers = _includedRenderers;
                TextureHandle mask = includeMask;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(mask, SobelIncludeTexId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (IncludePassData data, RasterGraphContext context) =>
                {
                    if (data.includedRenderers == null || data.coverageMaterial == null)
                    {
                        return;
                    }

                    for (int i = 0; i < data.includedRenderers.Count; i++)
                    {
                        Renderer renderer = data.includedRenderers[i];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        context.cmd.DrawRenderer(renderer, data.coverageMaterial);
                    }
                });
            }
        }

        private static TextureHandle CreateMaskTexture(RenderGraph renderGraph, TextureDesc sourceDesc, string name, GraphicsFormat format)
        {
            TextureDesc maskDesc = sourceDesc;
            maskDesc.name = name;
            maskDesc.format = format;
            maskDesc.colorFormat = format;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.black;
            maskDesc.filterMode = FilterMode.Point;
            maskDesc.useMipMap = false;
            maskDesc.msaaSamples = MSAASamples.None;
            maskDesc.fallBackToBlackTexture = true;
            return renderGraph.CreateTexture(maskDesc);
        }

        private class PassData
        {
            internal Material material;
            internal TextureHandle source;
            internal TextureHandle depth;
            internal TextureHandle normals;
            internal TextureHandle includeMask;
        }

        private class IncludePassData
        {
            internal Material coverageMaterial;
            internal List<Renderer> includedRenderers;
        }
    }
}
