using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mundo1.UI;
using Tienda.SistemaVentas;

namespace Mundo1
{
    /// <summary>
    /// Director principal de Mundo 1: orquesta la secuencia fija de 6 a 7 minutos.
    /// Coordina: Cinemática Gerente -> Clientes en Caja -> Reposición de las 4 Cajas ->
    /// Regreso a Caja -> Últimos 2 Clientes -> Clímax Final -> Transición al siguiente nivel.
    /// </summary>
    [DisallowMultipleComponent]
    public class DirectorTurno : MonoBehaviour
    {
        public static DirectorTurno Instance { get; private set; }

        public enum FaseTurno
        {
            CinematicaInicial,
            AtendiendoClientes,
            ReponiendoAnaqueles,
            ClienteTormenta,
            ApagonYClimax,
            TransicionFinal,
            VolverACaja,
            ClientesFinales
        }

        [Header("Estado del Turno")]
        [SerializeField] private FaseTurno faseActual = FaseTurno.CinematicaInicial;
        public FaseTurno FaseActual => faseActual;

        [Header("Fase 1: Clientes en Caja")]
        [SerializeField] private int clientesMeta = 2;
        [SerializeField] private int clientesAtendidos = 0;
        [SerializeField] private GameObject cliente1Obj;
        [SerializeField] private GameObject cliente2Obj;

        [Header("Fase 2: Reposición de Anaqueles (4 Cajas)")]
        [SerializeField] private int cajasRepuestas = 0;
        [SerializeField] private int cajasMeta = 4;
        [Tooltip("Las 4 cajas colocadas en posiciones fijas del mapa")]
        [SerializeField] private GameObject[] cajasEnMapa;
        [Tooltip("Monstruo o silueta en el almacén que se activa al ir por la caja 3/4")]
        [SerializeField] private GameObject siluetaMonstruoAlmacen;

        [Header("Fase 2: Oscurecimiento al Vaciar Anaqueles")]
        [SerializeField] private float duracionOscurecimiento = 0.45f;
        [SerializeField] private float duracionPausaOscura = 1.15f;
        [SerializeField] private float duracionAclarado = 0.75f;

        [Header("Fase 3: Último Cliente y Tormenta")]
        [SerializeField] private GameObject clienteTormentaRaiz;
        [SerializeField] private GameObject skinClienteNormal;
        [SerializeField] private GameObject skinClienteMonstruo;

        [Header("Fase 4: Clímax y Apagón")]
        [Tooltip("Luces de la tienda que se apagan durante el corte de luz")]
        [SerializeField] private GameObject[] lucesTienda;
        [SerializeField] private GameObject objetoJumpscareCamara;
        [SerializeField] private AudioSource audioSourceEfectos;
        [SerializeField] private AudioClip sonidoApagon;
        [SerializeField] private AudioClip sonidoRespiracionCerca;
        [SerializeField] private AudioClip sonidoJumpscare;

        [Header("Tramo Final: Regreso a Caja y Últimos 2 Clientes")]
        [SerializeField] private GameObject clienteFinal1Obj;
        [SerializeField] private GameObject clienteFinal2Obj;
        [SerializeField] private GameObject modeloMonstruoReferencia;
        [SerializeField] private Transform puntoCajaJugador;
        [SerializeField] private Transform puntoClienteMostrador;
        [SerializeField] private Vector3 centroZonaCaja = new Vector3(33.1f, 2.44f, 47.5f);
        [SerializeField] private float mitadZonaCajaX = 1.65f;
        [SerializeField] private float mitadZonaCajaZ = 2.25f;
        [SerializeField] private AudioClip clipLatidoContinuo;

        private CanvasGroup canvasGroupOscurecimiento;
        private AudioSource audioSourceLatido;
        private GameObject clonMonstruoVisual;
        private Animator animadorClonMonstruo;
        private Light luzSustoClimax;
        private int clientesFinalesAtendidos = 0;
        private const int ClientesFinalesMeta = 2;
        private bool climaxEnCurso = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Limpiar y desactivar sistemas antiguos (Spawner, NPCs aleatorios y HUD de saldo viejo)
            LimpiarSistemasAntiguos();
            AutoBuscarReferenciasFinales();

            if (GetComponent<SistemaPaquetesMundo1>() == null)
            {
                gameObject.AddComponent<SistemaPaquetesMundo1>();
            }

            if (audioSourceEfectos == null)
            {
                audioSourceEfectos = gameObject.AddComponent<AudioSource>();
            }
            audioSourceEfectos.playOnAwake = false;
            audioSourceEfectos.spatialBlend = 0f;
            audioSourceEfectos.volume = 1f;

#if UNITY_EDITOR
            if (sonidoApagon == null)
                sonidoApagon = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoApagon.mp3");
            if (sonidoRespiracionCerca == null)
                sonidoRespiracionCerca = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoRespiracionCerca.mp3");
            if (sonidoJumpscare == null)
                sonidoJumpscare = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoJumpscare.mp3");
            if (clipLatidoContinuo == null)
                clipLatidoContinuo = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/clipLatido.mp3");
#endif

