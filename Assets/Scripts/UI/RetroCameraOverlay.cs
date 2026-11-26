using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controlador y HUD para la vista de Cámara Antigua / Videocámara Retro (tipo VHS/Found Footage).
/// Incluye formato 4:3 con bandas laterales, interferencias analógicas (ruido estático y barra de tracking VHS),
/// zoom digital de lente, parpadeo de REC, timecode en tiempo real, visor de autofoco y post-processing URP.
/// </summary>
[DisallowMultipleComponent]
public class RetroCameraOverlay : MonoBehaviour
{
    public static RetroCameraOverlay Instance { get; private set; }

    [Header("Control de Activación")]
    [Tooltip("¿La cámara inicia encendida al comenzar la partida?")]
    [SerializeField] private bool iniciarActiva = true;

    [Tooltip("Permite encender/apagar la cámara presionando una tecla.")]
    [SerializeField] private bool permitirAlternarConTecla = true;

    [Tooltip("Tecla para activar o desactivar la cámara.")]
    [SerializeField] private Key teclaAlternar = Key.C;

    [Header("Formato de Cámara Retro (Widescreen o 4:3)")]
    [Tooltip("Modo Pantalla Completa (16:9 panorámico sin bandas negras): ideal para bodycam moderna estilo Found Footage.")]
    [SerializeField] private bool modoWidescreen = true;

    [Tooltip("Aplica la relación de aspecto 4:3 con bandas laterales (pillarbox), clásica de videocámaras de los 90.")]
    [SerializeField] private bool formatoCamara4x3 = false;

    [SerializeField] private Color colorBandasLaterales = new Color(0.04f, 0.04f, 0.04f, 0.98f);

    [Tooltip("Aplica un ligero zoom de lente (reduce FOV) al entrar en modo cámara.")]
    [SerializeField] private bool aplicarZoomCamara = true;
    [SerializeField] private float fovModoNormal = 60f;
    [SerializeField] private float fovModoCamara = 50f;

    [Header("Interferencias y Efectos VHS")]
    [Tooltip("Activa ruido blanco analógico animado en tiempo real.")]
    [SerializeField] private bool activarRuidoEstatico = true;
    [Range(0f, 0.4f)] [SerializeField] private float opacidadRuido = 0.035f;

    [Tooltip("Activa la típica barra horizontal de distorsión/tracking de cinta VHS.")]
    [SerializeField] private bool activarBarraTracking = true;
    [Range(0f, 0.6f)] [SerializeField] private float opacidadTracking = 0.10f;
    [SerializeField] private float velocidadTracking = 90f;

    [Tooltip("Activa micro-temblores horizontales aleatorios de cinta analógica (Jitter).")]
    [SerializeField] private bool activarJitterHorizontal = true;

    [Tooltip("Líneas de escaneo CRT horizontales.")]
    [SerializeField] private bool mostrarScanlines = true;
    [Range(0f, 0.6f)] [SerializeField] private float opacidadScanlines = 0.04f;

    [Tooltip("Viñeta oscura en las esquinas del visor.")]
    [SerializeField] private bool mostrarVinetaUI = true;
    [Range(0f, 0.8f)] [SerializeField] private float opacidadVinetaUI = 0.22f;

    [Tooltip("Tinte sutil de pantalla LCD/visor antiguo.")]
    [SerializeField] private Color tinteVisor = new Color(0.95f, 1f, 0.98f, 0.02f);

    [Header("Post-Processing URP (Escena 3D)")]
    [Tooltip("Aplica aberración cromática, grano y distorsión de lente mediante un Volume de URP.")]
    [SerializeField] private bool usarPostProcessingRetro = true;

    [Tooltip("Volume de URP asignado (si se deja vacío, buscará o creará uno automáticamente).")]
    [SerializeField] private Volume volumePostProcessing;

    [Range(0f, 1f)] [SerializeField] private float intensidadGrano = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float intensidadAberracion = 0.08f;
    [Range(-0.5f, 0.5f)] [SerializeField] private float curvaturaLente = -0.02f;
    [Range(0f, 1f)] [SerializeField] private float intensidadVinetaVolume = 0.22f;

    [Header("Configuración de Textos y HUD")]
    [Tooltip("Fuente opcional (si se deja vacía, usará la fuente estándar de Unity).")]
    [SerializeField] private Font fuentePersonalizada;

    [Tooltip("Fecha simulada (ej. OCT 24 1997).")]
    [SerializeField] private string fechaSimulada = "OCT 24 1997";

    [Tooltip("Si es true, muestra la fecha y hora del sistema en lugar de la simulada.")]
    [SerializeField] private bool usarHoraReal = false;

    [Tooltip("Modo de cinta o cámara mostrado en pantalla (ej: SP  PLAY ►).")]
    [SerializeField] private string modoGrabacion = "SP  PLAY ►";

    [Tooltip("Velocidad de parpadeo del icono y texto REC en segundos.")]
    [SerializeField] private float intervaloParpadeoRec = 0.70f;

    [Tooltip("Porcentaje inicial de batería.")]
    [Range(0, 100)] [SerializeField] private int nivelBateriaInicial = 88;

    [Tooltip("¿Simular consumo lento de batería mientras la cámara esté activa?")]
    [SerializeField] private bool simularConsumoBateria = true;

    [SerializeField] private bool mostrarVisorCentral = true;
    [SerializeField] private bool mostrarEsquinas = true;

    [SerializeField] private Color colorTextos = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color colorRec = new Color(0.95f, 0.12f, 0.12f, 1f);

    [Header("Efectos de Audio Opcionales")]
    [SerializeField] private AudioClip sonidoEncender;
    [SerializeField] private AudioClip sonidoApagar;
    [SerializeField] private AudioClip sonidoGlitch;

