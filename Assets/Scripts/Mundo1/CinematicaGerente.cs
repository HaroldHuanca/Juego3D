using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Mundo1.UI;

namespace Mundo1
{
    /// <summary>
    /// Cinemática de prólogo de Mundo 1:
    /// 1. Toma exterior contemplativa mirando la tienda en la noche (pasos sobre asfalto).
    /// 2. Fundido a negro y campanilla de entrada.
    /// 3. Dentro de la tienda frente al gerente (posiciona al jugador cara a cara, vista limpia).
    /// 4. El gerente explica el turno y pide colocarse la cámara corporal.
    /// 5. El jugador presiona [ESPACIO] para colocarse la cámara corporal -> ¡Se enciende la vista VHS!
    /// 6. El gerente se despide, camina a la puerta y al salir se produce el trueno con swap al monstruo.
    /// 7. Fin del prólogo: se liberan los controles y aparece el HUD de tareas.
    /// </summary>
    [DisallowMultipleComponent]
    public class CinematicaGerente : MonoBehaviour
    {
        public static CinematicaGerente Instance { get; private set; }

        [Header("--- FASE 1: TOMA EXTERIOR CONTEMPLATIVA ---")]
        [Tooltip("Transform de la cámara o posición fija exterior mirando hacia la fachada de la tienda")]
        [SerializeField] private Transform puntoCamaraExterior;
        [SerializeField] private float duracionTomaExterior = 3.5f;
        [SerializeField] private AudioClip sonidoPasosExterior;
        [SerializeField] private AudioClip sonidoCampanillaPuerta;

        [Header("--- FASE 2: GERENTE DENTRO DE LA TIENDA ---")]
        [SerializeField] private GameObject objetoGerenteRaiz;
        [SerializeField] private Animator animadorGerente;
        [SerializeField] private NavMeshAgent agenteGerente;
        [SerializeField] private GameObject modeloGerenteNormal;
        [SerializeField] private GameObject modeloGerenteMonstruo;

        [Tooltip("Punto exterior o umbral de la puerta hacia donde camina el gerente para marcharse")]
        [SerializeField] private Transform puntoSalidaPuerta;

        [Tooltip("Punto exterior final (calle o estacionamiento) hacia donde el gerente sigue su camino antes del susto")]
        [SerializeField] private Transform puntoExteriorFinal;

        [Header("--- POSICIÓN DEL JUGADOR FRENTE AL GERENTE ---")]
        [Tooltip("Punto exacto donde se ubica el jugador al entrar. Si está vacío, se calcula automáticamente a 2 metros frente al gerente.")]
        [SerializeField] private Transform puntoJugadorFrenteGerente;

        [Header("--- FASE 3: COLOCARSE LA CÁMARA CORPORAL ---")]
        [Tooltip("Referencia al overlay de cámara retro VHS existente en la escena")]
        [SerializeField] private RetroCameraOverlay overlayCamaraRetro;
        [SerializeField] private AudioClip sonidoBipCamara;

        [Header("--- CONTROL DEL JUGADOR ---")]
        [SerializeField] private Camera camaraPrincipal;
        [SerializeField] private Transform transformJugadorRaiz;
        [SerializeField] private MonoBehaviour scriptMovimientoJugador;
        [SerializeField] private MonoBehaviour scriptCamaraJugador;

