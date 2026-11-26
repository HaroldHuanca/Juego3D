
using UnityEngine;
using TMPro;

public class UIControladorInteracciones : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private JugadorRecolectorHarold recolector;

    [Header("Elementos UI")]
    [SerializeField] private GameObject panelInteraccion;
    [SerializeField] private TMP_Text textoInteraccion;

    void Start()
    {
        if (recolector == null)
        {
            Debug.LogError("Falta asignar JugadorRecolectorHarold.");
            return;
        }

        recolector.ObjetoDetectadoCambiado += ActualizarIndicador;

        ActualizarIndicador(null);
    }

    void ActualizarIndicador(ObjetoInteractuable objeto)
    {
        bool mostrar = objeto != null;

        if (panelInteraccion != null)
            panelInteraccion.SetActive(mostrar);

        if (mostrar && textoInteraccion != null)
        {
            textoInteraccion.text =
                "E  ·  Recoger " + objeto.NombreObjeto;
        }
    }

    void OnDestroy()
    {
        if (recolector != null)
            recolector.ObjetoDetectadoCambiado -= ActualizarIndicador;
    }
}
