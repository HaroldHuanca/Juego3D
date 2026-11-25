
using System;
using UnityEngine;

public class VidaJugador : MonoBehaviour
{
    [Header("Configuración")]
    [Min(1)]
    [SerializeField] private float vidaMaxima = 100f;

    [SerializeField] private float vidaActual = 100f;

    public float VidaActual => vidaActual;
    public float VidaMaxima => vidaMaxima;

    public event Action<float, float> VidaCambiada;

    void Awake()
    {
        vidaActual = Mathf.Clamp(vidaActual, 0f, vidaMaxima);
    }

    public void RecibirDanio(float cantidad)
    {
        if (cantidad <= 0f || vidaActual <= 0f)
            return;

        vidaActual = Mathf.Max(0f, vidaActual - cantidad);

        NotificarCambio();
    }

    public void Curar(float cantidad)
    {
        if (cantidad <= 0f || vidaActual <= 0f)
            return;

        vidaActual = Mathf.Min(
            vidaMaxima,
            vidaActual + cantidad
        );

        NotificarCambio();
    }

    void NotificarCambio()
    {
        VidaCambiada?.Invoke(vidaActual, vidaMaxima);

        Debug.Log(
            $"Vida del jugador: {vidaActual}/{vidaMaxima}"
        );
    }
}