        [Header("--- DIÁLOGOS Y AUDIO NARRATIVO ---")]
        [TextArea(2, 3)]
        [SerializeField] private string textoIntroExterior = "23:45 PM - TIENDA 6TWELVE, CARRETERA NORTE\nLlegas a cubrir el turno nocturno...";
        [TextArea(2, 3)]
        [SerializeField] private string dialogo1 = "Gerente: 'Buenas noches. Gracias por venir a cubrir el turno.'";
        [TextArea(2, 3)]
        [SerializeField] private string dialogo2 = "Gerente: 'Tu trabajo es simple: atiende a los clientes en la caja y mantén los anaqueles surtidos.'";
        [TextArea(2, 3)]
        [SerializeField] private string dialogo3 = "Gerente: 'Pero antes de comenzar, la empresa exige que te coloques la CÁMARA CORPORAL de seguridad.'";
        [TextArea(2, 3)]
        [SerializeField] private string dialogo4 = "Gerente: 'Excelente. Ahora estás grabando. Pase lo que pase... nunca apagues esa cámara.'";
        [TextArea(2, 3)]
        [SerializeField] private string dialogo5 = "Gerente: 'Cierro la puerta al salir. Que tengas buena noche.'";
        [TextArea(2, 3)]
        [SerializeField] private string pensamientoFinal = "Tú: '¿Qué demonios fue ese destello en la oscuridad...? Mejor me concentro en el turno.'";
        [Tooltip("Clip de audio opcional que suena con la reacción o pensamiento final")]
        [SerializeField] private AudioClip sonidoPensamientoFinal;

        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamHablando = Animator.StringToHash("Hablando");

        public event Action OnCinematicaFinalizada;
        public bool EstaEnCinematica { get; private set; }

        // Componentes internos para el fundido a negro
        private CanvasGroup canvasGroupFade;
        private AudioSource audioSourceLocal;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            EstaEnCinematica = true;

            AsegurarReferencias();
            BloquearControlesJugador(true);
        }

        private void OnValidate()
        {
            AutoDetectarComponentes();
        }

        private void AutoDetectarComponentes()
        {
            if (objetoGerenteRaiz == null) objetoGerenteRaiz = gameObject;
            if (animadorGerente == null) animadorGerente = GetComponentInChildren<Animator>();
            if (agenteGerente == null) agenteGerente = GetComponentInChildren<NavMeshAgent>();

            if (camaraPrincipal == null) camaraPrincipal = Camera.main;

            if (scriptCamaraJugador == null && camaraPrincipal != null)
            {
                scriptCamaraJugador = camaraPrincipal.GetComponent<Camara>();
            }

            if (overlayCamaraRetro == null)
            {
                overlayCamaraRetro = FindAnyObjectByType<RetroCameraOverlay>(FindObjectsInactive.Include);
            }

            if (transformJugadorRaiz == null)
            {
                var mov = FindAnyObjectByType<PlayerMovement>();
                if (mov != null)
                {
                    transformJugadorRaiz = mov.transform;
                    if (scriptMovimientoJugador == null) scriptMovimientoJugador = mov;
                }
            }

            if (modeloGerenteNormal == null)
            {
                modeloGerenteNormal = objetoGerenteRaiz;
            }

            if (modeloGerenteMonstruo == null)
            {
                var m = GameObject.Find("FinalMonster 1") ?? GameObject.Find("Monstruo");
                if (m != null) modeloGerenteMonstruo = m;
            }

            if (puntoSalidaPuerta == null)
            {
                var p = GameObject.Find("Punto_SalidaPuerta");
                if (p != null) puntoSalidaPuerta = p.transform;
            }

            if (puntoExteriorFinal == null)
            {
                var p = GameObject.Find("Punto_ExteriorFinal");
                if (p != null)
                {
                    puntoExteriorFinal = p.transform;
                }
                else if (puntoSalidaPuerta != null)
                {
                    var pExt = new GameObject("Punto_ExteriorFinal");
                    Vector3 dirAfuera = puntoSalidaPuerta.forward;
                    if (puntoJugadorFrenteGerente != null)
                    {
                        dirAfuera = (puntoSalidaPuerta.position - puntoJugadorFrenteGerente.position).normalized;
                        dirAfuera.y = 0f;
                    }
                    if (dirAfuera.sqrMagnitude < 0.1f) dirAfuera = Vector3.forward;
                    pExt.transform.position = puntoSalidaPuerta.position + dirAfuera * 10f;
                    puntoExteriorFinal = pExt.transform;
                }
            }
        }

        private GameObject clonMonstruoSusto;

