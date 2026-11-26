
using UnityEngine;

public class AccionLinterna : AccionHerramienta
{
    [Header("Fuente de luz")]
    [SerializeField] private Light luz;

    [Header("Estado")]
    [SerializeField] private bool encendida = false;

    public bool Encendida => encendida;

    void Awake()
    {
        if (luz == null)
            luz = GetComponentInChildren<Light>(true);

        ActualizarLuz();
    }

    public override void Ejecutar()
    {
        encendida = !encendida;

        ActualizarLuz();

        Debug.Log(
            encendida
                ? "Linterna encendida."
                : "Linterna apagada."
        );
    }

    void ActualizarLuz()
    {
        if (luz != null)
            luz.enabled = encendida;
        else
            Debug.LogWarning("La linterna no tiene una fuente de luz.");
    }
}