            audioSourceLatido = gameObject.AddComponent<AudioSource>();
            audioSourceLatido.playOnAwake = false;
            audioSourceLatido.loop = true;
            audioSourceLatido.spatialBlend = 0f;
            audioSourceLatido.volume = 0f;
            if (clipLatidoContinuo != null) audioSourceLatido.clip = clipLatidoContinuo;

            // Iniciar objetos en sus estados iniciales
            if (skinClienteMonstruo != null) skinClienteMonstruo.SetActive(false);
            if (objetoJumpscareCamara != null) objetoJumpscareCamara.SetActive(false);
            if (siluetaMonstruoAlmacen != null) siluetaMonstruoAlmacen.SetActive(false);

            // Desactivar cajas hasta que llegue su fase
            if (cajasEnMapa != null)
            {
                foreach (var c in cajasEnMapa)
                {
                    if (c != null) c.SetActive(false);
                }
            }

            if (clienteTormentaRaiz != null) clienteTormentaRaiz.SetActive(false);
            if (cliente1Obj != null) cliente1Obj.SetActive(false);
            if (cliente2Obj != null) cliente2Obj.SetActive(false);
            if (clienteFinal1Obj != null) clienteFinal1Obj.SetActive(false);
            if (clienteFinal2Obj != null) clienteFinal2Obj.SetActive(false);
        }

        private void Start()
        {
            // Suscripción limpia al sistema de cobro existente sin modificar sus scripts
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito += RegistrarVentaCompletada;
            }

