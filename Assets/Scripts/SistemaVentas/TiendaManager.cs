using System;
using System.Collections.Generic;
using UnityEngine;
using Tienda.Productos;

namespace Tienda.SistemaVentas
{
    [DefaultExecutionOrder(-50)]
    public class TiendaManager : MonoBehaviour
    {
        public static TiendaManager Instance { get; private set; }

        [Header("Configuración Económica")]
        [Tooltip("Dinero inicial en la caja registradora para dar cambio")]
        [SerializeField] private float fondoCajaInicial = 100.00f;

        [Header("Catálogo de Productos")]
        [Tooltip("Lista de productos disponibles que pueden pedir los clientes")]
        [SerializeField] private List<ProductoData> catalogoProductos = new List<ProductoData>();

        [Header("Estado de la Sesión")]
        [SerializeField] private float dineroEnCaja;
        [SerializeField] private int ventasExitosas;
        [SerializeField] private int ventasPerdidas;

        [Header("Pruebas y Depuración")]
        [Tooltip("Si está activo, genera un pedido automático de prueba al iniciar para testear la UI de la caja")]
        [SerializeField] private bool generarPedidoPruebaAlIniciar = false;

        // Pedido activo que está siendo atendido en el mostrador
        private PedidoCliente pedidoActual;

        // Eventos para conectar la UI sin acoplamiento
        public event Action<float> OnDineroCajaModificado;
        public event Action<PedidoCliente> OnNuevoPedidoGenerado;
        public event Action OnVentaFinalizadaConExito;
        public event Action<string> OnVentaCancelada;

        // Propiedades públicas
        public float DineroEnCaja => dineroEnCaja;
        public int VentasExitosas => ventasExitosas;
        public int VentasPerdidas => ventasPerdidas;
        public PedidoCliente PedidoActual => pedidoActual;
        public bool HayPedidoPendiente => pedidoActual != null;
        public List<ProductoData> CatalogoProductos => catalogoProductos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            dineroEnCaja = fondoCajaInicial;

#if UNITY_EDITOR
            if (catalogoProductos == null || catalogoProductos.Count == 0)
            {
                CargarProductosDesdeProyecto();
            }
#endif
        }

#if UNITY_EDITOR
        private void Reset()
        {
            CargarProductosDesdeProyecto();
        }

        [ContextMenu("Cargar Todos los Productos del Proyecto")]
        public void CargarProductosDesdeProyecto()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ProductoData");
            if (catalogoProductos == null) catalogoProductos = new List<ProductoData>();
            catalogoProductos.Clear();

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prod = UnityEditor.AssetDatabase.LoadAssetAtPath<ProductoData>(path);
                if (prod != null && !catalogoProductos.Contains(prod))
                {
                    catalogoProductos.Add(prod);
                }
            }
            Debug.Log($"[TiendaManager] Se cargaron automáticamente {catalogoProductos.Count} productos al catálogo.");
        }
#endif

        private void Start()
        {
            pedidoActual = null; // Iniciar siempre limpio, esperando al primer cliente
        }

        /// <summary>
        /// Genera un pedido aleatorio a partir de los productos en catálogo y asigna un billete de pago coherente.
        /// </summary>
        public PedidoCliente GenerarPedidoParaCliente()
        {
            if (catalogoProductos == null || catalogoProductos.Count == 0)
            {
                Debug.LogWarning("[TiendaManager] No hay productos en el catálogo para generar un pedido.");
                return null;
            }

            PedidoCliente nuevoPedido = new PedidoCliente();
            int cantidadItemsDistintos = UnityEngine.Random.Range(1, Mathf.Min(4, catalogoProductos.Count + 1));

            // Seleccionar productos aleatorios sin repetir
            List<ProductoData> disponibles = new List<ProductoData>(catalogoProductos);
            for (int i = 0; i < cantidadItemsDistintos; i++)
            {
                int indexAleatorio = UnityEngine.Random.Range(0, disponibles.Count);
                ProductoData prod = disponibles[indexAleatorio];
                disponibles.RemoveAt(indexAleatorio);

                int cantidad = UnityEngine.Random.Range(1, 3);
                nuevoPedido.lineas.Add(new LineaPedido(prod, cantidad));
            }

            float total = nuevoPedido.TotalPagar;

            // Determinar con qué billete pagará el cliente ($5, $10, $20, $50, $100)
            float[] denominaciones = { 5f, 10f, 20f, 50f, 100f };
            float billete = 100f;
            foreach (float den in denominaciones)
            {
                if (den >= total)
                {
                    billete = den;
                    break;
                }
            }
            nuevoPedido.billeteEntregado = billete;

            EstablecerPedidoActivo(nuevoPedido);
            Debug.Log($"[TiendaManager] Pedido Cliente: Total = ${total:F2} | Billete = ${billete:F2} | Vuelto Requerido = ${nuevoPedido.VueltoRequerido:F2}");
            return nuevoPedido;
        }

        [ContextMenu("Generar Pedido de Prueba")]
        public PedidoCliente GenerarPedidoAleatorioPrueba()
        {
            return GenerarPedidoParaCliente();
        }

        public bool EstablecerPedidoActivo(PedidoCliente pedido)
        {
            // Bloqueo de seguridad: Si ya hay un pedido activo siendo cobrado, no permitir que otro cliente lo pise
            if (pedidoActual != null && pedido != null)
            {
                Debug.LogWarning("[TiendaManager] Ya hay un cliente siendo atendido en caja. El siguiente cliente debe esperar.");
                return false;
            }

            pedidoActual = pedido;
            OnNuevoPedidoGenerado?.Invoke(pedidoActual);
            return true;
        }

        public void LimpiarPedidoActivo()
        {
            pedidoActual = null;
            OnNuevoPedidoGenerado?.Invoke(null);
        }

        /// <summary>
        /// Valida el vuelto entregado por el jugador desde la interfaz de la caja registradora.
        /// </summary>
        public bool ProcesarCobro(float vueltoEntregado, out string mensajeResultado)
        {
            if (pedidoActual == null)
            {
                mensajeResultado = "No hay ningún cliente o pedido activo en la caja.";
                return false;
            }

            if (pedidoActual.EsVueltoCorrecto(vueltoEntregado))
            {
                // El vuelto es exacto: cobro exitoso
                dineroEnCaja += pedidoActual.TotalPagar;
                ventasExitosas++;

                mensajeResultado = $"Cobro exitoso. Total ingresado: ${pedidoActual.TotalPagar:F2}";
                OnDineroCajaModificado?.Invoke(dineroEnCaja);
                OnVentaFinalizadaConExito?.Invoke();

                pedidoActual = null;
                return true;
            }
            else
            {
                // El vuelto es incorrecto
                float diferencia = vueltoEntregado - pedidoActual.VueltoRequerido;
                if (diferencia > 0)
                {
                    mensajeResultado = $"Estás entregando de más (+${diferencia:F2}).";
                }
                else
                {
                    mensajeResultado = $"Falta dinero en el vuelto (-${Mathf.Abs(diferencia):F2}).";
                }
                return false;
            }
        }

        /// <summary>
        /// Registra la pérdida de una venta (ej. si el cliente se marcha por falta de paciencia).
        /// </summary>
        public void RegistrarVentaPerdida(string motivo = "Tiempo de espera agotado")
        {
            ventasPerdidas++;
            Debug.LogWarning($"[TiendaManager] ¡Venta perdida! Motivo: {motivo} | Total pérdidas: {ventasPerdidas}");
            OnVentaCancelada?.Invoke(motivo);
            pedidoActual = null;
        }
    }
}
