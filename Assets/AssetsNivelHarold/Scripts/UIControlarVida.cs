
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIControladorVida : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private VidaJugador vidaJugador;

    [Header("Elementos UI")]
    [SerializeField] private Slider barraVida;
    [SerializeField] private TMP_Text textoVida;

    void Start()
    {
        if (vidaJugador == null)
        {
            Debug.LogError("Falta asignar VidaJugador.");
            return;
        }

        vidaJugador.VidaCambiada += ActualizarVida;

        ActualizarVida(
            vidaJugador.VidaActual,
            vidaJugador.VidaMaxima
        );
    }

    void ActualizarVida(float actual, float maxima)
    {
        if (barraVida != null)
        {
            barraVida.minValue = 0f;
            barraVida.maxValue = maxima;
            barraVida.value = actual;
        }

        if (textoVida != null)
        {
            textoVida.text =
                $"{Mathf.CeilToInt(actual)} / {Mathf.CeilToInt(maxima)}";
        }
    }

    void OnDestroy()
    {
        if (vidaJugador != null)
            vidaJugador.VidaCambiada -= ActualizarVida;
    }
}
