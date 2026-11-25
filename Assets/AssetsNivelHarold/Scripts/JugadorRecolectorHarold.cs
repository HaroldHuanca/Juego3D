
using UnityEngine;
using UnityEngine.InputSystem;

public class JugadorRecolectorHarold : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private Camera camara;
    [SerializeField] private float distanciaInteraccion = 2.5f;
    [SerializeField] private LayerMask capasDetectables = ~0;

    [Header("Inventario")]
    [SerializeField] private InventarioJugador inventario;

    private ObjetoInteractuable objetoDetectado;

    void Awake()
    {
        if (camara == null)
            camara = GetComponentInChildren<Camera>();

        if (inventario == null)
            inventario = GetComponent<InventarioJugador>();
    }

    void Update()
    {
        DetectarObjeto();

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame &&
            objetoDetectado != null)
        {
            IntentarRecoger();
        }
    }

    void DetectarObjeto()
    {
        objetoDetectado = null;

        if (camara == null)
            return;

        Ray rayo = new Ray(
            camara.transform.position,
            camara.transform.forward
        );

        if (Physics.Raycast(
            rayo,
            out RaycastHit impacto,
            distanciaInteraccion,
            capasDetectables,
            QueryTriggerInteraction.Ignore))
        {
            objetoDetectado =
                impacto.collider.GetComponentInParent<ObjetoInteractuable>();

            if (objetoDetectado != null &&
                !objetoDetectado.Recolectable)
            {
                objetoDetectado = null;
            }
        }
    }

    void IntentarRecoger()
    {
        if (inventario == null)
        {
            Debug.LogError("No se encontró el InventarioJugador.");
            return;
        }

        if (inventario.AgregarObjeto(objetoDetectado))
        {
            Debug.Log(
                "Recolectaste: " + objetoDetectado.NombreObjeto
            );

            objetoDetectado.Recoger();

            inventario.MostrarInventario();

            objetoDetectado = null;
        }
    }
}
