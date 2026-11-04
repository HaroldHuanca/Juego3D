using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente de la caja de cartón en el almacén.
/// Controla el efecto de contorno/brillo cuando el jugador la mira.
/// </summary>
public class CajaInteractuable : MonoBehaviour
{
    [Header("Efecto de Contorno y Brillo")]
    [Tooltip("Color del brillo y contorno al apuntar la mirada a la caja")]
    [SerializeField] private Color colorContorno = new Color(1f, 0.85f, 0.25f, 0.95f);

    [Tooltip("Grosor del contorno visible")]
    [Range(0.001f, 0.03f)]
    [SerializeField] private float grosorContorno = 0.008f;

    [Tooltip("Intensidad de la emisión de luz")]
    [Range(0.1f, 2f)]
    [SerializeField] private float intensidadEmision = 0.6f;

    private Renderer[] renderers;
    private Material materialContorno;
    private bool estaResaltada = false;
    private Dictionary<Renderer, Material[]> materialesOriginales = new Dictionary<Renderer, Material[]>();

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        GuardarMaterialesOriginales();
    }

    private void GuardarMaterialesOriginales()
    {
        materialesOriginales.Clear();
        foreach (var rend in renderers)
        {
            if (rend != null)
            {
                materialesOriginales[rend] = rend.materials;
            }
        }
    }

    private void CrearMaterialContornoSiEsNecesario()
    {
        if (materialContorno == null)
        {
            Shader shaderOutline = Shader.Find("Custom/URP_Outline");
            if (shaderOutline != null)
            {
                materialContorno = new Material(shaderOutline);
            }
        }

        if (materialContorno != null)
        {
            materialContorno.SetColor("_OutlineColor", colorContorno);
            materialContorno.SetFloat("_OutlineWidth", grosorContorno);
        }
    }

    /// <summary>
    /// Activa o desactiva el brillo y contorno de la caja al apuntarla con el puntero.
    /// </summary>
    public void SetResaltado(bool activo)
    {
        if (estaResaltada == activo) return;
        estaResaltada = activo;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            GuardarMaterialesOriginales();
        }

        CrearMaterialContornoSiEsNecesario();

        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            if (activo)
            {
                // 1. Efecto de contorno (Shader Outline como material adicional)
                if (materialContorno != null)
                {
                    List<Material> listaMats = new List<Material>(rend.materials);
                    if (!listaMats.Contains(materialContorno))
                    {
                        listaMats.Add(materialContorno);
                        rend.materials = listaMats.ToArray();
                    }
                }

                // 2. Efecto de brillo / emisión suave
                foreach (var mat in rend.materials)
                {
                    if (mat != materialContorno)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", colorContorno * intensidadEmision);
                    }
                }
            }
            else
            {
                // Restaurar materiales originales sin contorno
                if (materialesOriginales.ContainsKey(rend) && materialesOriginales[rend] != null)
                {
                    rend.materials = materialesOriginales[rend];
                }

                // Apagar emisión
                foreach (var mat in rend.materials)
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void OnDisable()
    {
        SetResaltado(false);
    }
}
