
using UnityEngine;
using System.Collections;

public class ObjetoAleatorio : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float intervalo = 20f;
    [SerializeField] private float probabilidad = 0.5f;

    private Renderer objetoRenderer;
    public bool EstaVisible => objetoRenderer != null && objetoRenderer.enabled;
    void Start()
    {
        objetoRenderer = GetComponent<Renderer>();

        if (objetoRenderer == null)
        {
            Debug.LogError("No se encontró un Renderer en " + gameObject.name);
            return;
        }

        StartCoroutine(ControlarVisibilidad());
    }

    IEnumerator ControlarVisibilidad()
    {
        while (true)
        {
            yield return new WaitForSeconds(intervalo);

            // Decisión aleatoria
            if (objetoRenderer.enabled && Random.value <= probabilidad)
            {
                objetoRenderer.enabled = false;

                if (!objetoRenderer.enabled)
                {
                    Debug.LogWarning("¡ALERTA! El objeto " + gameObject.name + " ha desaparecido.");
                }
            }
        }
    }
    public void MostrarObjeto()
{
    if (objetoRenderer == null)
        return;

    objetoRenderer.enabled = true;

    Debug.Log($"[Objeto] {gameObject.name} ha sido rellenado y ahora es visible.");
}
}