        private void AsegurarReferencias()
        {
            AutoDetectarComponentes();

            // Iniciar con la cámara retro apagada inmediatamente para que comience con vista normal
            if (overlayCamaraRetro != null)
            {
                overlayCamaraRetro.ActivarModoCamara(false, inmediato: true);
            }

            // Preparar el monstruo para el susto sin apagar al monstruo real del almacén
            PrepararMonstruoSusto();

            // Asegurar que el AudioListener de la escena esté encendido
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            foreach (var l in listeners)
            {
                if (!l.enabled) l.enabled = true;
            }

            audioSourceLocal = GetComponent<AudioSource>();
            if (audioSourceLocal == null) audioSourceLocal = gameObject.AddComponent<AudioSource>();
            audioSourceLocal.playOnAwake = false;
            audioSourceLocal.spatialBlend = 0f; // 2D universal para que se escuche siempre en la cinemática
            audioSourceLocal.volume = 1f;

#if UNITY_EDITOR
            if (sonidoPasosExterior == null)
            {
                sonidoPasosExterior = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Cinematica/sonidoPasosExterior.mp3");
            }
            if (sonidoCampanillaPuerta == null)
            {
                sonidoCampanillaPuerta = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Cinematica/sonidoCampanillaPuerta.mp3");
            }
            if (sonidoBipCamara == null)
            {
                sonidoBipCamara = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Cinematica/sonidoBipCamara.mp3");
            }
#endif

            CrearPantallaFade();
        }

        private void PrepararMonstruoSusto()
        {
            if (clonMonstruoSusto != null) return;

            if (modeloGerenteMonstruo != null)
            {
                // Si el modelo asignado es el monstruo que patrulla el almacén, no lo desactivamos
                bool esDelAlmacen = modeloGerenteMonstruo.GetComponentInParent<MonstruoWarehouse>() != null ||
                                   modeloGerenteMonstruo.GetComponent<MonstruoWarehouse>() != null ||
                                   modeloGerenteMonstruo.name.Contains("Warehouse");

                if (esDelAlmacen)
                {
                    // Dejar al monstruo del almacén completamente activo para cuando el jugador vaya allá
                    modeloGerenteMonstruo.SetActive(true);

                    // Instanciar un clon puramente visual para el susto de la cinemática
                    clonMonstruoSusto = Instantiate(modeloGerenteMonstruo);
                    clonMonstruoSusto.name = "[Monstruo_Susto_Cinematica]";

                    // Limpiar componentes de navegación o lógica para que sea solo una estatua visual
                    var nav = clonMonstruoSusto.GetComponent<NavMeshAgent>();
                    if (nav != null) Destroy(nav);
                    var mw = clonMonstruoSusto.GetComponent<MonstruoWarehouse>();
                    if (mw != null) Destroy(mw);
                    var col = clonMonstruoSusto.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    clonMonstruoSusto.SetActive(false);
                    return;
                }

                // Si es un prefab o un objeto independiente, solo aseguramos que inicie apagado
                modeloGerenteMonstruo.SetActive(false);
                clonMonstruoSusto = modeloGerenteMonstruo;
            }
        }

        private void Start()
        {
            if (camaraPrincipal == null || transformJugadorRaiz == null || overlayCamaraRetro == null)
            {
                AutoDetectarComponentes();
            }
        }

