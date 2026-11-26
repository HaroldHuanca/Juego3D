
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIControladorNotificaciones : MonoBehaviour
{
    [Header("Elementos UI")]
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text textoNotificacion;
    [SerializeField] private Image iconoNotificacion;

    [Header("Configuración")]
    [SerializeField] private float duracion = 2.5f;
    [SerializeField] private float duracionTransicion = 0.25f;

    private Coroutine rutinaActual;

    void Awake()
    {
        if (grupo != null)
        {
            grupo.alpha = 0f;
            grupo.interactable = false;
            grupo.blocksRaycasts = false;
        }
    }

    public void Mostrar(string mensaje, Sprite icono = null)
    {
        if (grupo == null || textoNotificacion == null)
            return;

        if (rutinaActual != null)
            StopCoroutine(rutinaActual);

        textoNotificacion.text = mensaje;

        if (iconoNotificacion != null)
        {
            iconoNotificacion.sprite = icono;
            iconoNotificacion.enabled = icono != null;
        }

        rutinaActual = StartCoroutine(MostrarRutina());
    }

    IEnumerator MostrarRutina()
    {
        yield return CambiarAlpha(0.1f);

        yield return new WaitForSecondsRealtime(duracion);

        yield return CambiarAlpha(0f);

        rutinaActual = null;
    }

    IEnumerator CambiarAlpha(float destino)
    {
        float inicio = grupo.alpha;
        float tiempo = 0f;

        if (duracionTransicion <= 0f)
        {
            grupo.alpha = destino;
            yield break;
        }

        while (tiempo < duracionTransicion)
        {
            tiempo += Time.unscaledDeltaTime;

            float progreso = Mathf.Clamp01(
                tiempo / duracionTransicion
            );

            grupo.alpha = Mathf.Lerp(
                inicio,
                destino,
                progreso
            );

            yield return null;
        }

        grupo.alpha = destino;
    }
}
