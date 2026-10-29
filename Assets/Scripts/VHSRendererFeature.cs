using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class VHSRendererFeature : ScriptableRendererFeature
{
    public Shader shader;
    private Material material;
    private VHSPass pass;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new VHSPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || renderingData.cameraData.cameraType != CameraType.Game ||
            !renderingData.cameraData.resolveFinalTarget) return;
        foreach (var control in VHSCamara.Activas)
        {
            if (control == null || !control.isActiveAndEnabled || !control.Aplica(renderingData.cameraData.camera)) continue;
            if (control.Intensidad <= 0f) return;
            pass.Preparar(material, control.Intensidad);
            renderer.EnqueuePass(pass);
            return;
        }
    }

    protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }

    private class VHSPass : ScriptableRenderPass
    {
        private Material material;
        private float intensidad;
        private static readonly int Parametros = Shader.PropertyToID("_VHSParams");
        public void Preparar(Material mat, float valor)
        {
            material = mat;
            intensidad = valor;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var recursos = frameData.Get<UniversalResourceData>();
            if (recursos.isActiveTargetBackBuffer) return;
            var original = recursos.activeColorTexture;
            var desc = graph.GetTextureDesc(original);
            desc.name = "VHS copia de color";
            desc.clearBuffer = false;
            var copia = graph.CreateTexture(desc);
            graph.AddBlitPass(original, copia, Vector2.one, Vector2.zero, passName: "VHS copiar imagen");
            // Copiar y escribir en el destino original conserva el render de
            // cámaras apiladas (escenario + brazos), sin realimentar la textura.
            var propiedades = new MaterialPropertyBlock();
            propiedades.SetVector(Parametros, new Vector4(intensidad, Time.time,
                1f / Mathf.Max(1, desc.width), 1f / Mathf.Max(1, desc.height)));
            var blit = new RenderGraphUtils.BlitMaterialParameters(copia, original, material, 0,
                propiedades, 0, 0, geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            graph.AddBlitPass(blit, passName: "VHS sutil del jugador");
        }
    }
}