            // Conectar con la cinemática del gerente
            if (CinematicaGerente.Instance != null)
            {
                CinematicaGerente.Instance.OnCinematicaFinalizada += IniciarFaseClientes;
                CinematicaGerente.Instance.IniciarCinematica();
            }
            else
            {
                // Si no hay cinemática, arrancar directo con los clientes
                IniciarFaseClientes();
            }
        }

        private void OnDestroy()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= RegistrarVentaCompletada;
            }
        }

        #region Fase 1: Clientes

        private void IniciarFaseClientes()
        {
            faseActual = FaseTurno.AtendiendoClientes;
            ActualizarHUDClientes();

            // Activar primer cliente
            if (cliente1Obj != null) cliente1Obj.SetActive(true);
        }

        private void RegistrarVentaCompletada()
        {
            if (faseActual == FaseTurno.AtendiendoClientes)
            {
                clientesAtendidos++;
                ActualizarHUDClientes();

                if (clientesAtendidos == 1)
                {
                    // Aparece el segundo cliente tras 2 segundos
                    StartCoroutine(RutinaAparecerCliente2());
                }
                else if (clientesAtendidos >= clientesMeta)
                {
                    // Pasar a la fase de reponer anaqueles
                    StartCoroutine(RutinaTransicionAFaseCajas());
                }
            }
            else if (faseActual == FaseTurno.ClientesFinales)
            {
                clientesFinalesAtendidos++;
                ActualizarHUDClientesFinales();

                if (clientesFinalesAtendidos == 1)
                {
                    StartCoroutine(RutinaActivarUltimoClienteJosh());
                }
            }
        }

        private void ActualizarHUDClientes()
        {
            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea(
                    "clientes",
                    $"Atiende a los clientes en la caja ({clientesAtendidos}/{clientesMeta})",
                    clientesAtendidos >= clientesMeta
                );
            }
        }

        private IEnumerator RutinaAparecerCliente2()
        {
            yield return new WaitForSeconds(2.5f);
            if (cliente2Obj != null) cliente2Obj.SetActive(true);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Llega el siguiente cliente al mostrador...", 3.5f);
            }
        }

        #endregion

        #region Fase 2: Reposición de Cajas (Anaqueles)

        private IEnumerator RutinaTransicionAFaseCajas()
        {
            yield return new WaitForSeconds(1.5f);
            faseActual = FaseTurno.ReponiendoAnaqueles;

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("clientes");
            }

            AsegurarOverlayOscurecimiento();

            // 1. Parpadeo eléctrico + sonido de bajón de luz
            if (sonidoApagon != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoApagon, 0.7f);
            }

            SetEstadoLucesTienda(false);
            yield return new WaitForSeconds(0.08f);
            SetEstadoLucesTienda(true);
            yield return new WaitForSeconds(0.07f);
            SetEstadoLucesTienda(false);
            yield return new WaitForSeconds(0.08f);
            SetEstadoLucesTienda(true);
            yield return new WaitForSeconds(0.05f);
            SetEstadoLucesTienda(false);

            // 2. Oscurecimiento total para ocultar el vaciado repentino de los anaqueles
            yield return StartCoroutine(FundirOscurecimiento(0f, 1f, duracionOscurecimiento));

            // 3. En plena oscuridad: desbloquear puertas, vaciar anaqueles y activar cajas
            PuertaSeguroMundo1.DesbloquearTodas();

            var sensores = FindObjectsByType<SensorAnaquelMundo1>(FindObjectsInactive.Include);
            for (int i = 0; i < sensores.Length; i++)
            {
                sensores[i].ForzarVacio();
            }

            if (sensores.Length == 0)
            {
                var objetos = FindObjectsByType<ObjetoAleatorio>(FindObjectsInactive.Include);
                for (int i = 0; i < objetos.Length; i++)
                {
                    objetos[i].VaciarObjeto();
                }
            }

            if (cajasEnMapa != null)
            {
                foreach (var caja in cajasEnMapa)
                {
                    if (caja != null) caja.SetActive(true);
                }
            }

            if (SistemaPaquetesMundo1.Instance != null)
            {
                SistemaPaquetesMundo1.Instance.InicializarListaDesdeEscena();
            }

            yield return new WaitForSeconds(duracionPausaOscura);

            // 4. Vuelve la luz lentamente y continúa el juego con los anaqueles vacíos
            SetEstadoLucesTienda(true);
            yield return new WaitForSeconds(0.06f);
            SetEstadoLucesTienda(false);
            yield return new WaitForSeconds(0.05f);
            SetEstadoLucesTienda(true);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Se fue la luz por un instante... ¡Los anaqueles quedaron vacíos! Consulta en la PC la ubicación del primer paquete.", 5.5f);
                ActualizarHUDCajas();
            }

            yield return StartCoroutine(FundirOscurecimiento(1f, 0f, duracionAclarado));
        }

        private void SetEstadoLucesTienda(bool encendidas)
        {
            if (lucesTienda == null) return;
            for (int i = 0; i < lucesTienda.Length; i++)
            {
                if (lucesTienda[i] != null) lucesTienda[i].SetActive(encendidas);
            }
        }

        private void AsegurarOverlayOscurecimiento()
        {
            if (canvasGroupOscurecimiento != null) return;

            var canvasObj = new GameObject("Canvas_OscurecimientoTurno", typeof(Canvas), typeof(CanvasScaler));
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            var panel = new GameObject("Panel_Oscurecimiento", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasObj.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = panel.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            canvasGroupOscurecimiento = panel.GetComponent<CanvasGroup>();
            canvasGroupOscurecimiento.alpha = 0f;
            canvasGroupOscurecimiento.interactable = false;
            canvasGroupOscurecimiento.blocksRaycasts = false;
        }

        private IEnumerator FundirOscurecimiento(float desde, float hasta, float duracion)
        {
            if (canvasGroupOscurecimiento == null) yield break;

            if (duracion <= 0.01f)
            {
                canvasGroupOscurecimiento.alpha = hasta;
                yield break;
            }

            float t = 0f;
            canvasGroupOscurecimiento.alpha = desde;
            while (t < duracion)
            {
                t += Time.unscaledDeltaTime;
                canvasGroupOscurecimiento.alpha = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t / duracion));
                yield return null;
            }
            canvasGroupOscurecimiento.alpha = hasta;
        }

        /// <summary>
        /// Llamado cada vez que el jugador llena un anaquel con una caja.
        /// </summary>
        public void NotificarCajaRepuesta()
        {
            if (faseActual != FaseTurno.ReponiendoAnaqueles) return;

            cajasRepuestas++;
            ActualizarHUDCajas();

            // Eventos progresivos de terror según las cajas entregadas
            if (cajasRepuestas == 1)
            {
                if (HUDTareasMundo1.Instance != null)
                    HUDTareasMundo1.Instance.MostrarSubtitulo("Se escuchó un crujido extraño en el almacén...", 3f);
            }
            else if (cajasRepuestas == 2)
            {
                // Se asoma la silueta del monstruo en la oscuridad del almacén
                if (siluetaMonstruoAlmacen != null) siluetaMonstruoAlmacen.SetActive(true);
            }
            else if (cajasRepuestas == 3)
            {
                if (HUDTareasMundo1.Instance != null)
                    HUDTareasMundo1.Instance.MostrarSubtitulo("¡Un golpe metálico resonó al fondo del almacén!", 3.5f);
            }
            else if (cajasRepuestas >= cajasMeta)
            {
                // Fin de la fase de cajas -> Tarea intermedia: volver detrás de la caja antes de los últimos clientes
                StartCoroutine(RutinaEsperarRegresoACaja());
            }
        }

        private void ActualizarHUDCajas()
        {
            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea(
                    "cajas",
                    $"Consulta en la PC las ubicaciones y repón anaqueles ({cajasRepuestas}/{cajasMeta})",
                    cajasRepuestas >= cajasMeta
                );
            }
        }

        #endregion

        #region Fase 3: Cliente Tormenta y Relámpago con Swap

        private IEnumerator RutinaIniciarClienteTormenta()
        {
            yield return new WaitForSeconds(2.0f);
            faseActual = FaseTurno.ClienteTormenta;

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("cajas");
                HUDTareasMundo1.Instance.EstablecerTarea("ultimo_cliente", "Atiende al último cliente antes del cierre", false);
                HUDTareasMundo1.Instance.MostrarSubtitulo("Suena el timbre de la puerta bajo la lluvia torrencial...", 4f);
            }

            // Intensificar tormenta
            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.IntensificarTormenta(0.95f);
            }

            // Activar al cliente que avanza hacia el mostrador
            if (clienteTormentaRaiz != null)
            {
                clienteTormentaRaiz.SetActive(true);
            }
        }

        /// <summary>
        /// Llamado cuando el jugador interactúa con el cliente tormenta en la caja.
        /// </summary>
        public void DispararSustoClienteTormenta()
        {
            if (faseActual != FaseTurno.ClienteTormenta) return;

            StartCoroutine(RutinaSustoClienteTormenta());
        }

        private IEnumerator RutinaSustoClienteTormenta()
        {
            // 1. Trueno brutal con swap de skin
            bool terminado = false;
            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.DispararRelampago(
                    skinClienteNormal,
                    skinClienteMonstruo,
                    0.25f,
                    () => { terminado = true; }
                );
                while (!terminado) yield return null;
            }

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("ultimo_cliente");
                HUDTareasMundo1.Instance.MostrarSubtitulo("Tú: '¡¿QUÉ ERA ESA COSA?! ¡LA CARA... NO ERA HUMANA!'", 4f);
            }

            yield return new WaitForSeconds(3f);

            // 3. Desencadenar el clímax final (apagón y jumpscare)
            StartCoroutine(RutinaApagonYClimax());
        }

        #endregion

        #region Fase 4: Apagón, Jumpscare y Transición

        private IEnumerator RutinaApagonYClimax()
        {
            faseActual = FaseTurno.ApagonYClimax;

            // 1. Apagón general de luces de la tienda
            if (sonidoApagon != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoApagon);
            }

            if (lucesTienda != null)
            {
                foreach (var luz in lucesTienda)
                {
                    if (luz != null) luz.SetActive(false);
                }
            }

            // El cliente del mostrador ya no está
            if (clienteTormentaRaiz != null) clienteTormentaRaiz.SetActive(false);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea("escape", "¡SAL DE LA TIENDA INMEDIATAMENTE!", false);
                HUDTareasMundo1.Instance.MostrarSubtitulo("Se cortó la electricidad... una respiración pesada suena a tu espalda.", 4.5f);
            }

            // Sonido de acoso/respiración detrás
            if (sonidoRespiracionCerca != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoRespiracionCerca, 0.85f);
            }

            // Pausa de 4 segundos de máxima tensión a oscuras
            yield return new WaitForSeconds(4.0f);

            // 2. ¡Jumpscare frontal en la cámara!
            if (objetoJumpscareCamara != null)
            {
                objetoJumpscareCamara.SetActive(true);
            }

            if (sonidoJumpscare != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoJumpscare, 1f);
            }

            yield return new WaitForSeconds(0.85f);

            // 3. Transición inmediata al siguiente mapa
            if (TransicionNivel.Instance != null)
            {
                TransicionNivel.Instance.IniciarTransicionSiguienteNivel(false);
            }
        }

        #endregion

        #region Tramo Final: Regreso a Caja, Sophie y Clímax Final

        private void AutoBuscarReferenciasFinales()
        {
            var grupoNPC = GameObject.Find("--NPC--");
            if (grupoNPC != null)
            {
                if (clienteFinal1Obj == null)
                {
                    var t = grupoNPC.transform.Find("Sophie");
                    if (t != null) clienteFinal1Obj = t.gameObject;
                }
                if (clienteFinal2Obj == null)
                {
                    var t = grupoNPC.transform.Find("Josh");
                    if (t != null) clienteFinal2Obj = t.gameObject;
                }
            }

            if (modeloMonstruoReferencia == null)
            {
                var monstruoVisual = GameObject.Find("FinalMonster 1");
                if (monstruoVisual != null)
                {
                    modeloMonstruoReferencia = monstruoVisual;
                }
                else
                {
                    var mh = FindAnyObjectByType<MonstruoWarehouse>(FindObjectsInactive.Include);
                    if (mh != null) modeloMonstruoReferencia = mh.gameObject;
                }
            }

            if (puntoCajaJugador == null)
            {
                var p = GameObject.Find("Punto_JugadorFrenteGerente");
                if (p != null) puntoCajaJugador = p.transform;
            }

            if (puntoClienteMostrador == null)
            {
                var p = GameObject.Find("Punto_CajaRegistradora");
                if (p != null) puntoClienteMostrador = p.transform;
            }
        }

        private void PrepararClonMonstruoVisual()
        {
            if (clonMonstruoVisual != null || modeloMonstruoReferencia == null) return;

            clonMonstruoVisual = Instantiate(modeloMonstruoReferencia);
            clonMonstruoVisual.name = "Monstruo_ClimaxVisual_Temp";

            var agente = clonMonstruoVisual.GetComponent<NavMeshAgent>();
            if (agente != null) Destroy(agente);

            var ia = clonMonstruoVisual.GetComponent<MonstruoWarehouse>();
            if (ia != null) Destroy(ia);

            foreach (var col in clonMonstruoVisual.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            foreach (var src in clonMonstruoVisual.GetComponentsInChildren<AudioSource>(true))
                Destroy(src);

            animadorClonMonstruo = clonMonstruoVisual.GetComponentInChildren<Animator>(true);
            if (animadorClonMonstruo != null)
            {
                animadorClonMonstruo.applyRootMotion = false;
            }

            clonMonstruoVisual.SetActive(false);

            if (luzSustoClimax == null)
            {
                var luzObj = new GameObject("Luz_SustoClimaxFinal");
                luzObj.transform.SetParent(transform, false);
                luzSustoClimax = luzObj.AddComponent<Light>();
                luzSustoClimax.type = LightType.Point;
                luzSustoClimax.range = 6.5f;
                luzSustoClimax.intensity = 2.8f;
                luzSustoClimax.color = new Color(0.95f, 0.88f, 0.85f);
                luzSustoClimax.enabled = false;
            }
        }

        private void EstablecerLatidoContinuo(bool activo, float volumen)
        {
            if (audioSourceLatido == null || clipLatidoContinuo == null) return;

            audioSourceLatido.clip = clipLatidoContinuo;
            audioSourceLatido.volume = Mathf.Clamp01(volumen);

            if (activo && volumen > 0.005f)
            {
                if (!audioSourceLatido.isPlaying) audioSourceLatido.Play();
            }
            else if (audioSourceLatido.isPlaying)
            {
                audioSourceLatido.Stop();
            }
        }

        private void ActualizarHUDClientesFinales()
        {
            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea(
                    "clientes_finales",
                    $"Atiende a los últimos clientes antes de cerrar ({clientesFinalesAtendidos}/{ClientesFinalesMeta})",
                    clientesFinalesAtendidos >= ClientesFinalesMeta
                );
            }
        }

        private IEnumerator RutinaEsperarRegresoACaja()
        {
            yield return new WaitForSeconds(1.2f);
            faseActual = FaseTurno.VolverACaja;

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("cajas");
                HUDTareasMundo1.Instance.EstablecerTarea("volver_caja", "Vuelve al mostrador de la caja", false);
                HUDTareasMundo1.Instance.MostrarSubtitulo("Tú: 'Ya repuse todo... debo regresar atrás de la caja registradora.'", 4.0f);
            }

            PlayerMovement playerMov = FindAnyObjectByType<PlayerMovement>();
            Transform jugador = playerMov != null ? playerMov.transform : null;

            if (jugador != null)
            {
                while (true)
                {
                    Vector3 p = jugador.position;
                    bool enZona = Mathf.Abs(p.x - centroZonaCaja.x) <= Mathf.Max(0.5f, mitadZonaCajaX - 0.1f) &&
                                  Mathf.Abs(p.z - centroZonaCaja.z) <= Mathf.Max(0.5f, mitadZonaCajaZ - 0.1f);
                    bool cercaPunto = puntoCajaJugador != null &&
                                      Vector2.Distance(new Vector2(p.x, p.z), new Vector2(puntoCajaJugador.position.x, puntoCajaJugador.position.z)) <= 1.85f;

                    if (enZona || cercaPunto) break;
                    yield return null;
                }
            }

            // Cerrar salidas y restringir movimiento dentro de la zona del mostrador
            PuertaSeguroMundo1.BloquearTodas();
            if (playerMov != null)
            {
                playerMov.EstablecerLimiteZonaCaja(true, centroZonaCaja, mitadZonaCajaX, mitadZonaCajaZ);
            }

            faseActual = FaseTurno.ClientesFinales;
            clientesFinalesAtendidos = 0;
            climaxEnCurso = false;

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("volver_caja");
                ActualizarHUDClientesFinales();
                HUDTareasMundo1.Instance.AjustarVolumenMusicaGradual(0.62f, 22f);
                HUDTareasMundo1.Instance.MostrarSubtitulo("Suena la puerta bajo la lluvia... vienen los últimos clientes.", 3.5f);
            }

            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.IntensificarTormenta(0.70f, 22f);
            }

            PrepararClonMonstruoVisual();

            yield return new WaitForSeconds(1.8f);

            if (clienteFinal1Obj != null)
            {
                var cg = clienteFinal1Obj.GetComponent<ClienteGuionadoMundo1>();
                if (cg != null)
                {
                    cg.activarEventoEntreAnaqueles = true;
                    cg.esClienteClimaxFinal = false;
                    cg.RutinaEventoEntreAnaqueles = RutinaSustoSophieEntreAnaqueles;
                    cg.AlAcercarseACaja = AlAcercarseSophieACaja;
                    cg.RutinaPrePedidoEnCaja = RutinaDialogoSophieEnCaja;
                }
                clienteFinal1Obj.SetActive(true);
            }
        }

        private IEnumerator RutinaSustoSophieEntreAnaqueles(ClienteGuionadoMundo1 sophie)
        {
            if (sophie != null) sophie.PausarMovimiento(true);

            Camara cam = FindAnyObjectByType<Camara>();
            PlayerMovement playerMov = FindAnyObjectByType<PlayerMovement>();

            // 1. Zoom involuntario hacia la posición fija entre anaqueles ANTES del cambio
            if (cam != null && sophie != null)
            {
                yield return StartCoroutine(cam.RutinaZoomHaciaObjetivo(sophie.transform, 28f, 0.50f));
                yield return new WaitForSeconds(0.25f);
            }

            // 2. Trueno + relámpago + cambio momentáneo de skin al monstruo
            if (clonMonstruoVisual != null && sophie != null)
            {
                clonMonstruoVisual.transform.position = sophie.transform.position;
                clonMonstruoVisual.transform.rotation = sophie.transform.rotation;
            }

            bool relampagoTerminado = false;
            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.DispararRelampago(
                    sophie != null ? sophie.gameObject : null,
                    clonMonstruoVisual,
                    0.28f,
                    () => relampagoTerminado = true
                );
                while (!relampagoTerminado) yield return null;
            }

            // 3. Empieza el dolor, baja la vista, latido fuerte, estática y esfuerzo al mover/levantar cámara
            if (cam != null)
            {
                cam.RestaurarFOV(50f, 0.35f);
                cam.AplicarReflejoDolorHaciaAbajo(24f, 0.28f);
                cam.EstablecerModificadorSensibilidad(0.14f);
            }

            if (playerMov != null)
            {
                playerMov.EstablecerPenalizacionDolor(true, 0.28f);
            }

            EstablecerLatidoContinuo(true, 0.90f);

            if (RetroCameraOverlay.Instance != null)
            {
                RetroCameraOverlay.Instance.DispararGlitch(0.65f);
                RetroCameraOverlay.Instance.EstablecerInterferenciaPersecucion(0.85f);
            }

            if (sophie != null) sophie.PausarMovimiento(false);
        }

        private void AlAcercarseSophieACaja(ClienteGuionadoMundo1 _)
        {
            Camara cam = FindAnyObjectByType<Camara>();
            if (cam != null) cam.EstablecerModificadorSensibilidad(1f);

            PlayerMovement playerMov = FindAnyObjectByType<PlayerMovement>();
            if (playerMov != null) playerMov.EstablecerPenalizacionDolor(false, 1f);

            // No vuelve al silencio/cero: queda latido bajo y estática leve continua
            EstablecerLatidoContinuo(true, 0.18f);

            if (RetroCameraOverlay.Instance != null)
            {
                RetroCameraOverlay.Instance.EstablecerInterferenciaPersecucion(0f);
                RetroCameraOverlay.Instance.EstablecerInterferenciaBase(0.20f);
            }
        }

        private IEnumerator RutinaDialogoSophieEnCaja(ClienteGuionadoMundo1 _)
        {
            AlAcercarseSophieACaja(null);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Sophie: 'Oye... ¿estás bien? Te ves muy pálido de repente.'", 3.2f);
            }
            yield return new WaitForSeconds(3.3f);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Tú: 'Sí... estoy bien, solo me dio un mareo. ¿Qué vas a llevar?'", 3.4f);
            }
            yield return new WaitForSeconds(3.0f);
        }

        private IEnumerator RutinaActivarUltimoClienteJosh()
        {
            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.AjustarVolumenMusicaGradual(0.84f, 18f);
            }

            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.IntensificarTormenta(0.88f, 18f);
            }

            if (RetroCameraOverlay.Instance != null)
            {
                RetroCameraOverlay.Instance.EstablecerInterferenciaBase(0.28f);
            }

            yield return new WaitForSeconds(2.5f);

            if (clienteFinal2Obj != null)
            {
                var cg = clienteFinal2Obj.GetComponent<ClienteGuionadoMundo1>();
                if (cg != null)
                {
                    cg.activarEventoEntreAnaqueles = false;
                    cg.esClienteClimaxFinal = true;
                    cg.RutinaEventoEntreAnaqueles = null;
                    cg.AlAcercarseACaja = null;
                    cg.RutinaPrePedidoEnCaja = null;
                }
                clienteFinal2Obj.SetActive(true);
            }

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Entra el último cliente del turno...", 3.2f);
            }
        }

        public bool DebeDispararClimaxAlCobrar()
        {
            if (climaxEnCurso || faseActual != FaseTurno.ClientesFinales) return false;
            if (clientesFinalesAtendidos < 1 || clienteFinal2Obj == null || !clienteFinal2Obj.activeInHierarchy) return false;

            var cg = clienteFinal2Obj.GetComponent<ClienteGuionadoMundo1>();
            return cg == null || cg.EstaEsperandoPago;
        }

        public void DispararSecuenciaFinalUltimoCliente()
        {
            if (climaxEnCurso) return;
            climaxEnCurso = true;
            StartCoroutine(RutinaClimaxFinalJosh());
        }

        private IEnumerator RutinaClimaxFinalJosh()
        {
            faseActual = FaseTurno.ApagonYClimax;

            if (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta)
            {
                CajaRegistradoraUI.Instance.CerrarCaja();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlayerMovement playerMov = FindAnyObjectByType<PlayerMovement>();
            Camara cam = FindAnyObjectByType<Camara>();
            Camera camPrincipal = cam != null ? cam.GetComponent<Camera>() : Camera.main;
            JugadorRecolector recolector = FindAnyObjectByType<JugadorRecolector>();

            if (playerMov != null)
            {
                playerMov.EstablecerPenalizacionDolor(false, 1f);
                playerMov.EstablecerLimiteZonaCaja(false, Vector3.zero, 0f, 0f);
                playerMov.enabled = false;
            }
            if (recolector != null) recolector.enabled = false;
            if (cam != null)
            {
                cam.EstablecerModificadorSensibilidad(1f);
                cam.EstablecerControlManualPausado(true);
            }

            // 1. Lluvia muchísimo más fuerte + cámara gira sola hacia la izquierda sacando al cliente de vista
            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.IntensificarTormenta(1.0f, 1.0f);
            }

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo("Tú: '¿Qué le pasa a la lluvia? Empezó a llover más fuerte... así nunca podré irme.'", 3.4f);
            }

            if (cam != null)
            {
                yield return StartCoroutine(cam.RutinaGirarRelativoSuave(-72f, 1.15f));
            }
            yield return new WaitForSeconds(1.25f);

            // 2. Fuera de cámara: reemplazar a Josh por el monstruo mirando al mostrador
            PrepararClonMonstruoVisual();
            Vector3 posMostrador = puntoClienteMostrador != null
                ? puntoClienteMostrador.position
                : (clienteFinal2Obj != null ? clienteFinal2Obj.transform.position : Vector3.zero);
            Quaternion rotHaciaJugador = clienteFinal2Obj != null ? clienteFinal2Obj.transform.rotation : Quaternion.identity;

            if (clienteFinal2Obj != null)
            {
                var cg = clienteFinal2Obj.GetComponent<ClienteGuionadoMundo1>();
                if (cg != null) cg.enabled = false;
                clienteFinal2Obj.SetActive(false);
            }

            if (clonMonstruoVisual != null)
            {
                clonMonstruoVisual.transform.position = posMostrador;
                if (playerMov != null)
                {
                    Vector3 dir = playerMov.transform.position - posMostrador;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) rotHaciaJugador = Quaternion.LookRotation(dir);
                }
                clonMonstruoVisual.transform.rotation = rotHaciaJugador;
                if (animadorClonMonstruo != null) animadorClonMonstruo.speed = 0.25f;
                clonMonstruoVisual.SetActive(true);
            }

            // 3. Regresa la cámara hacia el cliente -> trueno + revelación monstruo
            if (cam != null)
            {
                yield return StartCoroutine(cam.RutinaGirarRelativoSuave(72f, 0.55f));
            }

            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.DispararRelampago(null, null, 0.25f);
            }
            yield return new WaitForSeconds(0.28f);

            // 4. Mismo susto del almacén: oscuridad, luz tenue en rostro, embestida rápida y transición
            if (cam != null) cam.enabled = false;
            AsegurarOverlayOscurecimiento();
            SetEstadoLucesTienda(false);

            if (sonidoApagon != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoApagon, 0.9f);
            }

            if (RetroCameraOverlay.Instance != null)
            {
                RetroCameraOverlay.Instance.EstablecerInterferenciaPersecucion(0f);
                RetroCameraOverlay.Instance.EstablecerInterferenciaBase(0f);
                RetroCameraOverlay.Instance.DispararGlitch(0.80f);
            }

            yield return StartCoroutine(FundirOscurecimiento(0f, 0.80f, 0.12f));

            Transform trJugador = playerMov != null ? playerMov.transform : (camPrincipal != null ? camPrincipal.transform : transform);
            Vector3 posCam = camPrincipal != null ? camPrincipal.transform.position : trJugador.position + Vector3.up * 1.6f;
            Vector3 forwardPlano = camPrincipal != null ? camPrincipal.transform.forward : trJugador.forward;
            forwardPlano.y = 0f;
            if (forwardPlano.sqrMagnitude < 0.01f) forwardPlano = Vector3.forward;
            forwardPlano.Normalize();

            Vector3 posInicioSusto = posCam + forwardPlano * 2.65f;
            posInicioSusto.y = posMostrador.y;
            Vector3 posFinSusto = posCam + forwardPlano * 0.90f;
            posFinSusto.y = posMostrador.y;

            if (clonMonstruoVisual != null)
            {
                clonMonstruoVisual.transform.position = posInicioSusto;
                Vector3 dirAMirar = posCam - clonMonstruoVisual.transform.position;
                dirAMirar.y = 0f;
                if (dirAMirar.sqrMagnitude > 0.001f)
                {
                    clonMonstruoVisual.transform.rotation = Quaternion.LookRotation(dirAMirar);
                }
                if (animadorClonMonstruo != null) animadorClonMonstruo.speed = 3.2f;
            }

            if (luzSustoClimax != null)
            {
                luzSustoClimax.transform.position = posCam + forwardPlano * 0.6f + Vector3.up * 0.15f;
                luzSustoClimax.enabled = true;
            }

            if (sonidoJumpscare != null && audioSourceEfectos != null)
            {
                audioSourceEfectos.PlayOneShot(sonidoJumpscare, 1f);
            }

            Quaternion rotCamInicio = camPrincipal != null ? camPrincipal.transform.rotation : Quaternion.identity;
            float duracionAvance = 0.55f;
            float t = 0f;
            while (t < duracionAvance)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duracionAvance);
                float curva = p * p;

                if (clonMonstruoVisual != null)
                {
                    clonMonstruoVisual.transform.position = Vector3.Lerp(posInicioSusto, posFinSusto, curva);
                    if (camPrincipal != null)
                    {
                        Vector3 rostroObjetivo = clonMonstruoVisual.transform.position + Vector3.up * 1.78f;
                        Quaternion rotObjetivo = Quaternion.LookRotation(rostroObjetivo - camPrincipal.transform.position);
                        camPrincipal.transform.rotation = Quaternion.Slerp(rotCamInicio, rotObjetivo, Mathf.Clamp01(p * 3.5f));
                    }
                }
                yield return null;
            }

            yield return StartCoroutine(FundirOscurecimiento(0.80f, 1f, 0.22f));

            if (luzSustoClimax != null) luzSustoClimax.enabled = false;
            if (clonMonstruoVisual != null) clonMonstruoVisual.SetActive(false);
            EstablecerLatidoContinuo(false, 0f);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("clientes_finales");
                HUDTareasMundo1.Instance.OcultarSubtitulos();
            }

            yield return new WaitForSecondsRealtime(0.45f);
            faseActual = FaseTurno.TransicionFinal;

            if (TransicionNivel.Instance != null)
            {
                TransicionNivel.Instance.IniciarTransicionSiguienteNivel(false);
            }
            else
            {
                string escena = ConfiguracionSiguienteMapa.Instance != null && !string.IsNullOrEmpty(ConfiguracionSiguienteMapa.Instance.NombreSiguienteEscena)
                    ? ConfiguracionSiguienteMapa.Instance.NombreSiguienteEscena
                    : "NIvelHarold";
                SceneManager.LoadScene(escena);
            }
        }

        #endregion

        #region Limpieza de Sistemas Antiguos

        private void LimpiarSistemasAntiguos()
        {
            // 1. Desactivar el Spawner aleatorio antiguo
            var spawnerViejo = FindAnyObjectByType<Tienda.Clientes.SpawnerClientes>();
            if (spawnerViejo != null)
            {
                spawnerViejo.gameObject.SetActive(false);
            }

            // 2. Desactivar/Ocultar todos los NPCs con IA aleatoria que no sean el gerente
            var clientesViejos = FindObjectsByType<Tienda.Clientes.ClienteIA>(FindObjectsInactive.Exclude);
            foreach (var c in clientesViejos)
            {
                if (c.gameObject.name == "[Gerente_Prologo]" || (CinematicaGerente.Instance != null && c.gameObject == CinematicaGerente.Instance.gameObject))
                {
                    // En el gerente quitamos la IA de cliente para que no se mueva como comprador ni ponga iconos
                    c.enabled = false;
                    var icono = c.GetComponent<Tienda.Clientes.IconoFlotanteCliente>();
                    if (icono != null) icono.enabled = false;
                }
                else
                {
                    // Desactivar a todos los otros clientes de la escena para que la tienda esté vacía al inicio
                    c.gameObject.SetActive(false);
                }
            }

            // 3. Desactivar el grupo --NPC-- si existe
            var grupoNPC = GameObject.Find("--NPC--");
            if (grupoNPC != null)
            {
                for (int i = 0; i < grupoNPC.transform.childCount; i++)
                {
                    var hijo = grupoNPC.transform.GetChild(i);
                    if (hijo.GetComponent<Tienda.Clientes.ClienteIA>() != null)
                    {
                        hijo.gameObject.SetActive(false);
                    }
                }
            }

            // 4. Desactivar el HUD de saldo de la tienda viejo ($100.00) si ya existía
            var hudViejo = Tienda.SistemaVentas.HUDTienda.Instance ?? FindAnyObjectByType<Tienda.SistemaVentas.HUDTienda>();
            if (hudViejo != null)
            {
                Destroy(hudViejo.gameObject);
            }
        }

        #endregion
    }
}