        private IEnumerator EsperarSubtitulo(float tiempoMaximo)
        {
            float transcurrido = 0f;
            while (HUDTareasMundo1.Instance != null && HUDTareasMundo1.Instance.EstaMostrandoSubtitulo && transcurrido < tiempoMaximo)
            {
                transcurrido += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Inicia toda la secuencia inicial pulida paso a paso.
        /// </summary>
        public void IniciarCinematica()
        {
            EstaEnCinematica = true;
            StartCoroutine(RutinaCinematicaCompleta());
        }

        private IEnumerator RutinaCinematicaCompleta()
        {
            // Mantener pantalla negra y controles bloqueados mientras termina la carga inicial
            if (canvasGroupFade != null) canvasGroupFade.alpha = 1f;
            BloquearControlesJugador(true);
            if (overlayCamaraRetro != null) overlayCamaraRetro.ActivarModoCamara(false, inmediato: true);

            Vector3 posOriginalCamara = Vector3.zero;
            Quaternion rotOriginalCamara = Quaternion.identity;
            Transform padreOriginalCamara = null;

            if (camaraPrincipal != null)
            {
                padreOriginalCamara = camaraPrincipal.transform.parent;
                posOriginalCamara = camaraPrincipal.transform.localPosition;
                rotOriginalCamara = camaraPrincipal.transform.localRotation;

                if (puntoCamaraExterior != null)
                {
                    camaraPrincipal.transform.SetParent(null);
                    camaraPrincipal.transform.position = puntoCamaraExterior.position;
                    camaraPrincipal.transform.rotation = puntoCamaraExterior.rotation;
                }
            }

            // Esperar un frame en negro para absorber cualquier pico de carga inicial de la escena
            yield return null;

            // ==========================================================
            // ACTO 1: TOMA EXTERIOR CONTEMPLATIVA (Fachada nocturna)
            // ==========================================================
            if (puntoCamaraExterior != null && camaraPrincipal != null)
            {
                // Sonido de pasos sobre asfalto / lluvia
                if (sonidoPasosExterior != null && audioSourceLocal != null)
                {
                    audioSourceLocal.PlayOneShot(sonidoPasosExterior, 0.85f);
                }

                if (HUDTareasMundo1.Instance != null)
                {
                    HUDTareasMundo1.Instance.MostrarSubtitulo(textoIntroExterior, duracionTomaExterior - 0.5f);
                }

                // Abrir pantalla (Fade In)
                yield return StartCoroutine(RutinaFade(1f, 0f, 1.2f));

                yield return new WaitForSeconds(duracionTomaExterior);

                if (HUDTareasMundo1.Instance != null)
                {
                    HUDTareasMundo1.Instance.OcultarSubtitulos();
                }

                // Fundido a negro para entrar a la tienda (Fade Out)
                yield return StartCoroutine(RutinaFade(0f, 1f, 1.0f));

                // Sonido de campanilla de la puerta
                if (sonidoCampanillaPuerta != null && audioSourceLocal != null)
                {
                    audioSourceLocal.PlayOneShot(sonidoCampanillaPuerta, 1f);
                }
                yield return new WaitForSeconds(0.8f);

                // Restaurar la cámara a la cabeza del jugador
                camaraPrincipal.transform.SetParent(padreOriginalCamara);
                camaraPrincipal.transform.localPosition = posOriginalCamara;
                camaraPrincipal.transform.localRotation = rotOriginalCamara;
            }
            else
            {
                if (canvasGroupFade != null) canvasGroupFade.alpha = 1f;
            }

            // ==========================================================
            // ACTO 2: POSICIONAR AL JUGADOR CARA A CARA CON EL GERENTE
            // ==========================================================
            // Si el jugador está en otro lugar, lo colocamos exactamente frente al gerente
            PosicionarJugadorFrenteAlGerente();

            // Asegurar que la cámara retro esté apagada (vista normal sin efectos)
            if (overlayCamaraRetro != null) overlayCamaraRetro.ActivarModoCamara(false, inmediato: true);

            // Gerente mirando al jugador y activando animación de gesticular/hablar
            if (animadorGerente != null)
            {
                animadorGerente.SetFloat(ParamSpeed, 0f);
                animadorGerente.SetBool(ParamHablando, true);
            }

            // Abrir pantalla dentro de la tienda
            yield return StartCoroutine(RutinaFade(1f, 0f, 1.0f));

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea("camara_init", "Habla con el gerente de la tienda", false);
                HUDTareasMundo1.Instance.MostrarSubtitulo(dialogo1, 2.8f);
                yield return StartCoroutine(EsperarSubtitulo(5.0f));

                HUDTareasMundo1.Instance.MostrarSubtitulo(dialogo2, 3.2f);
                yield return StartCoroutine(EsperarSubtitulo(6.0f));

                HUDTareasMundo1.Instance.MostrarSubtitulo(dialogo3, 3.2f);
                yield return StartCoroutine(EsperarSubtitulo(6.0f));
            }
            else
            {
                yield return new WaitForSeconds(13f);
            }

            // Pausa en la gesticulación mientras espera que te pongas la cámara
            if (animadorGerente != null) animadorGerente.SetBool(ParamHablando, false);

            // ==========================================================
            // ACTO 3: INTERACCIÓN: COLOCARSE LA CÁMARA CORPORAL
            // ==========================================================
            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.EstablecerTarea("camara_init", "Presiona [ESPACIO] para colocarte la cámara corporal", false);
                HUDTareasMundo1.Instance.MostrarSubtitulo("<color=#55FF77>✦ PRESIONA [ESPACIO] O CLICK PARA COLOCARTE LA CÁMARA CORPORAL ✦</color>", 999f);
            }

            // Esperar input del jugador
            bool camaraColocada = false;
            while (!camaraColocada)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                {
                    camaraColocada = true;
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    camaraColocada = true;
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
                {
                    camaraColocada = true;
                }
#endif
                yield return null;
            }

            // Sonido de activación de la cámara
            if (sonidoBipCamara != null && audioSourceLocal != null)
            {
                audioSourceLocal.PlayOneShot(sonidoBipCamara, 1f);
            }

            // ¡ACTIVAR LA VISTA DE CÁMARA RETRO VHS!
            if (overlayCamaraRetro != null)
            {
                overlayCamaraRetro.ActivarModoCamara(true);
                overlayCamaraRetro.DispararGlitch(0.45f);
            }

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MarcarTareaCompletada("camara_init");
                HUDTareasMundo1.Instance.MostrarSubtitulo("<color=#66DD77>● GRABACIÓN INICIADA - CÁMARA CORPORAL ACTIVA</color>", 2.2f);
                yield return StartCoroutine(EsperarSubtitulo(4.5f));
            }
            else
            {
                yield return new WaitForSeconds(2.8f);
            }

