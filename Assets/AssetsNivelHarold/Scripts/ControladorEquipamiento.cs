
using UnityEngine;
using UnityEngine.InputSystem;

public class ControladorEquipamiento : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private InventarioJugador inventario;

    [Header("Punto de equipamiento")]
    [SerializeField] private Transform puntoEquipamiento;

    [Header("Posición del objeto")]
    [SerializeField]
    private Vector3 posicionLocal =
        new Vector3(0.25f, -0.25f, 0.55f);

    [SerializeField]
    private Vector3 rotacionLocal =
        Vector3.zero;

    private EntradaInventario objetoEquipado;
    private GameObject objetoVisual;
    public GameObject ObjetoVisual => objetoVisual;

    public EntradaInventario ObjetoEquipado => objetoEquipado;

    void Awake()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioJugador>();

        if (inventario == null)
            Debug.LogError("No se encontró InventarioJugador.");
    }

    void OnEnable()
    {
        if (inventario != null)
        {
            inventario.InventarioCambiado += ActualizarInventario;
            ActualizarInventario();
        }
    }

    void Update()
    {
        if (Keyboard.current == null || inventario == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SeleccionarRanura(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SeleccionarRanura(1);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SeleccionarRanura(2);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            SeleccionarRanura(3);

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            SeleccionarRanura(4);

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            SeleccionarRanura(5);

        if (Keyboard.current.digit7Key.wasPressedThisFrame)
            SeleccionarRanura(6);

        if (Keyboard.current.digit8Key.wasPressedThisFrame)
            SeleccionarRanura(7);

        if (Keyboard.current.digit9Key.wasPressedThisFrame)
            SeleccionarRanura(8);

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
            Desequipar();
    }

    void SeleccionarRanura(int indice)
    {
        EntradaInventario entrada =
            inventario.ObtenerObjetoEnIndice(indice);

        if (entrada == null)
        {
            Desequipar();
            return;
        }

        Equipar(entrada);
    }

    void Equipar(EntradaInventario entrada)
    {
        if (entrada == null || entrada.definicion == null)
            return;

        objetoEquipado = entrada;

        DestruirObjetoVisual();

        GameObject prefab = entrada.definicion.PrefabVisual;

        if (prefab != null && puntoEquipamiento != null)
        {
            objetoVisual = Instantiate(
                prefab,
                puntoEquipamiento,
                false
            );

            objetoVisual.transform.localPosition = posicionLocal;
            objetoVisual.transform.localEulerAngles =
    rotacionLocal + entrada.definicion.RotacionEquipamiento;
        }
        else if (prefab != null)
        {
            Debug.LogWarning(
                "Falta asignar el punto de equipamiento."
            );
        }

        Debug.Log("Objeto equipado: " + entrada.nombre);
    }

    void Desequipar()
    {
        objetoEquipado = null;
        DestruirObjetoVisual();

        Debug.Log("Objeto guardado.");
    }

    void ActualizarInventario()
    {
        if (objetoEquipado != null &&
            !inventario.Contiene(objetoEquipado))
        {
            Desequipar();
        }
    }

    void DestruirObjetoVisual()
    {
        if (objetoVisual != null)
        {
            Destroy(objetoVisual);
            objetoVisual = null;
        }
    }

    void OnDisable()
    {
        if (inventario != null)
            inventario.InventarioCambiado -= ActualizarInventario;

        DestruirObjetoVisual();
    }
}