    [Header("Referencias UI (Se auto-conectan al generar)")]
    [SerializeField] private Canvas canvasCamara;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectContenedor4x3;
    [SerializeField] private GameObject bandaIzquierda;
    [SerializeField] private GameObject bandaDerecha;
    [SerializeField] private GameObject panelHUD;
    [SerializeField] private Text textoRec;
    [SerializeField] private Image iconoPuntoRec;
    [SerializeField] private Text textoModo;
    [SerializeField] private Text textoBateria;
    [SerializeField] private Text textoFecha;
    [SerializeField] private Text textoTimecode;
    [SerializeField] private RawImage imagenScanlines;
    [SerializeField] private RawImage imagenVineta;
    [SerializeField] private RawImage imagenRuidoEstatico;
    [SerializeField] private RawImage imagenBarraTracking;
    [SerializeField] private RectTransform rectBarraTracking;
    [SerializeField] private GameObject objetoVisor;
    [SerializeField] private GameObject objetoEsquinas;

    // Estado interno
    private bool camaraActiva = false;
    private float tiempoGrabado = 0f;
    private float temporizadorParpadeo = 0f;
    private bool recVisible = true;
    private float nivelBateriaActual = 88f;

    // Búferes procedurales de interferencia
    private Texture2D[] texturasRuido;
    private int indiceTexturaRuido = 0;
    private float temporizadorFrameRuido = 0f;
    private float posTrackingY = -400f;
    private float temporizadorJitter = 0f;
    private float duracionJitterActual = 0f;
    private float desplazamientoJitterX = 0f;

    private Texture2D texturaScanlines;
    private Texture2D texturaVineta;
    private Texture2D texturaTracking;
    private AudioSource audioSource;
    private AudioSource audioSourceInterferencia;
    private Coroutine transicionRutina;
    private Camera camaraPrincipal;
    private int ultimoPorcentajeBateria = -1;
    private int ultimoFrameTimecode = -1;
    private int ultimoSegundoFecha = -1;
    private float intensidadPersecucionObjetivo = 0f;
    private float intensidadPersecucionActual = 0f;
    private float intensidadBaseObjetivo = 0f;
    private float intensidadBaseActual = 0f;

    public bool CamaraActiva => camaraActiva;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        camaraPrincipal = Camera.main;
        if (camaraPrincipal != null && fovModoNormal <= 0f)
        {
            fovModoNormal = camaraPrincipal.fieldOfView;
        }

        nivelBateriaActual = nivelBateriaInicial;

        // Normalizar parámetros nítidos (limpia valores viejos excesivos de versiones anteriores)
        NormalizarParametrosNitidos();

        AsegurarAudioSource();
        AsegurarTexturasProcedurales();

        // Si la UI no fue armada previamente, se autoconstruye
        if (panelHUD == null)
        {
            ConstruirUIProcedural();
        }

        if (canvasCamara == null) canvasCamara = GetComponent<Canvas>();
        if (canvasCamara != null) canvasCamara.sortingOrder = 30;

        // Asegurar que la cámara tenga activado el Post Processing de URP
        AsegurarPostProcessingCamara();

        if (usarPostProcessingRetro)
        {
            AsegurarVolumePostProcessing();
        }

