
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ControlLuces : MonoBehaviour
{
    [Header("Materiales de luces normales")]
    public Material lucesnormales;

    [Header("Iluminación general")]
    public Volume volumenGlobal;

    [Range(-10f, 0f)]
    public float exposicionApagada = -3f;

    [Range(0f, 1f)]
    public float ambienteApagado = 0.5f;

    private Color emisionOriginal;
    private Color ambienteOriginal;
    private ColorAdjustments ajustesColor;
    private float exposicionOriginal;

    void Start()
    {
        // Guardar valores originales
        ambienteOriginal = RenderSettings.ambientLight;

        if (lucesnormales != null &&
            lucesnormales.HasProperty("_EmissionColor"))
        {
            emisionOriginal =
                lucesnormales.GetColor("_EmissionColor");
        }

        if (volumenGlobal != null &&
            volumenGlobal.profile.TryGet(
                out ajustesColor))
        {
            exposicionOriginal =
                ajustesColor.postExposure.value;

            ajustesColor.postExposure.overrideState = true;
        }
        else
        {
            Debug.LogError(
                "Falta Global Volume o Color Adjustments."
            );
        }
    }

    public void ApagarLuces()
    {
        if (lucesnormales != null)
        {
            lucesnormales.SetColor(
                "_EmissionColor",
                Color.black
            );
        }

        RenderSettings.ambientLight =
            ambienteOriginal * ambienteApagado;

        if (ajustesColor != null)
        {
            ajustesColor.postExposure.value =
                exposicionApagada;
        }

        Debug.Log("APAGÓN: Luces normales apagadas.");
    }

    public void EncenderLuces()
    {
        if (lucesnormales != null)
        {
            lucesnormales.SetColor(
                "_EmissionColor",
                emisionOriginal
            );
        }

        RenderSettings.ambientLight =
            ambienteOriginal;

        if (ajustesColor != null)
        {
            ajustesColor.postExposure.value =
                exposicionOriginal;
        }

        Debug.Log("ILUMINACIÓN RESTAURADA.");
    }
}
