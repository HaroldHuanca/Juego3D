
using UnityEngine;

public class UIControladorInventario : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private InventarioJugador inventario;
    [SerializeField] private Transform contenedorObjetos;
    [SerializeField] private ElementoInventarioUI prefabObjeto;

    void Start()
    {
        if (inventario == null ||
            contenedorObjetos == null ||
            prefabObjeto == null)
        {
            Debug.LogError(
                "Faltan referencias en UIControladorInventario."
            );
            return;
        }

        inventario.InventarioCambiado += ActualizarInventario;

        ActualizarInventario();
    }

    void ActualizarInventario()
    {
        foreach (Transform hijo in contenedorObjetos)
        {
            Destroy(hijo.gameObject);
        }

        foreach (EntradaInventario objeto in inventario.Objetos)
        {
            ElementoInventarioUI elemento = Instantiate(
                prefabObjeto,
                contenedorObjetos
            );

            elemento.Configurar(objeto);
        }
    }

    void OnDestroy()
    {
        if (inventario != null)
            inventario.InventarioCambiado -= ActualizarInventario;
    }
}
