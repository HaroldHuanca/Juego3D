
using UnityEngine;

public class ControladorEventos : MonoBehaviour
{
    [Header("Controladores")]
    public ControlLuces controlLuces;
    public ControlLucesIntermitentes controlIntermitentes;

    [Header("Prueba automática")]
    public float intervalo = 10f;

    private float temporizador = 0f;
    private bool apagonActivo = false;

    void Start()
    {
        Debug.Log("===== CONTROLADOR DE EVENTOS INICIADO =====");

        if (controlLuces == null)
            Debug.LogError("Falta asignar ControlLuces.");

        if (controlIntermitentes == null)
            Debug.LogError("Falta asignar ControlLucesIntermitentes.");

        Debug.Log("Primer evento en " + intervalo + " segundos.");
    }

    void Update()
    {
        temporizador += Time.deltaTime;

        if (temporizador >= intervalo)
        {
            temporizador = 0f;

            if (!apagonActivo)
            {
                ActivarApagon();
            }
            else
            {
                RestaurarIluminacion();
            }
        }
    }

    public void ActivarApagon()
    {
        Debug.Log("===== EVENTO: APAGÓN =====");

        if (controlLuces != null)
            controlLuces.ApagarLuces();

        if (controlIntermitentes != null)
            controlIntermitentes.IniciarParpadeo();

        apagonActivo = true;
    }

    public void RestaurarIluminacion()
    {
        Debug.Log("===== EVENTO: RESTAURACIÓN =====");

        if (controlIntermitentes != null)
            controlIntermitentes.DetenerParpadeo();

        if (controlLuces != null)
            controlLuces.EncenderLuces();

        apagonActivo = false;
    }

    public void ActivarIntermitentes()
    {
        if (controlIntermitentes != null)
            controlIntermitentes.IniciarParpadeo();
    }

    public void DetenerIntermitentes()
    {
        if (controlIntermitentes != null)
            controlIntermitentes.DetenerParpadeo();
    }
}