            // ==========================================================
            // ACTO 4: DESPEDIDA DEL GERENTE Y MARCHA
            // ==========================================================
            if (animadorGerente != null) animadorGerente.SetBool(ParamHablando, true);

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo(dialogo4, 3.0f);
                yield return StartCoroutine(EsperarSubtitulo(5.5f));

                HUDTareasMundo1.Instance.MostrarSubtitulo(dialogo5, 2.6f);
                yield return StartCoroutine(EsperarSubtitulo(5.0f));
            }
            else
            {
                yield return new WaitForSeconds(8.0f);
            }

            if (animadorGerente != null) animadorGerente.SetBool(ParamHablando, false);
            if (HUDTareasMundo1.Instance != null) HUDTareasMundo1.Instance.OcultarSubtitulos();

            // ==========================================================
            // ACTO 4: MARCHA DEL GERENTE (PUERTA -> TRUENO -> EXTERIOR)
            // ==========================================================
            // Tramo 1: Caminar hacia la puerta de salida de la tienda
            Vector3 destinoPuerta;
            if (puntoSalidaPuerta != null)
            {
                destinoPuerta = puntoSalidaPuerta.position;
            }
            else if (objetoGerenteRaiz != null)
            {
                destinoPuerta = objetoGerenteRaiz.transform.position + objetoGerenteRaiz.transform.forward * 8f;
            }
            else
            {
                destinoPuerta = Vector3.forward * 10f;
            }

            yield return StartCoroutine(MoverGerenteHacia(destinoPuerta, 2.2f, 5.5f));

            // ==========================================================
            // ACTO 5: SUSTO DEL RELÁMPAGO Y TRUENO EN LA PUERTA DE SALIDA
            // ==========================================================
            bool relampagoTerminado = false;
            PrepararMonstruoSusto();
            GameObject monstruoSustoActivo = clonMonstruoSusto != null ? clonMonstruoSusto : modeloGerenteMonstruo;

            if (monstruoSustoActivo != null && objetoGerenteRaiz != null)
            {
                monstruoSustoActivo.transform.position = objetoGerenteRaiz.transform.position;
                monstruoSustoActivo.transform.rotation = objetoGerenteRaiz.transform.rotation;
            }

            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.DispararRelampago(
                    modeloGerenteNormal,
                    monstruoSustoActivo,
                    0.25f,
                    () => { relampagoTerminado = true; }
                );

                float tiempoEsperaRelampago = 0f;
                while (!relampagoTerminado && tiempoEsperaRelampago < 3.0f)
                {
                    tiempoEsperaRelampago += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.25f);
            }

            if (clonMonstruoSusto != null)
            {
                clonMonstruoSusto.SetActive(false);
            }

            // Asegurar que los renderers del gerente estén visibles para continuar su camino hacia afuera
            if (objetoGerenteRaiz != null)
            {
                var rManager = objetoGerenteRaiz.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rManager) r.enabled = true;
            }

            // Tramo 2: Continuar su camino hacia el exterior (en la noche / estacionamiento)
            // Desactivamos el NavMeshAgent y ponemos colliders en trigger para cruzar la puerta sin atorarse
            if (agenteGerente != null) agenteGerente.enabled = false;
            var colsGerente = objetoGerenteRaiz != null ? objetoGerenteRaiz.GetComponentsInChildren<Collider>() : null;
            if (colsGerente != null)
            {
                foreach (var c in colsGerente) c.isTrigger = true;
            }

            Vector3 destinoExterior;
            if (puntoExteriorFinal != null)
            {
                destinoExterior = puntoExteriorFinal.position;
            }
            else
            {
                // Si no hay un punto configurado, avanza 10 metros más hacia afuera en línea recta
                Vector3 dirAfuera = objetoGerenteRaiz != null ? objetoGerenteRaiz.transform.forward : Vector3.forward;
                destinoExterior = destinoPuerta + dirAfuera * 10f;
            }

            yield return StartCoroutine(MoverGerenteHacia(destinoExterior, 2.0f, 4.5f));

            // Desaparece el gerente en la noche al final de su camino exterior (ocultando renderers sin matar este GameObject)
            if (objetoGerenteRaiz != null)
            {
                var rManager = objetoGerenteRaiz.GetComponentsInChildren<Renderer>();
                foreach (var r in rManager) r.enabled = false;
                var cManager = objetoGerenteRaiz.GetComponentsInChildren<Collider>();
                foreach (var c in cManager) c.enabled = false;

                if (objetoGerenteRaiz != gameObject)
                {
                    objetoGerenteRaiz.SetActive(false);
                }
            }

            // Reacción y último audio del jugador
            if (sonidoPensamientoFinal != null && audioSourceLocal != null)
            {
                audioSourceLocal.PlayOneShot(sonidoPensamientoFinal, 1f);
            }

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo(pensamientoFinal, 3.2f);
                yield return StartCoroutine(EsperarSubtitulo(6.0f));
                HUDTareasMundo1.Instance.OcultarSubtitulos();
            }
            else
            {
                yield return new WaitForSeconds(4.5f);
            }

            // ==========================================================
            // FUNDIDO A NEGRO TOTAL Y PAUSA ANTES DE INICIAR EL JUEGO
            // ==========================================================
            // Todo se vuelve negro
            yield return StartCoroutine(RutinaFade(0f, 1f, 1.2f));

            // 1.2 segundos de silencio y máxima tensión a oscuras
            yield return new WaitForSeconds(1.2f);

            // Se aclara la pantalla de vuelta a la tienda
            yield return StartCoroutine(RutinaFade(1f, 0f, 1.0f));

            // ==========================================================
            // ACTO 6: INICIO DEL GAMEPLAY REAL
            // ==========================================================
            // Limpiar cualquier subtítulo residual antes de liberar controles
            if (HUDTareasMundo1.Instance != null) HUDTareasMundo1.Instance.OcultarSubtitulos();

            // Recién aquí se liberan los controles para que el jugador empiece a moverse
            EstaEnCinematica = false;
            BloquearControlesJugador(false);

            Debug.Log("<color=green>[Cinemática Prólogo] Prólogo completado con éxito. Controles liberados.</color>");
            OnCinematicaFinalizada?.Invoke();
        }

        /// <summary>
        /// Desplaza al gerente suavemente hacia un punto de ruta, usando NavMeshAgent si es posible o traslación directa.
        /// </summary>
        private IEnumerator MoverGerenteHacia(Vector3 destino, float velocidad, float tiempoMaximo)
        {
            if (objetoGerenteRaiz == null) yield break;

            bool usarNavMesh = false;
            if (agenteGerente != null && agenteGerente.enabled)
            {
                if (!agenteGerente.isOnNavMesh)
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(objetoGerenteRaiz.transform.position, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agenteGerente.Warp(hit.position);
                    }
                }

                if (agenteGerente.isOnNavMesh)
                {
                    try
                    {
                        agenteGerente.speed = velocidad;
                        agenteGerente.isStopped = false;
                        agenteGerente.SetDestination(destino);
                        usarNavMesh = true;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[CinematicaGerente] No se pudo usar NavMeshAgent: {ex.Message}. Usando traslación suave.");
                        usarNavMesh = false;
                    }
                }
            }

            if (animadorGerente != null) animadorGerente.SetFloat(ParamSpeed, 1.4f);

            float tiempoPasado = 0f;
            while (tiempoPasado < tiempoMaximo)
            {
                tiempoPasado += Time.deltaTime;

                float distHorizontal = Vector2.Distance(
                    new Vector2(objetoGerenteRaiz.transform.position.x, objetoGerenteRaiz.transform.position.z),
                    new Vector2(destino.x, destino.z)
                );

                if (distHorizontal <= 1.2f)
                {
                    break;
                }

                if (usarNavMesh && agenteGerente != null && agenteGerente.enabled && agenteGerente.isOnNavMesh)
                {
                    try
                    {
                        if (animadorGerente != null)
                        {
                            animadorGerente.SetFloat(ParamSpeed, Mathf.Max(agenteGerente.velocity.magnitude, 1.2f));
                        }

                        if (!agenteGerente.pathPending && agenteGerente.remainingDistance <= 1.0f && distHorizontal <= 1.5f)
                        {
                            break;
                        }

                        // Si el agente se frena (por ejemplo fin de NavMesh cerca de la puerta),
                        // cambiar a traslación directa para continuar avanzando sin atorarse
                        if (tiempoPasado > 1.5f && agenteGerente.velocity.sqrMagnitude < 0.02f)
                        {
                            agenteGerente.enabled = false;
                            usarNavMesh = false;
                        }
                    }
                    catch
                    {
                        usarNavMesh = false;
                    }
                }

                if (!usarNavMesh)
                {
                    // Fallback sin NavMesh: rotación y avance suave directo hacia el destino
                    Vector3 direccion = destino - objetoGerenteRaiz.transform.position;
                    direccion.y = 0f;

                    if (direccion.sqrMagnitude > 0.02f)
                    {
                        objetoGerenteRaiz.transform.rotation = Quaternion.Slerp(
                            objetoGerenteRaiz.transform.rotation,
                            Quaternion.LookRotation(direccion),
                            Time.deltaTime * 6f
                        );
                        objetoGerenteRaiz.transform.position = Vector3.MoveTowards(
                            objetoGerenteRaiz.transform.position,
                            new Vector3(destino.x, objetoGerenteRaiz.transform.position.y, destino.z),
                            velocidad * Time.deltaTime
                        );
                    }
                    else
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (animadorGerente != null) animadorGerente.SetFloat(ParamSpeed, 0f);

            if (usarNavMesh && agenteGerente != null && agenteGerente.enabled && agenteGerente.isOnNavMesh)
            {
                try { agenteGerente.isStopped = true; } catch { }
            }
        }

        private void PosicionarJugadorFrenteAlGerente()
        {
            if (transformJugadorRaiz == null)
            {
                var mov = FindAnyObjectByType<PlayerMovement>();
                if (mov != null) transformJugadorRaiz = mov.transform;
            }

            if (transformJugadorRaiz == null) return;

            var cc = transformJugadorRaiz.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (puntoJugadorFrenteGerente != null)
            {
                transformJugadorRaiz.position = puntoJugadorFrenteGerente.position;
                transformJugadorRaiz.rotation = puntoJugadorFrenteGerente.rotation;
            }
            else if (objetoGerenteRaiz != null)
            {
                // Si no hay un punto fijado manualmente, nos paramos automáticamente a 2 metros del gerente mirándolo a los ojos
                Vector3 haciaElJugador = -objetoGerenteRaiz.transform.forward;
                if (haciaElJugador.sqrMagnitude < 0.01f) haciaElJugador = Vector3.back;

                Vector3 posicionFrente = objetoGerenteRaiz.transform.position + (haciaElJugador * 2.2f);
                posicionFrente.y = transformJugadorRaiz.position.y; // Mantener la altura del suelo del jugador
                transformJugadorRaiz.position = posicionFrente;

                // Rotar para mirar directamente al gerente
                Vector3 dirHaciaGerente = objetoGerenteRaiz.transform.position - transformJugadorRaiz.position;
                dirHaciaGerente.y = 0f;
                if (dirHaciaGerente.sqrMagnitude > 0.01f)
                {
                    transformJugadorRaiz.rotation = Quaternion.LookRotation(dirHaciaGerente);
                }
            }

            if (cc != null) cc.enabled = true;

            // Apuntar la cámara directamente a la cabeza del gerente
            if (camaraPrincipal != null && objetoGerenteRaiz != null)
            {
                camaraPrincipal.transform.localRotation = Quaternion.identity;
            }
        }

        private void BloquearControlesJugador(bool bloquear)
        {
            if (scriptMovimientoJugador != null) scriptMovimientoJugador.enabled = !bloquear;
            if (scriptCamaraJugador != null) scriptCamaraJugador.enabled = !bloquear;

            // Asegurar que el ratón quede bloqueado para jugar en primera persona al terminar
            if (!bloquear)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private IEnumerator RutinaFade(float inicio, float fin, float duracion)
        {
            if (canvasGroupFade == null) yield break;

            canvasGroupFade.alpha = inicio;
            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                canvasGroupFade.alpha = Mathf.Lerp(inicio, fin, tiempo / duracion);
                yield return null;
            }
            canvasGroupFade.alpha = fin;
        }

        private void CrearPantallaFade()
        {
            var goCanvas = new GameObject("Canvas_CinematicaFade", typeof(Canvas), typeof(CanvasGroup));
            goCanvas.transform.SetParent(transform, false);
            var c = goCanvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 950;

            canvasGroupFade = goCanvas.GetComponent<CanvasGroup>();
            canvasGroupFade.alpha = 1f;
            canvasGroupFade.blocksRaycasts = false;

            var goImg = new GameObject("Fondo_Negro", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            goImg.transform.SetParent(goCanvas.transform, false);
            var rt = goImg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = goImg.GetComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
        }

        [ContextMenu("Probar Hablar")]
        public void ProbarAnimacionHablar()
        {
            AutoDetectarComponentes();
            if (animadorGerente != null)
            {
                animadorGerente.SetFloat(ParamSpeed, 0f);
                animadorGerente.SetBool(ParamHablando, true);
                Debug.Log("<color=green>[Prueba] Gerente gesticulando / hablando (talking1_npc).</color>");
            }
            else
            {
                Debug.LogWarning("[CinematicaGerente] No se encontró el componente Animator en el Gerente.");
            }
        }

        [ContextMenu("Probar Idle")]
        public void ProbarAnimacionIdle()
        {
            AutoDetectarComponentes();
            if (animadorGerente != null)
            {
                animadorGerente.SetFloat(ParamSpeed, 0f);
                animadorGerente.SetBool(ParamHablando, false);
                Debug.Log("<color=cyan>[Prueba] Gerente en reposo (idle_npc).</color>");
            }
        }

        [ContextMenu("Probar Relámpago y Swap")]
        public void ProbarRelampagoYSwap()
        {
            if (ControladorTormenta.Instance != null)
            {
                ControladorTormenta.Instance.DispararRelampago(modeloGerenteNormal, modeloGerenteMonstruo, 0.22f);
            }
        }
    }
}
