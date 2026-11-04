using UnityEngine;

namespace Tienda.Productos
{
    [CreateAssetMenu(fileName = "NuevoProducto", menuName = "Tienda/Producto", order = 1)]
    public class ProductoData : ScriptableObject
    {
        [Header("Información Básica")]
        [Tooltip("Identificador único interno (ej: prod_soda_01)")]
        [SerializeField] private string id = "prod_01";

        [Tooltip("Nombre que verá el jugador en la caja y estanterías")]
        [SerializeField] private string nombre = "Refresco de Cola";

        [Tooltip("Precio de venta al público")]
        [Min(0.01f)]
        [SerializeField] private float precio = 2.50f;

        [Header("Representación Visual")]
        [Tooltip("Icono 2D para la interfaz de la caja registradora y pedidos")]
        [SerializeField] private Sprite icono;

        [Tooltip("Prefab 3D del producto para colocar en estantes de la tienda o almacén")]
        [SerializeField] private GameObject prefab3D;

        [Header("Datos de Stock")]
        [Tooltip("Cantidad inicial disponible en los estantes de la tienda")]
        [SerializeField] private int stockTiendaInicial = 5;

        [Tooltip("Cantidad de reserva disponible en el almacén de terror")]
        [SerializeField] private int stockAlmacenInicial = 10;

        // Propiedades públicas de solo lectura (inmutabilidad segura)
        public string Id => id;
        public string Nombre => nombre;
        public float Precio => precio;
        public Sprite Icono => icono;
        public GameObject Prefab3D => prefab3D;
        public int StockTiendaInicial => stockTiendaInicial;
        public int StockAlmacenInicial => stockAlmacenInicial;
    }
}
