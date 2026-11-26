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

        [Header("Estado y Paneles")]
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

        // Vistas internas de la terminal PC
        private GameObject vistaMenuPrincipal;
        private GameObject vistaCobranza;
        private GameObject vistaPaquetes;
        private Button botonIdentificarPaquete;
        private Button botonCobrarCliente;
        private Text textoBotonCobrarCliente;
        private Image fondoBotonCobrarCliente;
        private Text textoEstadoTerminalMenu;
        private Text textoFeedbackMenu;
        private Text textoEstadoPaquetes;
        private Text textoFeedbackPaquetes;
        private Button botonConfirmarCobro;

        private struct TarjetaPaqueteUI
        {
            public int numero;
            public Button boton;
            public Image fondo;
            public Image insignia;
            public Text textoInsignia;
            public Text textoTitulo;
            public Text textoEstado;
        }

        private readonly TarjetaPaqueteUI[] tarjetasPaquetes = new TarjetaPaqueteUI[4];
        private Coroutine rutinaCerrarTrasRastreo;

        private float vueltoActualIngresado = 0f;
        private Font fuenteUI;
        private Camara scriptCamara;
        private PlayerMovement scriptMovimiento;
        private bool camaraEstabaActivaAntes = true;
        private bool movimientoEstabaActivoAntes = true;
        private float tiempoParaPoderCerrar = 0f;

        public bool EstaAbierta => estaAbierta;
        public Button BotonConfirmarCobro => botonConfirmarCobro;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scriptCamara = FindAnyObjectByType<Camara>();
            scriptMovimiento = FindAnyObjectByType<PlayerMovement>();

            AsegurarEventSystem();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            ConstruirInterfazProcedural();

            if (rootUI != null) rootUI.SetActive(false);
            if (panelCaja != null) panelCaja.SetActive(false);
        }

        private void AsegurarEventSystem()
        {
            var existente = EventSystem.current;
            if (existente == null) existente = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existente == null)
            {
                GameObject esObj = new GameObject("EventSystem_Tienda", typeof(EventSystem), typeof(InputSystemUIInputModule));
                if (Application.isPlaying) DontDestroyOnLoad(esObj);
                return;
            }

            if (existente.GetComponent<InputSystemUIInputModule>() == null)
            {
                var viejo = existente.GetComponent<StandaloneInputModule>();
                if (viejo != null) Destroy(viejo);
                existente.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void Start()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado += ActualizarDineroCajaUI;
                TiendaManager.Instance.OnNuevoPedidoGenerado += AlCambiarPedido;
                TiendaManager.Instance.OnVentaFinalizadaConExito += AlFinalizarOVariarPedido;
                TiendaManager.Instance.OnVentaCancelada += AlCancelarPedido;
                ActualizarDineroCajaUI(TiendaManager.Instance.DineroEnCaja);
            }

            if (Mundo1.SistemaPaquetesMundo1.Instance != null)
            {
                Mundo1.SistemaPaquetesMundo1.Instance.OnEstadoPaquetesCambiado += ActualizarTarjetasPaquetes;
            }
        }

        private void OnDestroy()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado -= ActualizarDineroCajaUI;
                TiendaManager.Instance.OnNuevoPedidoGenerado -= AlCambiarPedido;
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlFinalizarOVariarPedido;
                TiendaManager.Instance.OnVentaCancelada -= AlCancelarPedido;
            }

            if (Mundo1.SistemaPaquetesMundo1.Instance != null)
            {
                Mundo1.SistemaPaquetesMundo1.Instance.OnEstadoPaquetesCambiado -= ActualizarTarjetasPaquetes;
            }
        }

        private void AlCambiarPedido(PedidoCliente _)
        {
            ActualizarEstadoMenuPrincipal();
            if (vistaCobranza != null && vistaCobranza.activeSelf)
            {
                ActualizarPantallaPedido();
            }
        }

        private void AlFinalizarOVariarPedido()
        {
            ActualizarEstadoMenuPrincipal();
        }

        private void AlCancelarPedido(string _)
        {
            ActualizarEstadoMenuPrincipal();
            if (estaAbierta && vistaCobranza != null && vistaCobranza.activeSelf)
            {
                MostrarVistaMenuPrincipal();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (estaAbierta)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CerrarCaja();
                }
                else if (Keyboard.current.eKey.wasPressedThisFrame && Time.unscaledTime >= tiempoParaPoderCerrar)
                {
                    CerrarCaja();
                }
            }
            else if (scriptCamara != null && scriptCamara.enabled && scriptMovimiento != null && scriptMovimiento.enabled)
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
            AbrirTerminalEnCobroDirecto(false);
        }

        public void AbrirCajaDirectoACobro()
        {
            AbrirTerminalEnCobroDirecto(true);
        }

        private void AbrirTerminalEnCobroDirecto(bool directoACobro)
        {
            if (Mundo1.CinematicaGerente.Instance != null && Mundo1.CinematicaGerente.Instance.EstaEnCinematica)
            {
                return;
            }

            if (directoACobro && Mundo1.DirectorTurno.Instance != null && Mundo1.DirectorTurno.Instance.DebeDispararClimaxAlCobrar())
            {
                Mundo1.DirectorTurno.Instance.DispararSecuenciaFinalUltimoCliente();
                return;
            }

            estaAbierta = true;
            tiempoParaPoderCerrar = Time.unscaledTime + 0.25f;

            if (rootUI == null || panelCaja == null || vistaMenuPrincipal == null || vistaCobranza == null || vistaPaquetes == null)
            {
                ConstruirInterfazProcedural();
            }

            AsegurarEventSystem();

            if (rootUI != null) rootUI.SetActive(true);
            if (panelCaja != null) panelCaja.SetActive(true);

            // Pausar rotación y movimiento del jugador guardando el estado previo
            if (scriptCamara == null) scriptCamara = FindAnyObjectByType<Camara>();
            if (scriptMovimiento == null) scriptMovimiento = FindAnyObjectByType<PlayerMovement>();
            camaraEstabaActivaAntes = scriptCamara != null && scriptCamara.enabled;
            movimientoEstabaActivoAntes = scriptMovimiento != null && scriptMovimiento.enabled;
            if (scriptCamara != null) scriptCamara.enabled = false;
            if (scriptMovimiento != null) scriptMovimiento.enabled = false;

            // Liberar ratón para interactuar con los botones
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LimpiarVuelto();
            if (directoACobro)
            {
                AbrirModuloCobranza();
            }
            else
            {
                MostrarVistaMenuPrincipal();
            }
        }

        public void CerrarCaja()
        {
            if (rutinaCerrarTrasRastreo != null)
            {
                StopCoroutine(rutinaCerrarTrasRastreo);
                rutinaCerrarTrasRastreo = null;
            }

            estaAbierta = false;

            if (rootUI != null) rootUI.SetActive(false);
            if (panelCaja != null) panelCaja.SetActive(false);

            bool enCinematica = Mundo1.CinematicaGerente.Instance != null && Mundo1.CinematicaGerente.Instance.EstaEnCinematica;
            if (!enCinematica)
            {
                // Reactivar jugador solo si estaba activo antes de abrir la terminal
                if (scriptCamara != null && camaraEstabaActivaAntes) scriptCamara.enabled = true;
                if (scriptMovimiento != null && movimientoEstabaActivoAntes) scriptMovimiento.enabled = true;

                // Bloquear cursor nuevamente
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void MostrarVistaMenuPrincipal()
        {
            if (rutinaCerrarTrasRastreo != null)
            {
                StopCoroutine(rutinaCerrarTrasRastreo);
                rutinaCerrarTrasRastreo = null;
            }

            if (vistaMenuPrincipal != null) vistaMenuPrincipal.SetActive(true);
            if (vistaCobranza != null) vistaCobranza.SetActive(false);
            if (vistaPaquetes != null) vistaPaquetes.SetActive(false);
            if (textoFeedbackMenu != null) textoFeedbackMenu.text = "";
            ActualizarEstadoMenuPrincipal();
        }

        public void AbrirModuloCobranza()
        {
            if (Mundo1.DirectorTurno.Instance != null && Mundo1.DirectorTurno.Instance.DebeDispararClimaxAlCobrar())
            {
                CerrarCaja();
                Mundo1.DirectorTurno.Instance.DispararSecuenciaFinalUltimoCliente();
                return;
            }

            if (TiendaManager.Instance == null || !TiendaManager.Instance.HayPedidoPendiente)
            {
                if (textoFeedbackMenu != null)
                {
                    textoFeedbackMenu.text = "⏳ Esperando a que llegue un cliente a la ventanilla.";
                    textoFeedbackMenu.color = new Color(1f, 0.78f, 0.30f);
                }
                ActualizarEstadoMenuPrincipal();
                return;
            }

            if (vistaMenuPrincipal != null) vistaMenuPrincipal.SetActive(false);
            if (vistaPaquetes != null) vistaPaquetes.SetActive(false);
            if (vistaCobranza != null) vistaCobranza.SetActive(true);
            LimpiarVuelto();
            ActualizarPantallaPedido();
        }

        public void AbrirModuloPaquetes()
        {
            if (vistaMenuPrincipal != null) vistaMenuPrincipal.SetActive(false);
            if (vistaCobranza != null) vistaCobranza.SetActive(false);
            if (vistaPaquetes != null) vistaPaquetes.SetActive(true);
            if (textoFeedbackPaquetes != null) textoFeedbackPaquetes.text = "";
            ActualizarTarjetasPaquetes();
        }

        private void AlPulsarTarjetaPaquete(int numeroPaquete)
        {
            var sistema = Mundo1.SistemaPaquetesMundo1.Instance;
            if (sistema == null)
            {
                if (textoFeedbackPaquetes != null)
                {
                    textoFeedbackPaquetes.text = "⚠️ Sistema de paquetes no disponible en esta escena.";
                    textoFeedbackPaquetes.color = new Color(1f, 0.55f, 0.35f);
                }
                return;
            }

            bool exito = sistema.SeleccionarYRastrearPaquete(numeroPaquete, out string mensaje);
            if (textoFeedbackPaquetes != null)
            {
                textoFeedbackPaquetes.text = mensaje;
                textoFeedbackPaquetes.color = exito
                    ? new Color(0.40f, 0.96f, 0.72f)
                    : new Color(1f, 0.80f, 0.30f);
            }

            ActualizarTarjetasPaquetes();

            if (exito)
            {
                if (rutinaCerrarTrasRastreo != null) StopCoroutine(rutinaCerrarTrasRastreo);
                rutinaCerrarTrasRastreo = StartCoroutine(RutinaCerrarTrasCobro(0.75f));
            }
        }

        private void ActualizarTarjetasPaquetes()
        {
            var sistema = Mundo1.SistemaPaquetesMundo1.Instance;
            bool enReposicion = sistema != null && sistema.EstaEnFaseReposicion();

            if (textoEstadoPaquetes != null)
            {
                if (!enReposicion)
                {
                    textoEstadoPaquetes.text = "⏳ Aún debes atender a los clientes iniciales antes de buscar paquetes.";
                    textoEstadoPaquetes.color = new Color(1f, 0.82f, 0.35f);
                }
                else
                {
                    int actual = Mathf.Clamp(sistema.IndiceActual + 1, 1, 4);
                    bool todosListos = sistema.IndiceActual >= sistema.TotalPaquetes && sistema.TotalPaquetes > 0;
                    textoEstadoPaquetes.text = todosListos
                        ? "✅ Todos los paquetes ya fueron colocados en los anaqueles."
                        : $"📦 Selecciona el Paquete #{actual} para proyectar su ubicación en tu vista (orden 1 → 4).";
                    textoEstadoPaquetes.color = todosListos
                        ? new Color(0.42f, 1f, 0.58f)
                        : new Color(0.55f, 0.88f, 1f);
                }
            }

            var lista = sistema != null ? sistema.ObtenerPaquetes() : null;

            for (int i = 0; i < tarjetasPaquetes.Length; i++)
            {
                var t = tarjetasPaquetes[i];
                if (t.boton == null) continue;

                int num = i + 1;
                string ubicacion = (lista != null && i < lista.Count && !string.IsNullOrEmpty(lista[i].ubicacion))
                    ? lista[i].ubicacion
                    : (num == 1 ? "Almacén interior de la tienda" : $"Almacén exterior · Zona #{num - 1}");

                if (t.textoTitulo != null)
                {
                    string tagOrden = num == 1 ? "Más cercano" : (num == 4 ? "Más lejano" : $"Paso {num} de 4");
                    t.textoTitulo.text = $"PAQUETE #{num}  ·  {ubicacion}  ({tagOrden})";
                }

                var estado = sistema != null
                    ? sistema.ObtenerEstado(num)
                    : Mundo1.SistemaPaquetesMundo1.EstadoPaquete.Bloqueado;
                bool rastreado = sistema != null && sistema.EstaRastreado(num);

                if (estado == Mundo1.SistemaPaquetesMundo1.EstadoPaquete.Entregado)
                {
                    t.boton.interactable = false;
                    if (t.fondo != null) t.fondo.color = new Color(0.08f, 0.24f, 0.14f, 0.95f);
                    if (t.insignia != null) t.insignia.color = new Color(0.16f, 0.62f, 0.32f, 1f);
                    if (t.textoInsignia != null)
                    {
                        t.textoInsignia.text = "✔";
                        t.textoInsignia.color = Color.white;
                    }
                    if (t.textoTitulo != null) t.textoTitulo.color = new Color(0.75f, 0.96f, 0.80f);
                    if (t.textoEstado != null)
                    {
                        t.textoEstado.text = "✔ ENTREGADO EN ANAQUEL";
                        t.textoEstado.color = new Color(0.42f, 1f, 0.58f);
                    }
                }
                else if (estado == Mundo1.SistemaPaquetesMundo1.EstadoPaquete.Disponible)
                {
                    t.boton.interactable = true;
                    if (t.fondo != null)
                    {
                        t.fondo.color = rastreado
                            ? new Color(0.10f, 0.32f, 0.44f, 1f)
                            : new Color(0.12f, 0.22f, 0.36f, 1f);
                    }
                    if (t.insignia != null)
                    {
                        t.insignia.color = rastreado
                            ? new Color(0.20f, 0.72f, 0.88f, 1f)
                            : new Color(0.22f, 0.50f, 0.82f, 1f);
                    }
                    if (t.textoInsignia != null)
                    {
                        t.textoInsignia.text = num.ToString();
                        t.textoInsignia.color = Color.white;
                    }
                    if (t.textoTitulo != null) t.textoTitulo.color = Color.white;
                    if (t.textoEstado != null)
                    {
                        t.textoEstado.text = rastreado ? "🎯 RASTREANDO EN VISTA" : "📍 LOCALIZAR EN VISTA";
                        t.textoEstado.color = rastreado
                            ? new Color(0.45f, 1f, 0.85f)
                            : new Color(0.55f, 0.88f, 1f);
                    }
                }
                else
                {
                    t.boton.interactable = false;
                    if (t.fondo != null) t.fondo.color = new Color(0.12f, 0.14f, 0.18f, 0.88f);
                    if (t.insignia != null) t.insignia.color = new Color(0.22f, 0.25f, 0.32f, 1f);
                    if (t.textoInsignia != null)
                    {
                        t.textoInsignia.text = "🔒";
                        t.textoInsignia.color = new Color(0.75f, 0.78f, 0.85f);
                    }
                    if (t.textoTitulo != null) t.textoTitulo.color = new Color(0.55f, 0.60f, 0.68f);
                    if (t.textoEstado != null)
                    {
                        int requerido = sistema != null ? Mathf.Clamp(sistema.IndiceActual + 1, 1, 4) : 1;
                        t.textoEstado.text = enReposicion
                            ? $"🔒 Entrega primero Paquete #{requerido}"
                            : "🔒 Disponible tras atender clientes";
                        t.textoEstado.color = new Color(0.58f, 0.62f, 0.70f);
                    }
                }
            }
        }

        private void ActualizarEstadoMenuPrincipal()
        {
            bool hayCliente = TiendaManager.Instance != null && TiendaManager.Instance.HayPedidoPendiente;

            if (botonCobrarCliente != null)
            {
                botonCobrarCliente.interactable = hayCliente;
            }

            if (fondoBotonCobrarCliente != null)
            {
                fondoBotonCobrarCliente.color = hayCliente
                    ? new Color(0.12f, 0.56f, 0.28f, 1f)
                    : new Color(0.18f, 0.21f, 0.26f, 0.85f);
            }

            if (textoBotonCobrarCliente != null)
            {
                textoBotonCobrarCliente.text = hayCliente
                    ? "💳  COBRAR AL CLIENTE  (¡EN VENTANILLA!)"
                    : "⏳  COBRAR AL CLIENTE  (Esperando en ventanilla)";
                textoBotonCobrarCliente.color = hayCliente
                    ? Color.white
                    : new Color(0.65f, 0.70f, 0.78f);
            }

            if (textoEstadoTerminalMenu != null)
            {
                if (hayCliente)
                {
                    float total = TiendaManager.Instance.PedidoActual.TotalPagar;
                    textoEstadoTerminalMenu.text = $"🟢 Cliente esperando atención en mostrador — Total del pedido: ${total:F2}";
                    textoEstadoTerminalMenu.color = new Color(0.38f, 1f, 0.58f);
                }
                else
                {
                    textoEstadoTerminalMenu.text = "🟡 Sin clientes en ventanilla — Espera a que llegue un cliente para habilitar el cobro.";
                    textoEstadoTerminalMenu.color = new Color(1f, 0.84f, 0.35f);
                }
            }
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
                ActualizarEstadoMenuPrincipal();
                StartCoroutine(RutinaCerrarTrasCobro(1.1f));
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

        public void GenerarNuevoPedidoPrueba()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.GenerarPedidoAleatorioPrueba();
                LimpiarVuelto();
                ActualizarPantallaPedido();
                ActualizarEstadoMenuPrincipal();
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
                        string lower = nombreProd.ToLower();
                        if (lower.Contains("refresco") || lower.Contains("cola") || lower.Contains("soda") || lower.Contains("jugo"))
                            icono = "🥤";
                        else if (lower.Contains("galleta") || lower.Contains("snack") || lower.Contains("papas") || lower.Contains("chips"))
                            icono = "🍿";
                        else if (lower.Contains("chocolate") || lower.Contains("dulce") || lower.Contains("caramelo"))
                            icono = "🍫";
                        else if (lower.Contains("pan") || lower.Contains("sandwich"))
                            icono = "🥪";

                        float precioUnit = linea.producto != null ? linea.producto.Precio : 0f;
                        detalle += $"{icono} {nombreProd}  x{linea.cantidad}\n";
                        detalle += $"    <color=#94A3B8>(${precioUnit:F2} c/u)</color>   <color=#38BDF8><b>${linea.Subtotal:F2}</b></color>\n\n";
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
                    textoDetalleProductos.text = "⏳ <color=#94A3B8>Esperando cliente en el mostrador...</color>";
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

        #region Construcción Procedural de Terminal PC

        private void ConstruirInterfazProcedural()
        {
            // Siempre crear un Canvas dedicado propio para evitar conflictos de sortingOrder o blocksRaycasts
            GameObject canvasPrevio = GameObject.Find("Canvas_CajaRegistradora");
            GameObject canvasObj = canvasPrevio != null
                ? canvasPrevio
                : new GameObject("Canvas_CajaRegistradora", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            var cg = canvasObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvasObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            if (rootUI != null)
            {
                Destroy(rootUI);
            }

            Sprite spriteRedondo = HUDTienda.ObtenerSpriteRedondeado();

            // 1. Contenedor Raíz
            rootUI = CrearObjetoUI("Root_CajaRegistradora", canvas.transform);
            var rectRoot = rootUI.GetComponent<RectTransform>();
            rectRoot.anchorMin = Vector2.zero;
            rectRoot.anchorMax = Vector2.one;
            rectRoot.offsetMin = rectRoot.offsetMax = Vector2.zero;

            var imgCortina = rootUI.AddComponent<Image>();
            imgCortina.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);
            imgCortina.raycastTarget = true; // Bloquea clics detrás del modal

            // 2. Ventana Terminal Central Amplia (1260 x 800)
            panelCaja = CrearObjetoUI("Ventana_Terminal_POS", rootUI.transform);
            var rectVentana = panelCaja.GetComponent<RectTransform>();
            rectVentana.anchorMin = rectVentana.anchorMax = rectVentana.pivot = new Vector2(0.5f, 0.5f);
            rectVentana.sizeDelta = new Vector2(1260, 800);
            rectVentana.anchoredPosition = Vector2.zero;

            var imgFondo = panelCaja.AddComponent<Image>();
            imgFondo.sprite = spriteRedondo;
            imgFondo.type = Image.Type.Sliced;
            imgFondo.color = new Color(0.09f, 0.11f, 0.15f, 0.98f);
            imgFondo.raycastTarget = true;

            var bordeVentana = CrearObjetoUI("Borde_Ventana", panelCaja.transform);
            var rectBordeV = bordeVentana.GetComponent<RectTransform>();
            rectBordeV.anchorMin = Vector2.zero;
            rectBordeV.anchorMax = Vector2.one;
            rectBordeV.sizeDelta = Vector2.zero;
            var imgBordeV = bordeVentana.AddComponent<Image>();
            imgBordeV.sprite = spriteRedondo;
            imgBordeV.type = Image.Type.Sliced;
            imgBordeV.color = new Color(0.28f, 0.36f, 0.48f, 0.42f);
            imgBordeV.raycastTarget = false;

            // BARRA DE TÍTULO
            var barraTitulo = CrearObjetoUI("Barra_Titulo", panelCaja.transform);
            var rectBarra = barraTitulo.GetComponent<RectTransform>();
            rectBarra.anchorMin = new Vector2(0, 1);
            rectBarra.anchorMax = new Vector2(1, 1);
            rectBarra.pivot = new Vector2(0.5f, 1f);
            rectBarra.sizeDelta = new Vector2(-20, 68);
            rectBarra.anchoredPosition = new Vector2(0, -10);
            var imgBarra = barraTitulo.AddComponent<Image>();
            imgBarra.sprite = spriteRedondo;
            imgBarra.type = Image.Type.Sliced;
            imgBarra.color = new Color(0.06f, 0.07f, 0.10f, 1f);
            imgBarra.raycastTarget = false;

            CrearTextoUI("Titulo", barraTitulo.transform, "🏪 6-TWELVE STORE · TERMINAL PC #01", 24, TextAnchor.MiddleLeft,
                new Vector2(-260, 0), new Vector2(640, 48), new Color(0.95f, 0.88f, 0.65f), true);

            textoDineroEnCaja = CrearTextoUI("DineroEnCaja", barraTitulo.transform, "💰 Saldo en Caja:  $100.00", 22, TextAnchor.MiddleRight,
                new Vector2(310, 0), new Vector2(540, 48), new Color(0.35f, 0.95f, 0.55f), true);

            // ==========================================================
            // VISTA 1: MENÚ PRINCIPAL DE LA TERMINAL (2 OPCIONES)
            // ==========================================================
            vistaMenuPrincipal = CrearObjetoUI("Vista_MenuPrincipal", panelCaja.transform);
            var rectMenu = vistaMenuPrincipal.GetComponent<RectTransform>();
            rectMenu.anchorMin = Vector2.zero;
            rectMenu.anchorMax = Vector2.one;
            rectMenu.offsetMin = new Vector2(20, 80);
            rectMenu.offsetMax = new Vector2(-20, -90);

            CrearTextoUI("SubtituloMenu", vistaMenuPrincipal.transform, "SISTEMA DE GESTIÓN DE TURNO", 30, TextAnchor.MiddleCenter,
                new Vector2(0, 210), new Vector2(960, 48), new Color(0.88f, 0.93f, 1f), true);

            textoEstadoTerminalMenu = CrearTextoUI("EstadoTerminalMenu", vistaMenuPrincipal.transform,
                "🟡 Sin clientes en ventanilla — Espera a que llegue un cliente para habilitar el cobro.", 22, TextAnchor.MiddleCenter,
                new Vector2(0, 140), new Vector2(1060, 56), new Color(1f, 0.84f, 0.35f), true);

            // Opción 1: Identificar Paquete (4 tarjetas ordenadas por cercanía)
            botonIdentificarPaquete = CrearBotonUI("Btn_IdentificarPaquete", vistaMenuPrincipal.transform,
                "📦  IDENTIFICAR PAQUETE  (Ubicar en almacén)", new Vector2(0, 25), new Vector2(760, 100),
                new Color(0.14f, 0.28f, 0.44f), Color.white, AbrirModuloPaquetes, 26);

            // Opción 2: Cobrar al Cliente (solo se activa cuando llega a ventanilla)
            botonCobrarCliente = CrearBotonUI("Btn_CobrarCliente", vistaMenuPrincipal.transform,
                "⏳  COBRAR AL CLIENTE  (Esperando en ventanilla)", new Vector2(0, -105), new Vector2(760, 105),
                new Color(0.18f, 0.21f, 0.26f), new Color(0.65f, 0.70f, 0.78f), AbrirModuloCobranza, 26);
            fondoBotonCobrarCliente = botonCobrarCliente.GetComponent<Image>();
            textoBotonCobrarCliente = botonCobrarCliente.GetComponentInChildren<Text>();

            textoFeedbackMenu = CrearTextoUI("FeedbackMenu", vistaMenuPrincipal.transform, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0, -210), new Vector2(980, 44), new Color(0.60f, 0.82f, 1f), true);

            // ==========================================================
            // VISTA 2: MÓDULO DE COBRANZA (TICKET + CAJÓN)
            // ==========================================================
            vistaCobranza = CrearObjetoUI("Vista_Cobranza", panelCaja.transform);
            var rectCobranza = vistaCobranza.GetComponent<RectTransform>();
            rectCobranza.anchorMin = Vector2.zero;
            rectCobranza.anchorMax = Vector2.one;
            rectCobranza.offsetMin = Vector2.zero;
            rectCobranza.offsetMax = Vector2.zero;

            // PANEL IZQUIERDO: TICKET DE VENTA (580 x 585)
            var cardIzq = CrearObjetoUI("Card_Ticket", vistaCobranza.transform);
            var rectCardIzq = cardIzq.GetComponent<RectTransform>();
            rectCardIzq.anchorMin = rectCardIzq.anchorMax = rectCardIzq.pivot = new Vector2(0.5f, 0.5f);
            rectCardIzq.sizeDelta = new Vector2(580, 585);
            rectCardIzq.anchoredPosition = new Vector2(-305, 10);
            var imgCardIzq = cardIzq.AddComponent<Image>();
            imgCardIzq.sprite = spriteRedondo;
            imgCardIzq.type = Image.Type.Sliced;
            imgCardIzq.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            CrearTextoUI("CabeceraTicket", cardIzq.transform, "🧾 TICKET DE COMPRA", 22, TextAnchor.MiddleLeft,
                new Vector2(0, 260), new Vector2(530, 36), new Color(0.65f, 0.76f, 0.90f), true);

            CrearSeparadorUI("Sep1", cardIzq.transform, new Vector2(0, 236), 530);

            textoDetalleProductos = CrearTextoUI("Items", cardIzq.transform, "Esperando cliente...", 21, TextAnchor.UpperLeft,
                new Vector2(0, 88), new Vector2(530, 265), new Color(0.92f, 0.94f, 0.97f));

            CrearSeparadorUI("Sep2", cardIzq.transform, new Vector2(0, -65), 530);

            var cardTotales = CrearObjetoUI("Card_Totales", cardIzq.transform);
            var rectTotales = cardTotales.GetComponent<RectTransform>();
            rectTotales.anchorMin = rectTotales.anchorMax = rectTotales.pivot = new Vector2(0.5f, 0.5f);
            rectTotales.sizeDelta = new Vector2(530, 185);
            rectTotales.anchoredPosition = new Vector2(0, -175);
            var imgTotales = cardTotales.AddComponent<Image>();
            imgTotales.sprite = spriteRedondo;
            imgTotales.type = Image.Type.Sliced;
            imgTotales.color = new Color(0.09f, 0.12f, 0.17f, 0.92f);

            textoTotalPagar = CrearTextoUI("TotalPagar", cardTotales.transform, "Total a Pagar:  $0.00", 28, TextAnchor.MiddleLeft,
                new Vector2(0, 55), new Vector2(490, 42), new Color(1f, 0.85f, 0.35f), true);

            textoBilleteRecibido = CrearTextoUI("Billete", cardTotales.transform, "Cliente Entrega:  💵 $0.00", 24, TextAnchor.MiddleLeft,
                new Vector2(0, 4), new Vector2(490, 38), new Color(0.40f, 0.90f, 0.55f), true);

            textoVueltoRequerido = CrearTextoUI("VueltoReq", cardTotales.transform, "Vuelto Requerido:  🪙 $0.00", 24, TextAnchor.MiddleLeft,
                new Vector2(0, -48), new Vector2(490, 38), new Color(0.40f, 0.80f, 1f), true);

            // PANEL DERECHO: CAJÓN REGISTRADOR & CAMBIO (580 x 585)
            var cardDer = CrearObjetoUI("Card_CajonCambio", vistaCobranza.transform);
            var rectCardDer = cardDer.GetComponent<RectTransform>();
            rectCardDer.anchorMin = rectCardDer.anchorMax = rectCardDer.pivot = new Vector2(0.5f, 0.5f);
            rectCardDer.sizeDelta = new Vector2(580, 585);
            rectCardDer.anchoredPosition = new Vector2(305, 10);
            var imgCardDer = cardDer.AddComponent<Image>();
            imgCardDer.sprite = spriteRedondo;
            imgCardDer.type = Image.Type.Sliced;
            imgCardDer.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            CrearTextoUI("CabeceraCajon", cardDer.transform, "💵 CAJÓN REGISTRADOR Y CAMBIO", 22, TextAnchor.MiddleLeft,
                new Vector2(0, 260), new Vector2(530, 36), new Color(0.65f, 0.76f, 0.90f), true);

            CrearSeparadorUI("Sep3", cardDer.transform, new Vector2(0, 236), 530);

            var pantallaDigital = CrearObjetoUI("Display_Digital", cardDer.transform);
            var rectDigital = pantallaDigital.GetComponent<RectTransform>();
            rectDigital.anchorMin = rectDigital.anchorMax = rectDigital.pivot = new Vector2(0.5f, 0.5f);
            rectDigital.sizeDelta = new Vector2(530, 92);
            rectDigital.anchoredPosition = new Vector2(0, 172);
            var imgDigital = pantallaDigital.AddComponent<Image>();
            imgDigital.sprite = spriteRedondo;
            imgDigital.type = Image.Type.Sliced;
            imgDigital.color = new Color(0.03f, 0.05f, 0.07f, 1f);

            textoVueltoIngresado = CrearTextoUI("VueltoIngresado", pantallaDigital.transform, "Vuelto Ingresado:  $0.00", 28, TextAnchor.MiddleCenter,
                new Vector2(0, 16), new Vector2(500, 42), new Color(1f, 0.95f, 0.55f), true);

            textoEstadoVuelto = CrearTextoUI("EstadoVuelto", pantallaDigital.transform, "(Selecciona el dinero a entregar)", 18, TextAnchor.MiddleCenter,
                new Vector2(0, -22), new Vector2(500, 30), new Color(0.75f, 0.82f, 0.92f));

            // SECCIÓN MONEDAS
            CrearTextoUI("EtiquetaMonedas", cardDer.transform, "🪙 MONEDAS", 18, TextAnchor.MiddleLeft,
                new Vector2(-160, 108), new Vector2(210, 26), new Color(0.90f, 0.80f, 0.52f), true);

            float[] monedas = { 0.10f, 0.25f, 0.50f, 1.00f };
            string[] etiquetasMonedas = { "$0.10", "$0.25", "$0.50", "$1.00" };

            for (int i = 0; i < monedas.Length; i++)
            {
                float monto = monedas[i];
                float x = -195f + (i * 130f);
                CrearBotonUI("Btn_Coin_" + i, cardDer.transform, etiquetasMonedas[i], new Vector2(x, 62), new Vector2(118, 56),
                    new Color(0.24f, 0.19f, 0.13f), new Color(1f, 0.88f, 0.55f), () => AgregarDineroVuelto(monto), 21);
            }

            // SECCIÓN BILLETES
            CrearTextoUI("EtiquetaBilletes", cardDer.transform, "💵 BILLETES", 18, TextAnchor.MiddleLeft,
                new Vector2(-160, 10), new Vector2(210, 26), new Color(0.55f, 0.90f, 0.70f), true);

            float[] billetes = { 5.00f, 10.00f, 20.00f, 50.00f };
            string[] etiquetasBilletes = { "$5.00", "$10.00", "$20.00", "$50.00" };

            for (int i = 0; i < billetes.Length; i++)
            {
                float monto = billetes[i];
                float x = -195f + (i * 130f);
                CrearBotonUI("Btn_Bill_" + i, cardDer.transform, etiquetasBilletes[i], new Vector2(x, -36), new Vector2(118, 56),
                    new Color(0.12f, 0.25f, 0.18f), new Color(0.55f, 1f, 0.75f), () => AgregarDineroVuelto(monto), 21);
            }

            // BOTONES DE ACCIÓN: LIMPIAR Y CONFIRMAR
            CrearBotonUI("Btn_Limpiar", cardDer.transform, "🗑️ BORRAR VUELTO", new Vector2(-135, -140), new Vector2(250, 66),
                new Color(0.48f, 0.15f, 0.15f), Color.white, LimpiarVuelto, 20);

            botonConfirmarCobro = CrearBotonUI("Btn_Confirmar", cardDer.transform, "✅ CONFIRMAR COBRO", new Vector2(135, -140), new Vector2(250, 66),
                new Color(0.14f, 0.56f, 0.26f), Color.white, ConfirmarCobro, 20);

            textoMensajeFeedback = CrearTextoUI("Feedback", vistaCobranza.transform, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0, -305), new Vector2(1100, 38), Color.white, true);

            CrearBotonUI("Btn_VolverMenu", vistaCobranza.transform, "⬅ VOLVER AL MENÚ", new Vector2(-240, -355), new Vector2(310, 52),
                new Color(0.18f, 0.24f, 0.34f), new Color(0.90f, 0.94f, 1f), MostrarVistaMenuPrincipal, 19);

            // BOTÓN CERRAR GLOBAL (visible en ambas vistas)
            CrearBotonUI("Btn_Cerrar", panelCaja.transform, "❌ CERRAR TERMINAL [ESC / E]", new Vector2(220, -355), new Vector2(380, 52),
                new Color(0.24f, 0.26f, 0.32f), new Color(0.95f, 0.96f, 0.98f), CerrarCaja, 19);

            vistaCobranza.SetActive(false);

            // ==========================================================
            // VISTA 3: IDENTIFICAR PAQUETE (4 TARJETAS SECUENCIALES)
            // ==========================================================
            ConstruirVistaPaquetes(spriteRedondo);
        }

        private void ConstruirVistaPaquetes(Sprite spriteRedondo)
        {
            vistaPaquetes = CrearObjetoUI("Vista_IdentificarPaquete", panelCaja.transform);
            var rectPaq = vistaPaquetes.GetComponent<RectTransform>();
            rectPaq.anchorMin = Vector2.zero;
            rectPaq.anchorMax = Vector2.one;
            rectPaq.offsetMin = Vector2.zero;
            rectPaq.offsetMax = Vector2.zero;

            CrearTextoUI("TituloPaquetes", vistaPaquetes.transform, "📦 IDENTIFICAR PAQUETE · RUTA SECUENCIAL (1 → 4)", 26, TextAnchor.MiddleCenter,
                new Vector2(0, 275), new Vector2(1100, 42), new Color(0.75f, 0.90f, 1f), true);

            textoEstadoPaquetes = CrearTextoUI("EstadoPaquetes", vistaPaquetes.transform,
                "Selecciona el paquete disponible para proyectar su ubicación en tu vista.", 20, TextAnchor.MiddleCenter,
                new Vector2(0, 232), new Vector2(1100, 34), new Color(0.55f, 0.88f, 1f), true);

            for (int i = 0; i < 4; i++)
            {
                int num = i + 1;
                float posY = 148f - (i * 112f);

                var cardObj = CrearObjetoUI("Tarjeta_Paquete_" + num, vistaPaquetes.transform);
                var rectCard = cardObj.GetComponent<RectTransform>();
                rectCard.anchorMin = rectCard.anchorMax = rectCard.pivot = new Vector2(0.5f, 0.5f);
                rectCard.anchoredPosition = new Vector2(0f, posY);
                rectCard.sizeDelta = new Vector2(1100f, 96f);

                var imgFondo = cardObj.AddComponent<Image>();
                imgFondo.sprite = spriteRedondo;
                imgFondo.type = Image.Type.Sliced;
                imgFondo.color = new Color(0.12f, 0.14f, 0.18f, 0.90f);
                imgFondo.raycastTarget = true;

                var btn = cardObj.AddComponent<Button>();
                btn.targetGraphic = imgFondo;
                var colores = btn.colors;
                colores.normalColor = Color.white;
                colores.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
                colores.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
                colores.disabledColor = new Color(0.90f, 0.90f, 0.90f, 0.95f);
                btn.colors = colores;
                btn.onClick.AddListener(() => AlPulsarTarjetaPaquete(num));

                var insigniaObj = CrearObjetoUI("Insignia", cardObj.transform);
                var rectIns = insigniaObj.GetComponent<RectTransform>();
                rectIns.anchorMin = rectIns.anchorMax = rectIns.pivot = new Vector2(0.5f, 0.5f);
                rectIns.anchoredPosition = new Vector2(-485f, 0f);
                rectIns.sizeDelta = new Vector2(64f, 64f);
                var imgIns = insigniaObj.AddComponent<Image>();
                imgIns.sprite = spriteRedondo;
                imgIns.type = Image.Type.Sliced;
                imgIns.color = new Color(0.22f, 0.25f, 0.32f, 1f);
                imgIns.raycastTarget = false;

                var txtIns = CrearTextoUI("TxtInsignia", insigniaObj.transform, num.ToString(), 26, TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(60f, 60f), Color.white, true);

                var txtTitulo = CrearTextoUI("TituloTarjeta", cardObj.transform, $"PAQUETE #{num}", 22, TextAnchor.MiddleLeft,
                    new Vector2(-110f, 0f), new Vector2(640f, 64f), Color.white, true);

                var txtEstado = CrearTextoUI("EstadoTarjeta", cardObj.transform, "🔒 Bloqueado", 20, TextAnchor.MiddleRight,
                    new Vector2(340f, 0f), new Vector2(360f, 64f), new Color(0.65f, 0.70f, 0.78f), true);

                tarjetasPaquetes[i] = new TarjetaPaqueteUI
                {
                    numero = num,
                    boton = btn,
                    fondo = imgFondo,
                    insignia = imgIns,
                    textoInsignia = txtIns,
                    textoTitulo = txtTitulo,
                    textoEstado = txtEstado
                };
            }

            textoFeedbackPaquetes = CrearTextoUI("FeedbackPaquetes", vistaPaquetes.transform, "", 20, TextAnchor.MiddleCenter,
                new Vector2(0, -285), new Vector2(1100, 36), Color.white, true);

            CrearBotonUI("Btn_VolverMenuPaquetes", vistaPaquetes.transform, "⬅ VOLVER AL MENÚ", new Vector2(-240, -355), new Vector2(310, 52),
                new Color(0.18f, 0.24f, 0.34f), new Color(0.90f, 0.94f, 1f), MostrarVistaMenuPrincipal, 19);

            vistaPaquetes.SetActive(false);
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
            img.color = new Color(0.22f, 0.28f, 0.38f, 0.55f);
            img.raycastTarget = false;
            return sep;
        }

        private Text CrearTextoUI(string nombre, Transform padre, string textoInicial, int tamano, TextAnchor alineacion,
            Vector2 posicionLocal, Vector2 dimensiones, Color color, bool negrita = false)
        {
            var obj = CrearObjetoUI(nombre, padre);
            var rect = obj.GetComponent<RectTransform>();
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
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private Button CrearBotonUI(string nombre, Transform padre, string textoBoton, Vector2 posicionLocal,
            Vector2 dimensiones, Color colorFondo, Color colorTexto, UnityEngine.Events.UnityAction alHacerClick, int fontSize = 19)
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
            colores.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
            colores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colores.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.7f);
            btn.colors = colores;

            if (alHacerClick != null) btn.onClick.AddListener(alHacerClick);

            var textoObj = CrearTextoUI("Texto", obj.transform, textoBoton, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(dimensiones.x - 16f, dimensiones.y - 8f), colorTexto, true);
            textoObj.raycastTarget = false;

            return btn;
        }

        #endregion
    }
}
