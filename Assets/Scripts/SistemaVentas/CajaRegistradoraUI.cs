using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Tienda.Productos;

namespace Tienda.SistemaVentas
{
    public class CajaRegistradoraUI : MonoBehaviour
    {
        public static CajaRegistradoraUI Instance { get; private set; }

        [Header("Estado y Panel")]
        [SerializeField] private GameObject rootUI;
        [SerializeField] private GameObject panelCaja;
        [SerializeField] private bool estaAbierta = false;

        [Header("Visualización del Pedido")]
        [SerializeField] private Text textoTotalPagar;
        [SerializeField] private Text textoBilleteRecibido;
        [SerializeField] private Text textoVueltoRequerido;
        [SerializeField] private Text textoVueltoIngresado;
        [SerializeField] private Text textoEstadoVuelto;
        [SerializeField] private Text textoDetalleProductos;
        [SerializeField] private Text textoMensajeFeedback;
        [SerializeField] private Text textoDineroEnCaja;

        [Header("Audio Opcional")]
        [SerializeField] private AudioClip sonidoCobroExitoso;
        [SerializeField] private AudioClip sonidoError;
        [SerializeField] private AudioSource audioSource;

        private float vueltoActualIngresado = 0f;
        private Font fuenteUI;
        private Camara scriptCamara;
        private PlayerMovement scriptMovimiento;
        private float tiempoParaPoderCerrar = 0f;

        public bool EstaAbierta => estaAbierta;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scriptCamara = FindObjectOfType<Camara>();
            scriptMovimiento = FindObjectOfType<PlayerMovement>();

            AsegurarEventSystem();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // Construir la interfaz procedural moderna
            ConstruirInterfazProcedural();

            // Iniciar completamente oculta
            if (rootUI != null) rootUI.SetActive(false);
            else if (panelCaja != null) panelCaja.SetActive(false);
        }

        private void AsegurarEventSystem()
        {
            if (EventSystem.current == null)
            {
                GameObject esObj = new GameObject("EventSystem_Tienda", typeof(EventSystem), typeof(InputSystemUIInputModule));
                if (Application.isPlaying) DontDestroyOnLoad(esObj);
            }
            else
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    var viejo = EventSystem.current.GetComponent<StandaloneInputModule>();
                    if (viejo != null) Destroy(viejo);
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
        }

