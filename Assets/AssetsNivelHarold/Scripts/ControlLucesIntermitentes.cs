
using UnityEngine;

public class ControlLucesIntermitentes : MonoBehaviour
{
    [Header("Objetos Light")]
    public Light[] luces;

    [Header("Tiempo encendida (segundos)")]
    [Min(0.05f)]
    public float minimoEncendida = 0.5f;

    [Min(0.05f)]
    public float maximoEncendida = 4f;

    [Header("Tiempo apagada (segundos)")]
    [Min(0.05f)]
    public float minimoApagada = 0.1f;

    [Min(0.05f)]
    public float maximoApagada = 1.5f;

    private float[] temporizadores;
    private bool[] estados;
    private bool parpadeoActivo;

    void Start()
    {
        ApagarTodas();
    }

    void Update()
    {
        if (!parpadeoActivo || luces == null)
            return;

        for (int i = 0; i < luces.Length; i++)
        {
            if (luces[i] == null)
                continue;

            temporizadores[i] -= Time.deltaTime;

            if (temporizadores[i] <= 0f)
            {
                // Cambiar el estado de esta luz.
                estados[i] = !estados[i];

                luces[i].enabled = estados[i];

                // Elegir cuánto durará el nuevo estado.
                if (estados[i])
                {
                    temporizadores[i] = Random.Range(
                        minimoEncendida,
                        Mathf.Max(minimoEncendida, maximoEncendida)
                    );
                }
                else
                {
                    temporizadores[i] = Random.Range(
                        minimoApagada,
                        Mathf.Max(minimoApagada, maximoApagada)
                    );
                }
            }
        }
    }

    void ApagarTodas()
    {
        if (luces == null)
            return;

        foreach (Light luz in luces)
        {
            if (luz != null)
                luz.enabled = false;
        }
    }

    public void IniciarParpadeo()
    {
        if (luces == null)
            return;

        temporizadores = new float[luces.Length];
        estados = new bool[luces.Length];

        for (int i = 0; i < luces.Length; i++)
        {
            if (luces[i] == null)
                continue;

            // Todas comienzan encendidas.
            estados[i] = true;
            luces[i].enabled = true;

            // Cada luz recibe su propio tiempo aleatorio.
            temporizadores[i] = Random.Range(
                minimoEncendida,
                Mathf.Max(minimoEncendida, maximoEncendida)
            );
        }

        parpadeoActivo = true;

        Debug.Log("Parpadeo aleatorio iniciado.");
    }

    public void DetenerParpadeo()
    {
        parpadeoActivo = false;

        ApagarTodas();

        Debug.Log("Parpadeo detenido.");
    }

    public void ApagarLuces()
    {
        DetenerParpadeo();
    }
}