        // Si hay CinematicaGerente activo en la escena, la cámara corporal debe iniciar apagada hasta que el jugador la active
        bool hayCinematicaInicial = FindAnyObjectByType<Mundo1.CinematicaGerente>() != null;
        camaraActiva = hayCinematicaInicial ? false : iniciarActiva;
        AplicarEstadoVisibilidad(camaraActiva, inmediato: true);
    }

    private void Start()
    {
        NormalizarParametrosNitidos();
        if (canvasCamara != null) canvasCamara.sortingOrder = 30;
        if (usarPostProcessingRetro) ActualizarParametrosVolume();
        AsegurarPostProcessingCamara();
        ReajustarPosicionesHUD();
    }

    private void Update()
    {
        // 1. Alternar con tecla
        if (permitirAlternarConTecla && Keyboard.current != null)
        {
            var tecla = Keyboard.current[teclaAlternar];
            if (tecla != null && tecla.wasPressedThisFrame)
            {
                AlternarCamara();
            }
        }

        if (!camaraActiva) return;

        // 2. Parpadeo de REC
        temporizadorParpadeo += Time.deltaTime;
        if (temporizadorParpadeo >= intervaloParpadeoRec)
        {
            temporizadorParpadeo = 0f;
            recVisible = !recVisible;
            if (textoRec != null) textoRec.enabled = recVisible;
            if (iconoPuntoRec != null) iconoPuntoRec.enabled = recVisible;
        }

        // 3. Timecode avanzando (solo actualiza texto cuando cambia el frame a 30 FPS)
        tiempoGrabado += Time.deltaTime;
        int totalFrames = (int)(tiempoGrabado * 30f);
        if (totalFrames != ultimoFrameTimecode && textoTimecode != null)
        {
            ultimoFrameTimecode = totalFrames;
            int horas = (int)(tiempoGrabado / 3600f);
            int minutos = (int)((tiempoGrabado % 3600f) / 60f);
            int segundos = (int)(tiempoGrabado % 60f);
            int frames = totalFrames % 30;
            textoTimecode.text = string.Format("{0:00}:{1:00}:{2:00}:{3:00}", horas, minutos, segundos, frames);
        }

        // 4. Batería
        if (simularConsumoBateria && nivelBateriaActual > 1f)
        {
            nivelBateriaActual -= (Time.deltaTime / 45f);
            ActualizarTextoBateria();
        }

        // 5. Fecha (solo actualiza texto una vez por segundo)
        if (usarHoraReal && textoFecha != null)
        {
            DateTime ahora = DateTime.Now;
            if (ahora.Second != ultimoSegundoFecha)
            {
                ultimoSegundoFecha = ahora.Second;
                textoFecha.text = ahora.ToString("MMM dd yyyy  HH:mm:ss").ToUpper();
            }
        }

        // 6. Actualizar interferencias animadas (Ruido, Tracking, Jitter y Persecución)
        ActualizarInterferenciaPersecucion();
        ActualizarInterferencias();
    }

    public void EstablecerInterferenciaPersecucion(float intensidad01)
    {
        intensidadPersecucionObjetivo = Mathf.Clamp01(intensidad01);
    }

    public void EstablecerInterferenciaBase(float intensidad01)
    {
        intensidadBaseObjetivo = Mathf.Clamp01(intensidad01);
    }

    private void ActualizarInterferenciaPersecucion()
    {
        float objPers = camaraActiva ? intensidadPersecucionObjetivo : 0f;
        float objBase = camaraActiva ? intensidadBaseObjetivo : 0f;
        intensidadPersecucionActual = Mathf.MoveTowards(intensidadPersecucionActual, objPers, Time.deltaTime * 2.5f);
        intensidadBaseActual = Mathf.MoveTowards(intensidadBaseActual, objBase, Time.deltaTime * 1.5f);

        float p = Mathf.Clamp01(Mathf.Max(intensidadBaseActual, intensidadPersecucionActual));

        if (imagenRuidoEstatico != null)
        {
            float alphaRuido = Mathf.Lerp(opacidadRuido, 0.36f, p);
            imagenRuidoEstatico.color = new Color(1f, 1f, 1f, alphaRuido);
        }

        if (volumePostProcessing != null && volumePostProcessing.profile != null)
        {
            if (volumePostProcessing.profile.TryGet(out ChromaticAberration aberracion))
            {
                aberracion.intensity.overrideState = true;
                aberracion.intensity.value = Mathf.Lerp(intensidadAberracion, 0.78f, p);
            }
            if (volumePostProcessing.profile.TryGet(out FilmGrain grain))
            {
                grain.intensity.overrideState = true;
                grain.intensity.value = Mathf.Lerp(intensidadGrano, 0.85f, p);
            }
        }

        if (audioSourceInterferencia != null && sonidoGlitch != null)
        {
            if (p > 0.02f)
            {
                if (audioSourceInterferencia.clip != sonidoGlitch) audioSourceInterferencia.clip = sonidoGlitch;
                if (!audioSourceInterferencia.isPlaying) audioSourceInterferencia.Play();
                audioSourceInterferencia.volume = Mathf.Lerp(0.06f, 0.72f, p);
                audioSourceInterferencia.pitch = Mathf.Lerp(0.92f, 1.15f, p);
            }
            else if (audioSourceInterferencia.isPlaying)
            {
                audioSourceInterferencia.volume = 0f;
                audioSourceInterferencia.Stop();
            }
        }
    }

    private void ActualizarInterferencias()
    {
        // A) Ruido estático animado (24 FPS clásicos)
        if (activarRuidoEstatico && imagenRuidoEstatico != null && texturasRuido != null && texturasRuido.Length > 0)
        {
            temporizadorFrameRuido += Time.deltaTime;
            if (temporizadorFrameRuido >= 0.04f)
            {
                temporizadorFrameRuido = 0f;
                indiceTexturaRuido = (indiceTexturaRuido + 1) % texturasRuido.Length;
                imagenRuidoEstatico.texture = texturasRuido[indiceTexturaRuido];
                // Desfase aleatorio de UV para mayor sensación analógica
                imagenRuidoEstatico.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 1440f / 64f, 1080f / 64f);
            }
        }

        // B) Barra de Tracking VHS desplazándose verticalmente
        float nivelCombinado = Mathf.Clamp01(Mathf.Max(intensidadBaseActual, intensidadPersecucionActual));
        if (activarBarraTracking && rectBarraTracking != null && imagenBarraTracking != null)
        {
            float velTrack = Mathf.Lerp(velocidadTracking, 380f, nivelCombinado);
            posTrackingY += velTrack * Time.deltaTime;
            if (posTrackingY > 560f) posTrackingY = -560f;

            rectBarraTracking.anchoredPosition = new Vector2(0f, posTrackingY);
            float alphaTrackBase = Mathf.Lerp(opacidadTracking, 0.55f, nivelCombinado);
            float modAlpha = alphaTrackBase * UnityEngine.Random.Range(0.85f, 1.25f);
            imagenBarraTracking.color = new Color(1f, 1f, 1f, Mathf.Clamp01(modAlpha));
        }

        // C) Jitter horizontal de cinta (aumenta al ser perseguido o en nivel base elevado)
        if (activarJitterHorizontal && rectContenedor4x3 != null)
        {
            if (nivelCombinado > 0.25f)
            {
                float maxJitter = Mathf.Lerp(2f, 14f, nivelCombinado);
                rectContenedor4x3.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(-maxJitter, maxJitter),
                    UnityEngine.Random.Range(-maxJitter * 0.25f, maxJitter * 0.25f)
                );
                return;
            }

            temporizadorJitter += Time.deltaTime;
            if (duracionJitterActual > 0f)
            {
                duracionJitterActual -= Time.deltaTime;
                if (duracionJitterActual <= 0f)
                {
                    desplazamientoJitterX = 0f;
                    rectContenedor4x3.anchoredPosition = Vector2.zero;
                }
            }
            else if (temporizadorJitter >= UnityEngine.Random.Range(2.5f, 5.5f))
            {
                temporizadorJitter = 0f;
                duracionJitterActual = UnityEngine.Random.Range(0.04f, 0.09f);
                desplazamientoJitterX = UnityEngine.Random.Range(-4f, 4f);
                rectContenedor4x3.anchoredPosition = new Vector2(desplazamientoJitterX, 0f);
            }
        }
    }

    public void AlternarCamara()
    {
        ActivarModoCamara(!camaraActiva);
    }

    public void ActivarModoCamara(bool activar)
    {
        ActivarModoCamara(activar, inmediato: false);
    }

    public void ActivarModoCamara(bool activar, bool inmediato)
    {
        if (camaraActiva == activar && !inmediato) return;
        camaraActiva = activar;

        if (inmediato)
        {
            if (transicionRutina != null)
            {
                StopCoroutine(transicionRutina);
                transicionRutina = null;
            }
            AplicarEstadoVisibilidad(camaraActiva, inmediato: true);
            return;
        }

        if (audioSource != null)
        {
            AudioClip clip = camaraActiva ? sonidoEncender : sonidoApagar;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        if (transicionRutina != null) StopCoroutine(transicionRutina);
        transicionRutina = StartCoroutine(RutinaTransicion(camaraActiva));
    }

    private IEnumerator RutinaTransicion(bool encender)
    {
        float duracion = 0.22f;
        float tiempo = 0f;
        float inicioAlfa = canvasGroup != null ? canvasGroup.alpha : (encender ? 0f : 1f);
        float objetivoAlfa = encender ? 1f : 0f;

        float fovInicial = camaraPrincipal != null ? camaraPrincipal.fieldOfView : fovModoNormal;
        float fovObjetivo = encender ? fovModoCamara : fovModoNormal;

        if (encender && panelHUD != null) panelHUD.SetActive(true);

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);
            float alfa = Mathf.Lerp(inicioAlfa, objetivoAlfa, t);

            if (canvasGroup != null) canvasGroup.alpha = alfa;
            if (volumePostProcessing != null) volumePostProcessing.weight = alfa;

            if (aplicarZoomCamara && camaraPrincipal != null)
            {
                camaraPrincipal.fieldOfView = Mathf.Lerp(fovInicial, fovObjetivo, t);
            }

            yield return null;
        }

        AplicarEstadoVisibilidad(encender, inmediato: true);
        transicionRutina = null;
    }

    private void AplicarEstadoVisibilidad(bool activa, bool inmediato)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = activa ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (panelHUD != null)
        {
            panelHUD.SetActive(activa);
        }

        if (bandaIzquierda != null) bandaIzquierda.SetActive(activa && formatoCamara4x3 && !modoWidescreen);
        if (bandaDerecha != null) bandaDerecha.SetActive(activa && formatoCamara4x3 && !modoWidescreen);

        if (volumePostProcessing != null)
        {
            volumePostProcessing.weight = activa ? 1f : 0f;
        }

        AplicarZoom(activa, inmediato);

        if (activa)
        {
            ActualizarTextoBateria();
            if (textoFecha != null)
            {
                textoFecha.text = usarHoraReal ? DateTime.Now.ToString("MMM dd yyyy  HH:mm:ss").ToUpper() : fechaSimulada;
            }
            if (textoModo != null)
            {
                textoModo.text = modoGrabacion;
            }
        }
    }

    private void ActualizarTextoBateria()
    {
        if (textoBateria == null) return;
        int pct = Mathf.Clamp((int)nivelBateriaActual, 0, 100);
        if (pct == ultimoPorcentajeBateria) return;
        ultimoPorcentajeBateria = pct;

        string icono = "[■■■■]";
        if (pct < 20) icono = "[!   ]";
        else if (pct < 45) icono = "[■   ]";
        else if (pct < 70) icono = "[■■  ]";
        else if (pct < 90) icono = "[■■■ ]";

        textoBateria.text = $"{icono} {pct}%";
    }

    private Coroutine rutinaZoomFOV;

    private void AplicarZoom(bool activa, bool inmediato)
    {
        if (!aplicarZoomCamara || camaraPrincipal == null) return;
        float objetivo = activa ? fovModoCamara : fovModoNormal;
        if (inmediato)
        {
            camaraPrincipal.fieldOfView = objetivo;
        }
        else
        {
            if (rutinaZoomFOV != null) StopCoroutine(rutinaZoomFOV);
            rutinaZoomFOV = StartCoroutine(RutinaSuaveFOV(objetivo, 0.28f));
        }
    }

    private IEnumerator RutinaSuaveFOV(float targetFov, float duracion)
    {
        float fovInicial = camaraPrincipal.fieldOfView;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            camaraPrincipal.fieldOfView = Mathf.SmoothStep(fovInicial, targetFov, t / duracion);
            yield return null;
        }
        camaraPrincipal.fieldOfView = targetFov;
        rutinaZoomFOV = null;
    }

    public void DispararGlitch(float duracion = 0.35f)
    {
        StartCoroutine(RutinaGlitch(duracion));
    }

    private IEnumerator RutinaGlitch(float duracion)
    {
        if (sonidoGlitch != null && audioSource != null)
        {
            audioSource.PlayOneShot(sonidoGlitch);
        }

        ChromaticAberration aberracion = null;
        FilmGrain grano = null;

        if (volumePostProcessing != null && volumePostProcessing.profile != null)
        {
            volumePostProcessing.profile.TryGet(out aberracion);
            volumePostProcessing.profile.TryGet(out grano);
        }

        float aberracionOrig = aberracion != null ? aberracion.intensity.value : intensidadAberracion;
        float granoOrig = grano != null ? grano.intensity.value : intensidadGrano;

        if (aberracion != null) aberracion.intensity.value = 1f;
        if (grano != null) grano.intensity.value = 1f;

        float tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            if (rectContenedor4x3 != null)
            {
                rectContenedor4x3.anchoredPosition = new Vector2(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-4f, 4f));
            }
            if (imagenRuidoEstatico != null)
            {
                imagenRuidoEstatico.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacidadRuido * 2.2f));
            }
            yield return null;
        }

        if (rectContenedor4x3 != null) rectContenedor4x3.anchoredPosition = Vector2.zero;
        if (imagenRuidoEstatico != null) imagenRuidoEstatico.color = new Color(1f, 1f, 1f, opacidadRuido);

        if (aberracion != null) aberracion.intensity.value = aberracionOrig;
        if (grano != null) grano.intensity.value = granoOrig;
    }

    // =========================================================================
    // CREACIÓN Y PREPARACIÓN PROCEDURAL
    // =========================================================================

    public void AsignarVolume(Volume vol)
    {
        volumePostProcessing = vol;
    }

    private void AsegurarPostProcessingCamara()
    {
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;
        if (camaraPrincipal != null)
        {
            var camData = camaraPrincipal.GetUniversalAdditionalCameraData();
            if (camData != null && !camData.renderPostProcessing)
            {
                camData.renderPostProcessing = true;
            }
        }
    }

    private void AsegurarAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;

        if (audioSourceInterferencia == null)
        {
            audioSourceInterferencia = gameObject.AddComponent<AudioSource>();
        }
        audioSourceInterferencia.playOnAwake = false;
        audioSourceInterferencia.loop = true;
        audioSourceInterferencia.spatialBlend = 0f;
        audioSourceInterferencia.volume = 0f;

