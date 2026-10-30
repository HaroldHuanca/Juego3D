using System.Collections.Generic;
using ColbyO.VNTG.CRT;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>VNTG, bruma y luz frontal; comparte la transición del ambiente sonoro.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(VHSCamara))]
public sealed class EstiloAnalogico : MonoBehaviour
{
    [Header("Visibilidad")]
    [Range(0f, 0.2f)] public float granoExterior = 0.065f;
    [Range(0f, 0.2f)] public float granoInterior = 0.095f;
    [Range(0f, 0.15f)] public float nieblaExterior = 0.035f;
    [Range(0f, 0.15f)] public float nieblaInterior = 0.008f;
    [Range(0f, 30f)] public float intensidadLinterna = 12f;
    [Range(8, 64)] public int pasosNiebla = 32;

    private VHSCamara control;
    private GameObject volumenObjeto, linternaObjeto;
    private Volume volumen;
    private VolumeProfile perfil;
    private CRTSettings crt;
    private VolumetricFogVolumeComponent niebla;
    private Light linterna;
    private UniversalAdditionalCameraData principal, brazos;
    private LayerMask mascaraPrincipal, mascaraBrazos;
    private bool postPrincipal, postBrazos;
    private readonly List<VolumetricAdditionalLight> lucesAgregadas = new();
    // Dedicated volume layer: does not change menu/Scene View or existing profiles.
    private const int CapaVolumen = 30;

    private void OnEnable()
    {
        control = GetComponent<VHSCamara>();
        principal = GetComponent<Camera>().GetUniversalAdditionalCameraData();
        mascaraPrincipal = principal.volumeLayerMask;
        postPrincipal = principal.renderPostProcessing;
        principal.volumeLayerMask = 1 << CapaVolumen;
        principal.renderPostProcessing = control.camaraBrazos == null;
        if (control.camaraBrazos != null)
        {
            brazos = control.camaraBrazos.GetUniversalAdditionalCameraData();
            mascaraBrazos = brazos.volumeLayerMask;
            postBrazos = brazos.renderPostProcessing;
            brazos.volumeLayerMask = 1 << CapaVolumen;
            brazos.renderPostProcessing = true;
        }

        volumenObjeto = new GameObject("Perfil VHS y bruma del jugador");
        volumenObjeto.layer = CapaVolumen;
        volumenObjeto.transform.SetParent(transform, false);
        volumen = volumenObjeto.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 100;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.name = "VHS legible - VNTG";
        volumen.sharedProfile = perfil;

        crt = perfil.Add<CRTSettings>(true);
        crt.Enabled.value = true;
        crt.ShowInSceneView.value = false;
        crt.EnableScreenSettings.value = false;
        crt.UseMaxFPS.value = true;
        crt.EnableInterlacedRendering.value = false;
        crt.EnableScreenBend.value = false;
        crt.EnableVignette.value = false;
        crt.EnableSubPixels.value = false;
        crt.EnableTrackerLine.value = false;
        crt.EnableGlitch.value = false;
        crt.EnableSignalInterference.value = true;
        crt.InterferenceFrequency.value = 150f;
        crt.InterferenceAmplitude.value = 0.65f;
        crt.EnableScanlines.value = true;
        crt.ScanLineVerticalOpacity.value = 0f;
        crt.ScanLineHorizontalOpacity.value = 1f;
        crt.ScanLineVerticalSpeed.value = 0f;
        crt.ScanLineHorizontalSpeed.value = 0.005f;
        crt.EnableNoise.value = true;
        crt.NoiseScale.value = 1f;
        crt.NoiseSpeed.value = 0.5f;
        crt.NoiseRBGOffsetX.value = 0.1f;
        crt.NoiseRBGOffsetY.value = 0.1f;
        crt.EnableVhs.value = true;
        crt.VhsSmear.value = 0.10f;
        crt.EnableChromaticAberration.value = true;
        crt.ChromaticOffsetSpeed.value = 0.04f;
        crt.EnableUnsharp.value = false;
        crt.EnableClampColor.value = false;
        crt.EnableColorAdjustment.value = false;

        var bloom = perfil.Add<Bloom>(true);
        bloom.intensity.value = 0.12f;
        bloom.threshold.value = 1.15f;
        bloom.scatter.value = 0.45f;
        var color = perfil.Add<ColorAdjustments>(true);
        color.saturation.value = -8f;
        color.postExposure.value = 0.25f;
        color.contrast.value = 0f;
        var vignette = perfil.Add<Vignette>(true);
        vignette.intensity.value = 0.16f;
        vignette.smoothness.value = 0.55f;
        // Neutral retains the already dark scene's shadow detail while compressing lamps.
        perfil.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;

        niebla = perfil.Add<VolumetricFogVolumeComponent>(true);
        niebla.enabled.value = true;
        niebla.distance.value = 55f;
        niebla.baseHeight.value = 0f;
        niebla.maximumHeight.value = 18f;
        niebla.groundHeight.value = -5f;
        niebla.attenuationDistance.value = 180f;
        niebla.enableMainLightContribution.value = true;
        niebla.enableAdditionalLightsContribution.value = true;
        niebla.anisotropy.value = 0.25f;
        niebla.scattering.value = 0.12f;
        niebla.tint.value = new Color(0.68f, 0.79f, 0.88f);
        niebla.blurIterations.value = 1;

        linternaObjeto = new GameObject("Luz frontal suave");
        linternaObjeto.transform.SetParent(transform, false);
        linternaObjeto.transform.localPosition = new Vector3(0.06f, -0.04f, 0.12f);
        linterna = linternaObjeto.AddComponent<Light>();
        linterna.type = LightType.Spot;
        linterna.color = new Color(1f, 0.94f, 0.84f);
        linterna.range = 20f;
        linterna.spotAngle = 72f;
        linterna.innerSpotAngle = 44f;
        linterna.shadows = LightShadows.Soft;
        linterna.shadowNearPlane = 0.1f;
        linterna.shadowBias = 0.02f;
        linterna.shadowNormalBias = 0.25f;
        linterna.cullingMask = ~(1 << 9);
        var haz = linternaObjeto.AddComponent<VolumetricAdditionalLight>();
        haz.Scattering = 0.12f;
        haz.Anisotropy = 0.15f;
        haz.Radius = 0.5f;

        // Only two fixed exterior lights contribute in addition to the flashlight.
        var candidatas = FindObjectsByType<Light>(FindObjectsSortMode.None);
        System.Array.Sort(candidatas, (a, b) =>
            (a.transform.position - transform.position).sqrMagnitude.CompareTo(
                (b.transform.position - transform.position).sqrMagnitude));
        foreach (var luz in candidatas)
        {
            if (lucesAgregadas.Count >= 2) break;
            if (!luz.name.StartsWith("LuzPoste") || luz.GetComponent<VolumetricAdditionalLight>() != null) continue;
            var aporte = luz.gameObject.AddComponent<VolumetricAdditionalLight>();
            aporte.Scattering = 0.18f;
            aporte.Anisotropy = 0.2f;
            aporte.Radius = 0.5f;
            lucesAgregadas.Add(aporte);
        }
        ActualizarMezcla();
    }