        private void Start()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado += ActualizarDineroCajaUI;
                TiendaManager.Instance.OnNuevoPedidoGenerado += (pedido) => ActualizarPantallaPedido();
                ActualizarDineroCajaUI(TiendaManager.Instance.DineroEnCaja);
            }
        }

        private void OnDestroy()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado -= ActualizarDineroCajaUI;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (estaAbierta)
            {
                // Escape cierra de inmediato
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CerrarCaja();
                }
                // La tecla E solo puede cerrarlo si han pasado al menos 0.25 segundos desde que se abrió
                else if (Keyboard.current.eKey.wasPressedThisFrame && Time.unscaledTime >= tiempoParaPoderCerrar)
                {
                    CerrarCaja();
                }
            }
            else
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public void AbrirCaja()
        {
            estaAbierta = true;
            tiempoParaPoderCerrar = Time.unscaledTime + 0.25f;

            if (rootUI == null || panelCaja == null)
            {
                Debug.LogWarning("[CajaRegistradoraUI] Interfaz no encontrada en memoria. Reconstruyendo al vuelo...");
                ConstruirInterfazProcedural();
            }

            if (rootUI != null) rootUI.SetActive(true);
            if (panelCaja != null) panelCaja.SetActive(true);

            // Pausar rotación y movimiento
            if (scriptCamara == null) scriptCamara = FindObjectOfType<Camara>();
            if (scriptMovimiento == null) scriptMovimiento = FindObjectOfType<PlayerMovement>();
            if (scriptCamara != null) scriptCamara.enabled = false;
            if (scriptMovimiento != null) scriptMovimiento.enabled = false;

            // Liberar ratón
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LimpiarVuelto();
            ActualizarPantallaPedido();
        }

        public void CerrarCaja()
        {
            estaAbierta = false;

            if (rootUI != null) rootUI.SetActive(false);
            if (panelCaja != null) panelCaja.SetActive(false);

            // Reactivar jugador
            if (scriptCamara != null) scriptCamara.enabled = true;
            if (scriptMovimiento != null) scriptMovimiento.enabled = true;

            // Bloquear cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void AgregarDineroVuelto(float cantidad)
        {
            vueltoActualIngresado = (float)Math.Round(vueltoActualIngresado + cantidad, 2);
            ActualizarTextoVueltoIngresado();
        }

        public void LimpiarVuelto()
        {
            vueltoActualIngresado = 0f;
            ActualizarTextoVueltoIngresado();
            MostrarMensaje("", Color.white);
        }

        public void ConfirmarCobro()
        {
            if (TiendaManager.Instance == null)
            {
                MostrarMensaje("Error: No existe TiendaManager en la escena.", Color.red);
                return;
            }

            if (!TiendaManager.Instance.HayPedidoPendiente)
            {
                MostrarMensaje("No hay ningún cliente activo para cobrar.", new Color(1f, 0.75f, 0.2f));
                return;
            }

            bool exito = TiendaManager.Instance.ProcesarCobro(vueltoActualIngresado, out string mensaje);

            if (exito)
            {
                MostrarMensaje("¡VENTA COMPLETADA! " + mensaje, new Color(0.25f, 0.98f, 0.45f));
                if (sonidoCobroExitoso != null) audioSource.PlayOneShot(sonidoCobroExitoso);

                LimpiarVuelto();
                ActualizarPantallaPedido();
                StartCoroutine(RutinaCerrarTrasCobro(1.2f));
            }
            else
            {
                MostrarMensaje("¡VUELTO INCORRECTO! " + mensaje, new Color(1f, 0.35f, 0.30f));
                if (sonidoError != null) audioSource.PlayOneShot(sonidoError);
            }
        }

        private IEnumerator RutinaCerrarTrasCobro(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (estaAbierta)
            {
                CerrarCaja();
            }
        }

        /// <summary>
        /// Mantenido por compatibilidad de interfaz (el botón visual de prueba ha sido eliminado).
        /// </summary>
        public void GenerarNuevoPedidoPrueba()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.GenerarPedidoAleatorioPrueba();
                LimpiarVuelto();
                ActualizarPantallaPedido();
                MostrarMensaje("Pedido de prueba generado.", Color.cyan);
            }
        }

        private void ActualizarPantallaPedido()
        {
            if (TiendaManager.Instance == null) return;

            PedidoCliente pedido = TiendaManager.Instance.PedidoActual;

            if (pedido != null)
            {
                if (textoTotalPagar != null)
                    textoTotalPagar.text = $"Total a Pagar:  ${pedido.TotalPagar:F2}";

                if (textoBilleteRecibido != null)
                    textoBilleteRecibido.text = $"Cliente Entrega:  💵 ${pedido.billeteEntregado:F2}";

                if (textoVueltoRequerido != null)
                    textoVueltoRequerido.text = $"Vuelto Requerido:  🪙 ${pedido.VueltoRequerido:F2}";

                if (textoDetalleProductos != null)
                {
                    string detalle = "";
                    foreach (var linea in pedido.lineas)
                    {
                        string icono = "🏷️";
                        string nombreProd = linea.producto != null ? linea.producto.Nombre : "Artículo";
                        if (nombreProd.ToLower().Contains("refresco") || nombreProd.ToLower().Contains("cola") || nombreProd.ToLower().Contains("soda") || nombreProd.ToLower().Contains("jugo"))
                            icono = "🥤";
                        else if (nombreProd.ToLower().Contains("galleta") || nombreProd.ToLower().Contains("snack") || nombreProd.ToLower().Contains("papas") || nombreProd.ToLower().Contains("chips"))
                            icono = "🍿";
                        else if (nombreProd.ToLower().Contains("chocolate") || nombreProd.ToLower().Contains("dulce") || nombreProd.ToLower().Contains("caramelo"))
                            icono = "🍫";
                        else if (nombreProd.ToLower().Contains("pan") || nombreProd.ToLower().Contains("sandwich"))
                            icono = "🥪";

                        detalle += $"{icono} {nombreProd}  x{linea.cantidad}\n";
                        detalle += $"    <color=#94A3B8>(${linea.producto.Precio:F2} c/u)</color>   <color=#38BDF8><b>${linea.Subtotal:F2}</b></color>\n\n";
                    }
                    textoDetalleProductos.text = detalle.TrimEnd();
                }
            }
            else
            {
                if (textoTotalPagar != null) textoTotalPagar.text = "Total a Pagar:  $0.00";
                if (textoBilleteRecibido != null) textoBilleteRecibido.text = "Cliente Entrega:  $0.00";
                if (textoVueltoRequerido != null) textoVueltoRequerido.text = "Vuelto Requerido:  $0.00";
                if (textoDetalleProductos != null)
                {
                    textoDetalleProductos.text = "⏳ <color=#94A3B8>Esperando cliente en el mostrador...</color>\n\n" +
                                                 "Los clientes de la tienda harán fila frente a la caja con el icono <b>($)</b> sobre su cabeza para pagar.";
                }
            }

            ActualizarTextoVueltoIngresado();
        }

        private void ActualizarTextoVueltoIngresado()
        {
            if (textoVueltoIngresado != null)
            {
                textoVueltoIngresado.text = $"Vuelto Ingresado:  ${vueltoActualIngresado:F2}";
            }

            if (textoEstadoVuelto != null)
            {
                if (TiendaManager.Instance == null || !TiendaManager.Instance.HayPedidoPendiente)
                {
                    textoEstadoVuelto.text = "Sin cliente activo en el mostrador";
                    textoEstadoVuelto.color = new Color(0.6f, 0.65f, 0.72f);
                    return;
                }

                float vueltoReq = TiendaManager.Instance.PedidoActual.VueltoRequerido;
                float diff = (float)Math.Round(vueltoActualIngresado - vueltoReq, 2);

                if (Mathf.Abs(diff) < 0.01f)
                {
                    textoEstadoVuelto.text = "✅ ¡VUELTO EXACTO! Presiona Confirmar Cobro";
                    textoEstadoVuelto.color = new Color(0.35f, 1f, 0.55f);
                }
                else if (diff < 0f)
                {
                    textoEstadoVuelto.text = $"⚠️ Faltan: ${Mathf.Abs(diff):F2} por entregar";
                    textoEstadoVuelto.color = new Color(1f, 0.85f, 0.25f);
                }
                else
                {
                    textoEstadoVuelto.text = $"⛔ Te pasas por: +${diff:F2} de más";
                    textoEstadoVuelto.color = new Color(1f, 0.40f, 0.35f);
                }
            }
        }

        private void ActualizarDineroCajaUI(float dinero)
        {
            if (textoDineroEnCaja != null)
            {
                textoDineroEnCaja.text = $"💰 Saldo en Caja:  ${dinero:F2}";
            }
        }

        private void MostrarMensaje(string mensaje, Color color)
        {
            if (textoMensajeFeedback != null)
            {
                textoMensajeFeedback.text = mensaje;
                textoMensajeFeedback.color = color;
            }
        }

        #region Construcción Procedural Moderna

        private void ConstruirInterfazProcedural()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            GameObject canvasObj;

            if (canvas == null)
            {
                canvasObj = new GameObject("Canvas_CajaRegistradora", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
            }
            else
            {
                canvasObj = canvas.gameObject;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Máxima prioridad visual modal

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Sprite spriteRedondo = HUDTienda.ObtenerSpriteRedondeado();

            // 1. Contenedor Raíz
            rootUI = CrearObjetoUI("Root_CajaRegistradora", canvas.transform);
            var rectRoot = rootUI.GetComponent<RectTransform>();
            rectRoot.anchorMin = Vector2.zero;
            rectRoot.anchorMax = Vector2.one;
            rectRoot.offsetMin = rectRoot.offsetMax = Vector2.zero;

            var imgCortina = rootUI.AddComponent<Image>();
            imgCortina.color = new Color(0.04f, 0.05f, 0.08f, 0.65f);
            imgCortina.raycastTarget = false;

            // 2. Ventana Terminal Central Kiosk (1080 x 700)
            panelCaja = CrearObjetoUI("Ventana_Terminal_POS", rootUI.transform);
            var rectVentana = panelCaja.GetComponent<RectTransform>();
            rectVentana.anchorMin = rectVentana.anchorMax = rectVentana.pivot = new Vector2(0.5f, 0.5f);
            rectVentana.sizeDelta = new Vector2(1080, 700);
            rectVentana.anchoredPosition = Vector2.zero;

            var imgFondo = panelCaja.AddComponent<Image>();
            imgFondo.sprite = spriteRedondo;
            imgFondo.type = Image.Type.Sliced;
            imgFondo.color = new Color(0.09f, 0.11f, 0.15f, 0.98f);
            imgFondo.raycastTarget = true;

            // Borde sutil ventana
            var bordeVentana = CrearObjetoUI("Borde_Ventana", panelCaja.transform);
            var rectBordeV = bordeVentana.GetComponent<RectTransform>();
            rectBordeV.anchorMin = Vector2.zero;
            rectBordeV.anchorMax = Vector2.one;
            rectBordeV.sizeDelta = Vector2.zero;
            var imgBordeV = bordeVentana.AddComponent<Image>();
            imgBordeV.sprite = spriteRedondo;
            imgBordeV.type = Image.Type.Sliced;
            imgBordeV.color = new Color(0.25f, 0.32f, 0.42f, 0.35f);
            imgBordeV.raycastTarget = false;

            // BARRA DE TÍTULO
            var barraTitulo = CrearObjetoUI("Barra_Titulo", panelCaja.transform);
            var rectBarra = barraTitulo.GetComponent<RectTransform>();
            rectBarra.anchorMin = new Vector2(0, 1);
            rectBarra.anchorMax = new Vector2(1, 1);
            rectBarra.pivot = new Vector2(0.5f, 1f);
            rectBarra.sizeDelta = new Vector2(-16, 56);
            rectBarra.anchoredPosition = new Vector2(0, -8);
            var imgBarra = barraTitulo.AddComponent<Image>();
            imgBarra.sprite = spriteRedondo;
            imgBarra.type = Image.Type.Sliced;
            imgBarra.color = new Color(0.06f, 0.07f, 0.10f, 1f);

            CrearTextoUI("Titulo", barraTitulo.transform, "🏪 6-TWELVE STORE · TERMINAL POS #01", 20, TextAnchor.MiddleLeft,
                new Vector2(25, 0), new Vector2(480, 40), new Color(0.95f, 0.88f, 0.65f), true);

            textoDineroEnCaja = CrearTextoUI("DineroEnCaja", barraTitulo.transform, "💰 Saldo en Caja:  $100.00", 18, TextAnchor.MiddleRight,
                new Vector2(-25, 0), new Vector2(400, 40), new Color(0.35f, 0.95f, 0.55f), true);

            // ==========================================
            // PANEL IZQUIERDO: TICKET DE VENTA (490 x 510)
            // ==========================================
            var cardIzq = CrearObjetoUI("Card_Ticket", panelCaja.transform);
            var rectCardIzq = cardIzq.GetComponent<RectTransform>();
            rectCardIzq.anchorMin = rectCardIzq.anchorMax = rectCardIzq.pivot = new Vector2(0.5f, 0.5f);
            rectCardIzq.sizeDelta = new Vector2(490, 520);
            rectCardIzq.anchoredPosition = new Vector2(-255, 15);
            var imgCardIzq = cardIzq.AddComponent<Image>();
            imgCardIzq.sprite = spriteRedondo;
            imgCardIzq.type = Image.Type.Sliced;
            imgCardIzq.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            // Cabecera Ticket
            var cabeceraTicket = CrearTextoUI("CabeceraTicket", cardIzq.transform, "🧾 TICKET DE COMPRA", 18, TextAnchor.MiddleLeft,
                new Vector2(20, 235), new Vector2(450, 30), new Color(0.58f, 0.68f, 0.82f), true);

            var separador1 = CrearSeparadorUI("Sep1", cardIzq.transform, new Vector2(0, 215), 450);

            // Lista de productos
            textoDetalleProductos = CrearTextoUI("Items", cardIzq.transform, "Esperando cliente...", 16, TextAnchor.UpperLeft,
                new Vector2(20, 85), new Vector2(450, 230), new Color(0.90f, 0.92f, 0.95f));

            var separador2 = CrearSeparadorUI("Sep2", cardIzq.transform, new Vector2(0, -60), 450);

            // Resumen de cobro en tarjeta destacada
            var cardTotales = CrearObjetoUI("Card_Totales", cardIzq.transform);
            var rectTotales = cardTotales.GetComponent<RectTransform>();
            rectTotales.anchorMin = rectTotales.anchorMax = rectTotales.pivot = new Vector2(0.5f, 0.5f);
            rectTotales.sizeDelta = new Vector2(450, 160);
            rectTotales.anchoredPosition = new Vector2(0, -160);
            var imgTotales = cardTotales.AddComponent<Image>();
            imgTotales.sprite = spriteRedondo;
            imgTotales.type = Image.Type.Sliced;
            imgTotales.color = new Color(0.09f, 0.12f, 0.17f, 0.90f);

            textoTotalPagar = CrearTextoUI("TotalPagar", cardTotales.transform, "Total a Pagar:  $0.00", 24, TextAnchor.MiddleLeft,
                new Vector2(18, 48), new Vector2(410, 35), new Color(1f, 0.85f, 0.35f), true);

            textoBilleteRecibido = CrearTextoUI("Billete", cardTotales.transform, "Cliente Entrega:  💵 $0.00", 20, TextAnchor.MiddleLeft,
                new Vector2(18, 5), new Vector2(410, 30), new Color(0.40f, 0.90f, 0.55f), true);

            textoVueltoRequerido = CrearTextoUI("VueltoReq", cardTotales.transform, "Vuelto Requerido:  🪙 $0.00", 20, TextAnchor.MiddleLeft,
                new Vector2(18, -40), new Vector2(410, 30), new Color(0.40f, 0.80f, 1f), true);

            // ==========================================
            // PANEL DERECHO: CAJÓN REGISTRADOR & CAMBIO (490 x 510)
            // ==========================================
            var cardDer = CrearObjetoUI("Card_CajonCambio", panelCaja.transform);
            var rectCardDer = cardDer.GetComponent<RectTransform>();
            rectCardDer.anchorMin = rectCardDer.anchorMax = rectCardDer.pivot = new Vector2(0.5f, 0.5f);
            rectCardDer.sizeDelta = new Vector2(490, 520);
            rectCardDer.anchoredPosition = new Vector2(255, 15);
            var imgCardDer = cardDer.AddComponent<Image>();
            imgCardDer.sprite = spriteRedondo;
            imgCardDer.type = Image.Type.Sliced;
            imgCardDer.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            // Cabecera Cajón
            CrearTextoUI("CabeceraCajon", cardDer.transform, "💵 CAJÓN REGISTRADOR Y CAMBIO", 18, TextAnchor.MiddleLeft,
                new Vector2(20, 235), new Vector2(450, 30), new Color(0.58f, 0.68f, 0.82f), true);

            var separador3 = CrearSeparadorUI("Sep3", cardDer.transform, new Vector2(0, 215), 450);

            // Pantalla digital Vuelto Ingresado
            var pantallaDigital = CrearObjetoUI("Display_Digital", cardDer.transform);
            var rectDigital = pantallaDigital.GetComponent<RectTransform>();
            rectDigital.anchorMin = rectDigital.anchorMax = rectDigital.pivot = new Vector2(0.5f, 0.5f);
            rectDigital.sizeDelta = new Vector2(450, 75);
            rectDigital.anchoredPosition = new Vector2(0, 160);
            var imgDigital = pantallaDigital.AddComponent<Image>();
            imgDigital.sprite = spriteRedondo;
            imgDigital.type = Image.Type.Sliced;
            imgDigital.color = new Color(0.03f, 0.05f, 0.07f, 1f);

            textoVueltoIngresado = CrearTextoUI("VueltoIngresado", pantallaDigital.transform, "Vuelto Ingresado:  $0.00", 24, TextAnchor.MiddleCenter,
                new Vector2(0, 12), new Vector2(430, 34), new Color(1f, 0.95f, 0.55f), true);

            textoEstadoVuelto = CrearTextoUI("EstadoVuelto", pantallaDigital.transform, "(Selecciona el dinero a entregar)", 14, TextAnchor.MiddleCenter,
                new Vector2(0, -18), new Vector2(430, 24), new Color(0.70f, 0.78f, 0.88f));

            // SECCIÓN MONEDAS
            CrearTextoUI("EtiquetaMonedas", cardDer.transform, "🪙 MONEDAS", 14, TextAnchor.MiddleLeft,
                new Vector2(25, 105), new Vector2(200, 20), new Color(0.85f, 0.75f, 0.50f), true);

            float[] monedas = { 0.10f, 0.25f, 0.50f, 1.00f };
            string[] etiquetasMonedas = { "🪙 $0.10", "🪙 $0.25", "🪙 $0.50", "🪙 $1.00" };

            for (int i = 0; i < monedas.Length; i++)
            {
                float monto = monedas[i];
                float x = -165f + (i * 110f);
                CrearBotonUI("Btn_Coin_" + i, cardDer.transform, etiquetasMonedas[i], new Vector2(x, 65), new Vector2(100, 48),
                    new Color(0.24f, 0.19f, 0.13f), new Color(1f, 0.88f, 0.55f), () => AgregarDineroVuelto(monto));
            }

            // SECCIÓN BILLETES
            CrearTextoUI("EtiquetaBilletes", cardDer.transform, "💵 BILLETES", 14, TextAnchor.MiddleLeft,
                new Vector2(25, 25), new Vector2(200, 20), new Color(0.50f, 0.85f, 0.65f), true);

            float[] billetes = { 5.00f, 10.00f, 20.00f, 50.00f };
            string[] etiquetasBilletes = { "💵 $5.00", "💵 $10.00", "💵 $20.00", "💵 $50.00" };

            for (int i = 0; i < billetes.Length; i++)
            {
                float monto = billetes[i];
                float x = -165f + (i * 110f);
                CrearBotonUI("Btn_Bill_" + i, cardDer.transform, etiquetasBilletes[i], new Vector2(x, -15), new Vector2(100, 48),
                    new Color(0.12f, 0.25f, 0.18f), new Color(0.55f, 1f, 0.75f), () => AgregarDineroVuelto(monto));
            }

            // BOTONES DE ACCIÓN: LIMPIAR Y CONFIRMAR
            CrearBotonUI("Btn_Limpiar", cardDer.transform, "🗑️ BORRAR VUELTO", new Vector2(-115, -110), new Vector2(210, 56),
                new Color(0.48f, 0.15f, 0.15f), Color.white, LimpiarVuelto, 16);

            CrearBotonUI("Btn_Confirmar", cardDer.transform, "✅ CONFIRMAR COBRO", new Vector2(115, -110), new Vector2(210, 56),
                new Color(0.14f, 0.56f, 0.26f), Color.white, ConfirmarCobro, 16);

            // ==========================================
            // BARRA INFERIOR: FEEDBACK & CERRAR
            // ==========================================
            textoMensajeFeedback = CrearTextoUI("Feedback", panelCaja.transform, "", 18, TextAnchor.MiddleCenter,
                new Vector2(0, -265), new Vector2(850, 28), Color.white, true);

            // Botón cerrar centrado y elegante (sin botón de prueba)
            CrearBotonUI("Btn_Cerrar", panelCaja.transform, "❌ CERRAR TERMINAL [ESC / E]", new Vector2(0, -310), new Vector2(360, 44),
                new Color(0.22f, 0.25f, 0.30f), new Color(0.90f, 0.92f, 0.95f), CerrarCaja, 16);
        }

        private GameObject CrearObjetoUI(string nombre, Transform padre)
        {
            var obj = new GameObject(nombre, typeof(RectTransform));
            obj.transform.SetParent(padre, false);
            return obj;
        }

        private GameObject CrearSeparadorUI(string nombre, Transform padre, Vector2 pos, float ancho)
        {
            var sep = CrearObjetoUI(nombre, padre);
            var rect = sep.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ancho, 2);
            rect.anchoredPosition = pos;
            var img = sep.AddComponent<Image>();
            img.color = new Color(0.20f, 0.26f, 0.35f, 0.5f);
            return sep;
        }

        private Text CrearTextoUI(string nombre, Transform padre, string textoInicial, int tamano, TextAnchor alineacion,
            Vector2 posicionLocal, Vector2 dimensiones, Color color, bool negrita = false)
        {
            var obj = CrearObjetoUI(nombre, padre);
            var rect = obj.GetComponent<RectTransform>();

            // Determinar si es anclaje relativo o centrado
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = posicionLocal;
            rect.sizeDelta = dimensiones;

            var txt = obj.AddComponent<Text>();
            txt.font = fuenteUI;
            txt.text = textoInicial;
            txt.fontSize = tamano;
            txt.alignment = alineacion;
            txt.color = color;
            txt.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
            txt.raycastTarget = false;
            return txt;
        }

        private Button CrearBotonUI(string nombre, Transform padre, string textoBoton, Vector2 posicionLocal,
            Vector2 dimensiones, Color colorFondo, Color colorTexto, UnityEngine.Events.UnityAction alHacerClick, int fontSize = 15)
        {
            var obj = CrearObjetoUI(nombre, padre);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = posicionLocal;
            rect.sizeDelta = dimensiones;

            var img = obj.AddComponent<Image>();
            img.sprite = HUDTienda.ObtenerSpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = colorFondo;
            img.raycastTarget = true;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            var colores = btn.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = colores;

            if (alHacerClick != null) btn.onClick.AddListener(alHacerClick);

            var textoObj = CrearTextoUI("Texto", obj.transform, textoBoton, fontSize, TextAnchor.MiddleCenter, Vector2.zero, dimensiones, colorTexto, true);
            textoObj.raycastTarget = false;

            return btn;
        }

        #endregion
    }
}