#if UNITY_EDITOR
        if (sonidoEncender == null)
        {
            sonidoEncender = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Camara/sonidoEncender.mp3");
        }
        if (sonidoApagar == null)
        {
            sonidoApagar = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Camara/sonidoApagar.mp3");
        }
        if (sonidoGlitch == null)
        {
            sonidoGlitch = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Camara/sonidoGlitch.mp3");
        }
#endif
    }

    /// <summary>
    /// Normaliza los parámetros visuales a rangos limpios y nítidos tipo cámara de seguridad/bodycam,
    /// evitando neblina gris opaca o desenfoques excesivos.
    /// </summary>
    public void NormalizarParametrosNitidos()
    {
        if (opacidadRuido > 0.05f) opacidadRuido = 0.035f;
        if (opacidadScanlines > 0.08f) opacidadScanlines = 0.04f;
        if (opacidadTracking > 0.15f) opacidadTracking = 0.10f;
        if (opacidadVinetaUI > 0.30f) opacidadVinetaUI = 0.22f;
        if (intensidadGrano > 0.30f) intensidadGrano = 0.18f;
        if (intensidadAberracion > 0.15f) intensidadAberracion = 0.08f;
        if (Mathf.Abs(curvaturaLente) > 0.05f) curvaturaLente = -0.02f;
        if (intensidadVinetaVolume > 0.30f) intensidadVinetaVolume = 0.22f;

        if (imagenRuidoEstatico != null)
        {
            imagenRuidoEstatico.color = new Color(1f, 1f, 1f, opacidadRuido);
        }
        if (imagenScanlines != null)
        {
            imagenScanlines.color = new Color(1f, 1f, 1f, opacidadScanlines);
        }
        if (imagenVineta != null)
        {
            imagenVineta.color = new Color(0f, 0f, 0f, opacidadVinetaUI);
        }
    }

    /// <summary>
    /// Sincroniza los parámetros de nitidez y aberración suave directamente en el Volume activo de URP.
    /// </summary>
    public void ActualizarParametrosVolume()
    {
        if (volumePostProcessing == null || volumePostProcessing.profile == null) return;

        if (volumePostProcessing.profile.TryGet(out FilmGrain grain))
        {
            grain.intensity.overrideState = true;
            grain.intensity.value = intensidadGrano;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Thin1;
        }

        if (volumePostProcessing.profile.TryGet(out ChromaticAberration chrom))
        {
            chrom.intensity.overrideState = true;
            chrom.intensity.value = intensidadAberracion;
        }

        if (volumePostProcessing.profile.TryGet(out LensDistortion lens))
        {
            lens.intensity.overrideState = true;
            lens.intensity.value = curvaturaLente;
        }

        if (volumePostProcessing.profile.TryGet(out Vignette vig))
        {
            vig.intensity.overrideState = true;
            vig.intensity.value = intensidadVinetaVolume;
        }

        if (volumePostProcessing.profile.TryGet(out ColorAdjustments color))
        {
            color.saturation.overrideState = true;
            color.saturation.value = -8f;
            color.contrast.overrideState = true;
            color.contrast.value = 8f;
        }
    }

    private void AsegurarVolumePostProcessing()
    {
        if (volumePostProcessing == null)
        {
            volumePostProcessing = GetComponentInChildren<Volume>();
            if (volumePostProcessing == null)
            {
                GameObject volObj = new GameObject("Volume_CamaraRetro");
                volObj.transform.SetParent(transform, false);

                volumePostProcessing = volObj.AddComponent<Volume>();
                volumePostProcessing.isGlobal = true;
                volumePostProcessing.priority = 100f;
            }
        }

        if (volumePostProcessing.profile == null)
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PerfilCamaraRetro_Procedural";

            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.overrideState = true;
            grain.intensity.value = intensidadGrano;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Thin1;

            var chrom = profile.Add<ChromaticAberration>(true);
            chrom.intensity.overrideState = true;
            chrom.intensity.value = intensidadAberracion;

            var lens = profile.Add<LensDistortion>(true);
            lens.intensity.overrideState = true;
            lens.intensity.value = curvaturaLente;

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true;
            vig.intensity.value = intensidadVinetaVolume;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.45f;

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.overrideState = true;
            color.saturation.value = -8f;
            color.contrast.overrideState = true;
            color.contrast.value = 8f;

            volumePostProcessing.profile = profile;
        }

        ActualizarParametrosVolume();
        volumePostProcessing.weight = camaraActiva ? 1f : 0f;
    }

    private void AsegurarTexturasProcedurales()
    {
        if (texturaScanlines == null) texturaScanlines = GenerarTexturaScanlines();
        if (texturaVineta == null) texturaVineta = GenerarTexturaVineta();
        if (texturaTracking == null) texturaTracking = GenerarTexturaTracking();

        if (texturasRuido == null || texturasRuido.Length != 4)
        {
            texturasRuido = new Texture2D[4];
            for (int i = 0; i < texturasRuido.Length; i++)
            {
                texturasRuido[i] = GenerarTexturaRuido(64);
            }
        }
    }

    private Texture2D GenerarTexturaRuido(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color32[] pixeles = new Color32[size * size];
        for (int i = 0; i < pixeles.Length; i++)
        {
            byte val = (byte)UnityEngine.Random.Range(20, 240);
            pixeles[i] = new Color32(val, val, val, 255);
        }
        tex.SetPixels32(pixeles);
        tex.Apply(false, true);
        return tex;
    }

    private Texture2D GenerarTexturaTracking()
    {
        // Barra con ruido y líneas horizontales de fallo de cabezal VHS
        int width = 256;
        int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixeles = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float distCentroY = 1f - Mathf.Abs((y / (float)(height - 1)) - 0.5f) * 2f;
            for (int x = 0; x < width; x++)
            {
                float ruido = UnityEngine.Random.value;
                float alfa = distCentroY * (ruido > 0.35f ? 0.9f : 0.1f);
                pixeles[y * width + x] = new Color(ruido, ruido, ruido, alfa);
            }
        }
        tex.SetPixels(pixeles);
        tex.Apply(false, true);
        return tex;
    }

    private Texture2D GenerarTexturaScanlines()
    {
        Texture2D tex = new Texture2D(2, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color lineaOscura = new Color(0f, 0f, 0f, 0.85f);
        Color lineaClara = new Color(0f, 0f, 0f, 0.05f);

        tex.SetPixel(0, 0, lineaOscura); tex.SetPixel(1, 0, lineaOscura);
        tex.SetPixel(0, 1, lineaOscura); tex.SetPixel(1, 1, lineaOscura);
        tex.SetPixel(0, 2, lineaClara);  tex.SetPixel(1, 2, lineaClara);
        tex.SetPixel(0, 3, lineaClara);  tex.SetPixel(1, 3, lineaClara);

        tex.Apply(false, true);
        return tex;
    }

    private Texture2D GenerarTexturaVineta()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixeles = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x / (float)(size - 1)) - 0.5f;
                float v = (y / (float)(size - 1)) - 0.5f;
                float dist = new Vector2(u * 1.55f, v * 1.55f).magnitude;
                float alfa = Mathf.SmoothStep(0.25f, 0.90f, dist);
                pixeles[y * size + x] = new Color(0f, 0f, 0f, alfa);
            }
        }
        tex.SetPixels(pixeles);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>
    /// Construye toda la jerarquía de UI del Canvas con formato 4:3 e interferencias VHS.
    /// </summary>
    public void ConstruirUIProcedural()
    {
        AsegurarTexturasProcedurales();

        // 1. Configurar componentes Canvas en este GameObject
        canvasCamara = GetComponent<Canvas>();
        if (canvasCamara == null) canvasCamara = gameObject.AddComponent<Canvas>();
        canvasCamara.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasCamara.sortingOrder = 30; // Nivel base de cámara: HUD de juego y diálogos se dibujan encima (150)

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // Limpiar elementos hijos previos
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "Volume_CamaraRetro") continue; // Preservar Volume si existe
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        Font fuente = fuentePersonalizada != null 
            ? fuentePersonalizada 
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 2. Contenedor Maestro HUD (1920x1080)
        panelHUD = new GameObject("Panel_HUD");
        panelHUD.transform.SetParent(transform, false);
        RectTransform rtPanel = panelHUD.AddComponent<RectTransform>();
        EstirarRect(rtPanel);

        // 3. Bandas Negras Laterales (Pillarboxing 4:3)
        // En 1920x1080, el área 4:3 central tiene 1440 de ancho. Cada banda mide (1920-1440)/2 = 240px.
        float anchoBanda = 240f;

        bandaIzquierda = new GameObject("Banda_Negra_Izquierda");
        bandaIzquierda.transform.SetParent(panelHUD.transform, false);
        RectTransform rtBandaIzq = bandaIzquierda.AddComponent<RectTransform>();
        rtBandaIzq.anchorMin = new Vector2(0f, 0f);
        rtBandaIzq.anchorMax = new Vector2(0f, 1f);
        rtBandaIzq.pivot = new Vector2(0f, 0.5f);
        rtBandaIzq.sizeDelta = new Vector2(anchoBanda, 0f);
        rtBandaIzq.anchoredPosition = Vector2.zero;
        Image imgBandaIzq = bandaIzquierda.AddComponent<Image>();
        imgBandaIzq.color = colorBandasLaterales;
        imgBandaIzq.raycastTarget = false;

        bandaDerecha = new GameObject("Banda_Negra_Derecha");
        bandaDerecha.transform.SetParent(panelHUD.transform, false);
        RectTransform rtBandaDer = bandaDerecha.AddComponent<RectTransform>();
        rtBandaDer.anchorMin = new Vector2(1f, 0f);
        rtBandaDer.anchorMax = new Vector2(1f, 1f);
        rtBandaDer.pivot = new Vector2(1f, 0.5f);
        rtBandaDer.sizeDelta = new Vector2(anchoBanda, 0f);
        rtBandaDer.anchoredPosition = Vector2.zero;
        Image imgBandaDer = bandaDerecha.AddComponent<Image>();
        imgBandaDer.color = colorBandasLaterales;
        imgBandaDer.raycastTarget = false;

        // 4. Contenedor Maestro de Vista (1920x1080 Widescreen o 1440x1080 4:3)
        GameObject visor4x3Obj = new GameObject("Visor_Camara_Principal");
        visor4x3Obj.transform.SetParent(panelHUD.transform, false);
        rectContenedor4x3 = visor4x3Obj.AddComponent<RectTransform>();
        rectContenedor4x3.anchorMin = new Vector2(0.5f, 0.5f);
        rectContenedor4x3.anchorMax = new Vector2(0.5f, 0.5f);
        rectContenedor4x3.pivot = new Vector2(0.5f, 0.5f);
        rectContenedor4x3.sizeDelta = modoWidescreen ? new Vector2(1920f, 1080f) : new Vector2(1440f, 1080f);

        // 5. Tinte sutil analógico
        GameObject tinteObj = new GameObject("Tinte_Visor");
        tinteObj.transform.SetParent(rectContenedor4x3, false);
        RectTransform rtTinte = tinteObj.AddComponent<RectTransform>();
        EstirarRect(rtTinte);
        Image imgTinte = tinteObj.AddComponent<Image>();
        imgTinte.color = tinteVisor;
        imgTinte.raycastTarget = false;

        // 6. Scanlines CRT (Ligeras y nítidas)
        if (mostrarScanlines)
        {
            GameObject scanObj = new GameObject("Scanlines_CRT");
            scanObj.transform.SetParent(rectContenedor4x3, false);
            RectTransform rtScan = scanObj.AddComponent<RectTransform>();
            EstirarRect(rtScan);

            imagenScanlines = scanObj.AddComponent<RawImage>();
            imagenScanlines.texture = texturaScanlines;
            float anchoVisor = modoWidescreen ? 1920f : 1440f;
            imagenScanlines.uvRect = new Rect(0, 0, anchoVisor / 4f, 1080f / 4f);
            imagenScanlines.color = new Color(1f, 1f, 1f, opacidadScanlines);
            imagenScanlines.raycastTarget = false;
        }

        // 7. Ruido Blanco Estático Animado (Interferencia viva)
        if (activarRuidoEstatico)
        {
            GameObject ruidoObj = new GameObject("Ruido_Estatico_VHS");
            ruidoObj.transform.SetParent(rectContenedor4x3, false);
            RectTransform rtRuido = ruidoObj.AddComponent<RectTransform>();
            EstirarRect(rtRuido);

            imagenRuidoEstatico = ruidoObj.AddComponent<RawImage>();
            imagenRuidoEstatico.texture = texturasRuido[0];
            imagenRuidoEstatico.uvRect = new Rect(0, 0, 1440f / 64f, 1080f / 64f);
            imagenRuidoEstatico.color = new Color(1f, 1f, 1f, opacidadRuido);
            imagenRuidoEstatico.raycastTarget = false;
        }

        // 8. Barra de Tracking VHS (Línea de cinta analógica que sube)
        if (activarBarraTracking)
        {
            GameObject trackObj = new GameObject("Barra_Tracking_VHS");
            trackObj.transform.SetParent(rectContenedor4x3, false);
            rectBarraTracking = trackObj.AddComponent<RectTransform>();
            rectBarraTracking.anchorMin = new Vector2(0f, 0.5f);
            rectBarraTracking.anchorMax = new Vector2(1f, 0.5f);
            rectBarraTracking.pivot = new Vector2(0.5f, 0.5f);
            rectBarraTracking.sizeDelta = new Vector2(0f, 50f);
            rectBarraTracking.anchoredPosition = new Vector2(0f, posTrackingY);

            imagenBarraTracking = trackObj.AddComponent<RawImage>();
            imagenBarraTracking.texture = texturaTracking;
            imagenBarraTracking.uvRect = new Rect(0, 0, 6f, 1f);
            imagenBarraTracking.color = new Color(1f, 1f, 1f, opacidadTracking);
            imagenBarraTracking.raycastTarget = false;
        }

        // 9. Viñeta oscura
        if (mostrarVinetaUI)
        {
            GameObject vinObj = new GameObject("Viñeta_Visor");
            vinObj.transform.SetParent(rectContenedor4x3, false);
            RectTransform rtVin = vinObj.AddComponent<RectTransform>();
            EstirarRect(rtVin);

            imagenVineta = vinObj.AddComponent<RawImage>();
            imagenVineta.texture = texturaVineta;
            imagenVineta.color = new Color(0f, 0f, 0f, opacidadVinetaUI);
            imagenVineta.raycastTarget = false;
        }

        // 10. Esquinas de encuadre (Mucho más grandes y notorias: 90x90 con 5px de grosor)
        if (mostrarEsquinas)
        {
            objetoEsquinas = new GameObject("Esquinas_Encuadre");
            objetoEsquinas.transform.SetParent(rectContenedor4x3, false);
            RectTransform rtEsq = objetoEsquinas.AddComponent<RectTransform>();
            EstirarRect(rtEsq);

            CrearEsquina(objetoEsquinas.transform, "Esq_SuperiorIzquierda", new Vector2(0.04f, 0.94f), new Vector2(1, -1));
            CrearEsquina(objetoEsquinas.transform, "Esq_SuperiorDerecha", new Vector2(0.96f, 0.94f), new Vector2(-1, -1));
            CrearEsquina(objetoEsquinas.transform, "Esq_InferiorIzquierda", new Vector2(0.04f, 0.06f), new Vector2(1, 1));
            CrearEsquina(objetoEsquinas.transform, "Esq_InferiorDerecha", new Vector2(0.96f, 0.06f), new Vector2(-1, 1));
        }

        // 11. Visor Central de Enfoque [ + ]
        if (mostrarVisorCentral)
        {
            objetoVisor = new GameObject("Visor_Central");
            objetoVisor.transform.SetParent(rectContenedor4x3, false);
            RectTransform rtVisor = objetoVisor.AddComponent<RectTransform>();
            rtVisor.anchorMin = rtVisor.anchorMax = rtVisor.pivot = new Vector2(0.5f, 0.5f);
            rtVisor.sizeDelta = new Vector2(60, 60);

            Color colVisor = new Color(1f, 1f, 1f, 0.65f);
            // Cruz central
            CrearBarraColor(objetoVisor.transform, "Cruz_H", new Vector2(0, 0), new Vector2(36, 3), colVisor);
            CrearBarraColor(objetoVisor.transform, "Cruz_V", new Vector2(0, 0), new Vector2(3, 36), colVisor);

            // Corchetes de autofoco alrededor de la cruz
            CrearBarraColor(objetoVisor.transform, "Corchete_Izq_V", new Vector2(-28, 0), new Vector2(3, 24), colVisor);
            CrearBarraColor(objetoVisor.transform, "Corchete_Izq_H1", new Vector2(-24, 12), new Vector2(8, 3), colVisor);
            CrearBarraColor(objetoVisor.transform, "Corchete_Izq_H2", new Vector2(-24, -12), new Vector2(8, 3), colVisor);

            CrearBarraColor(objetoVisor.transform, "Corchete_Der_V", new Vector2(28, 0), new Vector2(3, 24), colVisor);
            CrearBarraColor(objetoVisor.transform, "Corchete_Der_H1", new Vector2(24, 12), new Vector2(8, 3), colVisor);
            CrearBarraColor(objetoVisor.transform, "Corchete_Der_H2", new Vector2(24, -12), new Vector2(8, 3), colVisor);
        }

        // 12. TEXTOS OSD RETRO (Ubicados limpiamente para no colisionar con Cordura ni Tareas)
        // A) REC y Modo de Cámara: Ubicado justo debajo del panel de Cordura en la izquierda
        GameObject contRec = new GameObject("Contenedor_REC");
        contRec.transform.SetParent(rectContenedor4x3, false);
        RectTransform rtContRec = contRec.AddComponent<RectTransform>();
        rtContRec.anchorMin = rtContRec.anchorMax = rtContRec.pivot = new Vector2(0f, 1f);
        rtContRec.anchoredPosition = new Vector2(40f, -125f);
        rtContRec.sizeDelta = new Vector2(300f, 65f);

        // Punto rojo REC (más estilizado)
        GameObject puntoObj = new GameObject("Punto_Rojo_REC");
        puntoObj.transform.SetParent(contRec.transform, false);
        RectTransform rtPunto = puntoObj.AddComponent<RectTransform>();
        rtPunto.anchorMin = rtPunto.anchorMax = rtPunto.pivot = new Vector2(0f, 1f);
        rtPunto.anchoredPosition = new Vector2(0, -6);
        rtPunto.sizeDelta = new Vector2(18, 18);
        iconoPuntoRec = puntoObj.AddComponent<Image>();
        iconoPuntoRec.color = colorRec;
        iconoPuntoRec.raycastTarget = false;

        // Texto "REC" (Tamaño 38, Bold)
        textoRec = CrearTexto(contRec.transform, "Texto_REC", "REC", fuente, 38, colorRec, TextAnchor.UpperLeft);
        PosicionarRect(textoRec.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, 0), new Vector2(120, 45));

        // Texto Modo Cinta "CAM-01  [BODYCAM]" (Tamaño 22)
        textoModo = CrearTexto(contRec.transform, "Texto_Modo", "CAM-01  [BODYCAM]", fuente, 22, colorTextos, TextAnchor.UpperLeft);
        PosicionarRect(textoModo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2, -40), new Vector2(280, 35));

        // B) Inferior Izquierda: Fecha y hora (Tamaño 30, dentro del margen sin tapar subtítulos)
        textoFecha = CrearTexto(rectContenedor4x3, "Texto_Fecha", fechaSimulada, fuente, 30, colorTextos, TextAnchor.LowerLeft);
        PosicionarRect(textoFecha.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(45f, 35f), new Vector2(380, 45));

        // C) Inferior Derecha (Arriba del Timecode): Batería (Despeja la esquina superior derecha para las tareas)
        textoBateria = CrearTexto(rectContenedor4x3, "Texto_Bateria", "[■■■ ] 88%", fuente, 28, colorTextos, TextAnchor.LowerRight);
        PosicionarRect(textoBateria.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 75f), new Vector2(260, 40));

        // D) Inferior Derecha: Timecode (Tamaño 32, Bold)
        textoTimecode = CrearTexto(rectContenedor4x3, "Texto_Timecode", "00:00:00:00", fuente, 32, colorTextos, TextAnchor.LowerRight);
        PosicionarRect(textoTimecode.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 35f), new Vector2(260, 45));
    }

    /// <summary>
    /// Reajusta las posiciones del HUD de la videocámara para no solapar con la barra de Cordura ni con las Tareas.
    /// </summary>
    public void ReajustarPosicionesHUD()
    {
        if (rectContenedor4x3 == null) return;

        rectContenedor4x3.sizeDelta = modoWidescreen ? new Vector2(1920f, 1080f) : new Vector2(1440f, 1080f);
        if (bandaIzquierda != null) bandaIzquierda.SetActive(camaraActiva && formatoCamara4x3 && !modoWidescreen);
        if (bandaDerecha != null) bandaDerecha.SetActive(camaraActiva && formatoCamara4x3 && !modoWidescreen);

        var contRec = rectContenedor4x3.Find("Contenedor_REC");
        if (contRec != null)
        {
            var rt = contRec.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(40f, -125f);
        }

        if (textoBateria != null)
        {
            var rt = textoBateria.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-45f, 75f);
            textoBateria.alignment = TextAnchor.LowerRight;
            textoBateria.fontSize = 28;
        }

        if (textoFecha != null)
        {
            var rt = textoFecha.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(45f, 35f);
            textoFecha.alignment = TextAnchor.LowerLeft;
            textoFecha.fontSize = 30;
        }

        if (textoTimecode != null)
        {
            var rt = textoTimecode.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-45f, 35f);
            textoTimecode.alignment = TextAnchor.LowerRight;
            textoTimecode.fontSize = 32;
        }
    }

    private void CrearEsquina(Transform padre, string nombre, Vector2 anclaje, Vector2 escala)
    {
        GameObject esqObj = new GameObject(nombre);
        esqObj.transform.SetParent(padre, false);
        RectTransform rt = esqObj.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anclaje;
        rt.sizeDelta = new Vector2(90, 90);

        Color c = new Color(1f, 1f, 1f, 0.75f);
        // Barra horizontal de 80px x 5px
        CrearBarraColor(esqObj.transform, "H", new Vector2(40f * escala.x, 0), new Vector2(80, 5), c);
        // Barra vertical de 5px x 80px
        CrearBarraColor(esqObj.transform, "V", new Vector2(0, 40f * escala.y), new Vector2(5, 80), c);
    }

    private void CrearBarraColor(Transform padre, string nombre, Vector2 pos, Vector2 tamano, Color color)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        // Sombra para contraste
        Shadow sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sombra.effectDistance = new Vector2(2f, -2f);
    }

    private Text CrearTexto(Transform padre, string nombre, string contenido, Font fuente, int tamano, Color color, TextAnchor alineacion)
    {
        GameObject txtObj = new GameObject(nombre);
        txtObj.transform.SetParent(padre, false);
        RectTransform rt = txtObj.AddComponent<RectTransform>();

        Text txt = txtObj.AddComponent<Text>();
        txt.text = contenido;
        txt.font = fuente;
        txt.fontSize = tamano;
        txt.fontStyle = FontStyle.Bold;
        txt.color = color;
        txt.alignment = alineacion;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // Contorno / Borde negro grueso tipo OSD retro
        Outline contorno = txtObj.AddComponent<Outline>();
        contorno.effectColor = new Color(0f, 0f, 0f, 0.95f);
        contorno.effectDistance = new Vector2(2.5f, -2.5f);

        Shadow sombra = txtObj.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sombra.effectDistance = new Vector2(3f, -3f);

        return txt;
    }

    private static void EstirarRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void PosicionarRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private void OnDestroy()
    {
        if (texturaScanlines != null) Destroy(texturaScanlines);
        if (texturaVineta != null) Destroy(texturaVineta);
        if (texturaTracking != null) Destroy(texturaTracking);

        if (texturasRuido != null)
        {
            for (int i = 0; i < texturasRuido.Length; i++)
            {
                if (texturasRuido[i] != null) Destroy(texturasRuido[i]);
            }
        }
    }
}