    private void LateUpdate() => ActualizarMezcla();

    private void ActualizarMezcla()
    {
        if (crt == null || niebla == null) return;
        principal.renderPostProcessing = control.camaraBrazos == null || !control.camaraBrazos.isActiveAndEnabled;
        float mezcla = control.ambiente != null ? control.ambiente.MezclaInterior : 0f;
        float fuerza = Mathf.Clamp(control.Intensidad / Mathf.Lerp(0.3f, 0.6f, mezcla), 0f, 2f);
        crt.Enabled.value = control.isActiveAndEnabled && fuerza > 0f;
        crt.NoiseFade.value = Mathf.Lerp(granoExterior, granoInterior, mezcla) * fuerza;
        crt.ScanLineStrength.value = Mathf.Lerp(0.025f, 0.04f, mezcla) * fuerza;
        crt.ChromaticOffset.value = Mathf.Lerp(0.005f, 0.008f, mezcla) * fuerza;
        crt.InterferenceAmplitude.value = 0.65f * fuerza;
        niebla.density.value = Mathf.Lerp(nieblaExterior, nieblaInterior, mezcla);
        niebla.maxSteps.value = Mathf.Clamp(pasosNiebla, 8, 64);
        linterna.intensity = intensidadLinterna * Mathf.Lerp(1f, 0.65f, mezcla);
    }

    private void OnDisable()
    {
        if (principal != null) { principal.volumeLayerMask = mascaraPrincipal; principal.renderPostProcessing = postPrincipal; }
        if (brazos != null) { brazos.volumeLayerMask = mascaraBrazos; brazos.renderPostProcessing = postBrazos; }
        foreach (var luz in lucesAgregadas) if (luz != null) Destroy(luz);
        lucesAgregadas.Clear();
        if (volumenObjeto != null) { volumenObjeto.SetActive(false); Destroy(volumenObjeto); }
        if (linternaObjeto != null) { linternaObjeto.SetActive(false); Destroy(linternaObjeto); }
        if (perfil != null)
        {
            foreach (var componente in perfil.components) Destroy(componente);
            Destroy(perfil);
        }
    }
}
