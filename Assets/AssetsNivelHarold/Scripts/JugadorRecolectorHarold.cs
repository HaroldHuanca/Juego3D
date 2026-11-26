
using System;
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

    [Header("Notificaciones")]
    [SerializeField] private UIControladorNotificaciones notificaciones;

    private ObjetoInteractuable objetoDetectado;

    public event Action<ObjetoInteractuable> ObjetoDetectadoCambiado;

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
        ObjetoInteractuable nuevoObjeto = null;

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
            nuevoObjeto =
                impacto.collider.GetComponentInParent<ObjetoInteractuable>();

            if (nuevoObjeto != null &&
                !nuevoObjeto.Recolectable)
            {
                nuevoObjeto = null;
            }
        }

        if (nuevoObjeto != objetoDetectado)
        {
            objetoDetectado = nuevoObjeto;

            ObjetoDetectadoCambiado?.Invoke(objetoDetectado);
        }
    }

    void IntentarRecoger()
    {
        if (inventario == null || objetoDetectado == null)
            return;

        ObjetoInteractuable objeto = objetoDetectado;

        string nombre = objeto.NombreObjeto;
        Sprite icono = objeto.Icono;

        if (inventario.AgregarObjeto(objeto))
        {
            objeto.Recoger();

            objetoDetectado = null;
            ObjetoDetectadoCambiado?.Invoke(null);

            if (notificaciones != null)
            {
                notificaciones.Mostrar(
                    "Has encontrado: " + nombre,
                    icono
                );
            }

            inventario.MostrarInventario();
        }
    }

    void OnDisable()
    {
        objetoDetectado = null;
        ObjetoDetectadoCambiado?.Invoke(null);
    }
}
