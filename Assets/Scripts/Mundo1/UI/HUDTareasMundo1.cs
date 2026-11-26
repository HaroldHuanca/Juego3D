using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mundo1.UI
{
    /// <summary>
    /// Gestiona la visualización y audio del HUD de Mundo 1:
    /// 1. Lista de Tareas nocturnas (Superior Derecha) con tachado real [✔] y sonido de misión cumplida.
    /// 2. Barra de Cordura retro de 10 bloques LED (10% cada uno) con línea amarilla iluminada.
    /// 3. Subtítulos y diálogos con mecanografía letra por letra, soporte de Rich Text y blips de voz.
    /// 4. Interfaz y fuentes con tamaños y posiciones 100% públicos y configurables en Inspector.
    /// 5. Reproductor de música de fondo ambiental y efectos integrados.
    /// </summary>
    [DisallowMultipleComponent]
    public class HUDTareasMundo1 : MonoBehaviour
    {
        public static HUDTareasMundo1 Instance { get; private set; }

        #region Inspector: Tareas
        [Header("--- ELEMENTOS DE TAREAS (Superior Derecho) ---")]
        [SerializeField] private GameObject panelTareas;
        [SerializeField] private Text textoTituloTareas;
        [SerializeField] private Text textoListaTareas;
        #endregion

        #region Inspector: Cordura Segmentada (10% por barra)
        [Header("--- BARRA DE CORDURA (10% por segmento) ---")]
        [SerializeField] private GameObject panelCordura;
        [SerializeField] private Text textoPorcentajeCordura;

        [Tooltip("Número de bloques de la barra de cordura (10 para que cada barra represente exactamente 10%).")]
        [Range(5, 20)] public int cantidadSegmentos = 10;

        [Tooltip("Sprite personalizado opcional para segmentos activos.")]
        [SerializeField] private Sprite spriteSegmentoPersonalizado;

        [Tooltip("Sprite personalizado opcional para segmentos vacíos / descargados.")]
        [SerializeField] private Sprite spriteVacioPersonalizado;
        #endregion

        #region Inspector: Subtítulos y Diálogo
        [Header("--- SUBTÍTULOS Y DIÁLOGOS (Inferior Centro) ---")]
        [SerializeField] private GameObject panelSubtitulos;
        [SerializeField] private Text textoSubtitulos;
        #endregion

        #region Inspector: Tamaños de Fuente y Dimensiones de la Interfaz (PÚBLICOS)
        [Header("--- TAMAÑOS DE FUENTE (Modifica libremente en Inspector) ---")]
        [Tooltip("Tamaño de letra del título de tareas ('✦ TURNO NOCTURNO: TAREAS').")]
        [Range(14, 40)] public int tamanoFuenteTituloTareas = 24;

        [Tooltip("Tamaño de letra de las tareas en la lista.")]
        [Range(12, 36)] public int tamanoFuenteListaTareas = 21;

        [Tooltip("Tamaño de letra del porcentaje de cordura ('✦ CORDURA: 100%').")]
        [Range(14, 38)] public int tamanoFuenteCordura = 22;

        [Tooltip("Tamaño de letra de los diálogos y subtítulos.")]
        [Range(16, 48)] public int tamanoFuenteSubtitulos = 26;

        [Header("--- POSICIONES Y TAMAÑOS DE PANELES ---")]
        [Tooltip("Posición en pantalla del panel de Cordura (X e Y respecto a superior-izquierda).")]
        public Vector2 posicionPanelCordura = new Vector2(35f, -35f);

        [Tooltip("Dimensiones del panel de Cordura (Ancho y Alto).")]
        public Vector2 tamanoPanelCordura = new Vector2(390f, 85f);

        [Tooltip("Posición en pantalla del panel de Tareas (X e Y respecto a superior-derecha).")]
        public Vector2 posicionPanelTareas = new Vector2(-35f, -35f);

        [Tooltip("Dimensiones del panel de Tareas (Ancho y Alto).")]
        public Vector2 tamanoPanelTareas = new Vector2(450f, 260f);

        [Tooltip("Posición en pantalla del panel de Subtítulos (X e Y respecto a inferior-centro).")]
        public Vector2 posicionPanelSubtitulos = new Vector2(0f, 65f);

        [Tooltip("Dimensiones del panel de Subtítulos (Ancho y Alto).")]
        public Vector2 tamanoPanelSubtitulos = new Vector2(960f, 105f);
        #endregion

        #region Inspector: Audio y Música
        [Header("--- AUDIO: TAREAS Y NOTIFICACIONES ---")]
        [Tooltip("Sonido de tarea completada (chime de misión cumplida). Si está vacío, genera un chime armónico procedural.")]
        [SerializeField] private AudioClip sonidoTareaCompletada;

        [Tooltip("Sonido al registrar una nueva tarea. Si está vacío, genera un click procedural.")]
        [SerializeField] private AudioClip sonidoNuevaTarea;

        [Range(0f, 1f)] public float volumenSFX = 0.85f;

        [Header("--- AUDIO: MÚSICA DE FONDO / AMBIENTE ---")]
        [Tooltip("Pista de música ambiental de fondo.")]
        [SerializeField] private AudioClip musicaFondo;
        [Range(0f, 1f)] public float volumenMusica = 0.40f;
        public bool reproducirMusicaAlIniciar = true;

        [Header("--- AUDIO: SIMULACIÓN DE HABLA (BLIPS / BITS DE VOZ) ---")]
        [Tooltip("Sonido corto tipo 'blip / bit' para simular voz durante los subtítulos. Si está vacío, genera blips analógicos cálidos automáticamente.")]
        [SerializeField] private AudioClip sonidoHablaBlip;
        [Range(0f, 1f)] public float volumenBlipHabla = 0.55f;

        [Tooltip("¿Activar aparición letra por letra con sonido tipo máquina de escribir / retro RPG?")]
        public bool usarEfectoMecanografia = true;

        [Tooltip("Velocidad de aparición de caracteres en segundos.")]
        [Range(0.005f, 0.08f)] public float velocidadMecanografia = 0.022f;

        [Tooltip("Frecuencia: emite un sonido de voz cada N caracteres.")]
        [Range(1, 4)] public int cadaCuantosCaracteresSuenaBlip = 2;

        [Tooltip("Tono base de la voz simulada.")]
        [Range(0.6f, 1.6f)] public float tonoBaseHabla = 1.0f;

        [Tooltip("Variación aleatoria sutil de tono para dar vida a la voz.")]
        [Range(0f, 0.25f)] public float variacionTonoHabla = 0.08f;

        [Header("--- AUDIO: ALERTA DE CORDURA ---")]
        [Tooltip("Sonido de latido o tensión cuando la cordura cae a nivel crítico.")]
        [SerializeField] private AudioClip sonidoAlertaCordura;
        #endregion

        #region Inspector: Colores
        [Header("--- COLORES DE ESTADO ---")]
        [SerializeField] private Color colorNormal = new Color(0.20f, 0.75f, 0.38f, 1f);  // Verde esmeralda vivo
        [SerializeField] private Color colorAlerta = new Color(0.96f, 0.65f, 0.12f, 1f);  // Ámbar / naranja
        [SerializeField] private Color colorCritico = new Color(0.92f, 0.22f, 0.22f, 1f); // Rojo crítico
        #endregion

        // Estructura interna de datos
        private class ItemTarea
        {
            public string id;
            public string descripcion;
            public bool completada;
        }

        private readonly List<ItemTarea> tareasActivas = new List<ItemTarea>();
        private readonly List<Image> listaSegmentosCordura = new List<Image>();

        // Sprites procedurales de cordura con franja central amarilla iluminada
        private Sprite spriteVerdeProcedural;
        private Sprite spriteAmbarProcedural;
        private Sprite spriteRojoProcedural;
        private Sprite spriteVacioProcedural;

        // Clips procedurales generados dinámicamente si el usuario no asigna audios
        private AudioClip clipChimeProcedural;
        private AudioClip clipNuevaTareaProcedural;
        private AudioClip clipBlipProcedural;

        // Canales de audio
        private AudioSource audioSourceSFX;
        private AudioSource audioSourceMusica;
        private AudioSource audioSourceHabla;

        private Coroutine rutinaSubtitulo;
        private Coroutine rutinaFadeMusica;
        private Font fuenteUI;
        private bool saltarMecanografia = false;
        private float temporizadorPulsoCritico = 0f;
        private PlayerMovement jugadorEstamina;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Asegurar que el AudioListener esté activo en la escena
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            foreach (var l in listeners)
            {
                if (!l.enabled) l.enabled = true;
            }

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            AutoCargarAudiosSiFaltan();
            AsegurarAudioSources();
            GenerarAssetsProcedurales();

            // Construir UI si falta algún panel o si la cantidad de segmentos no coincide
            if (panelTareas == null || panelCordura == null || panelSubtitulos == null || listaSegmentosCordura.Count != cantidadSegmentos)
            {
                ConstruirUIProcedural();
            }

            AsegurarSortingOrderCanvas();
            AplicarEstiloYPosiciones();
        }

        private void AutoCargarAudiosSiFaltan()
        {
#if UNITY_EDITOR
            if (musicaFondo == null)
            {
                musicaFondo = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambiente/ambientehorror.mp3");
            }
            if (sonidoTareaCompletada == null)
            {
                sonidoTareaCompletada = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/sonidoTareaCompletada.mp3");
            }
            if (sonidoNuevaTarea == null)
            {
                sonidoNuevaTarea = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/sonidoNuevaTarea.mp3");
            }
#endif
        }

        private void Start()
        {
            AsegurarSortingOrderCanvas();
            AplicarEstiloYPosiciones();

            if (reproducirMusicaAlIniciar && musicaFondo != null)
            {
                ReproducirMusica(musicaFondo);
            }

            jugadorEstamina = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (jugadorEstamina != null)
            {
                jugadorEstamina.OnEstaminaCambiada += ActualizarCorduraUI;
                ActualizarCorduraUI(jugadorEstamina.EstaminaActual, jugadorEstamina.EstaminaMaxima);
            }
            else
            {
                ActualizarCorduraUI(100f, 100f);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                AplicarEstiloYPosiciones();
                AsegurarSegmentosExistentes();
                if (jugadorEstamina != null)
                {
                    ActualizarCorduraUI(jugadorEstamina.EstaminaActual, jugadorEstamina.EstaminaMaxima);
                }
            }
        }

        private void Update()
        {
            // Permitir adelantar la mecanografía presionando Espacio, E o Clic
            if (rutinaSubtitulo != null && usarEfectoMecanografia)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                {
                    saltarMecanografia = true;
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    saltarMecanografia = true;
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
                {
                    saltarMecanografia = true;
                }
#endif
            }

            // Pulso sutil de advertencia en el último bloque si la estamina es crítica o agotada
            if (jugadorEstamina != null && (jugadorEstamina.EstaminaAgotada || jugadorEstamina.PorcentajeEstamina <= 0.20f) && listaSegmentosCordura.Count > 0)
            {
                temporizadorPulsoCritico += Time.deltaTime * 6f;
                float alfa = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(temporizadorPulsoCritico) + 1f) * 0.5f);
                if (listaSegmentosCordura[0] != null)
                {
                    listaSegmentosCordura[0].color = new Color(1f, 1f, 1f, alfa);
                }
            }
        }

        private void OnDestroy()
        {
            if (jugadorEstamina != null)
            {
                jugadorEstamina.OnEstaminaCambiada -= ActualizarCorduraUI;
            }
        }

        #region Gestión de Tareas (Con Tachado Real)

        public void EstablecerTarea(string id, string descripcion, bool completada = false)
        {
            var existente = tareasActivas.Find(t => t.id == id);
            if (existente != null)
            {
                existente.descripcion = descripcion;
                existente.completada = completada;
            }
            else
            {
                tareasActivas.Add(new ItemTarea { id = id, descripcion = descripcion, completada = completada });
                ReproducirSonidoNuevaTarea();
            }

            RefrescarTextoTareas();
        }

        public void MarcarTareaCompletada(string id)
        {
            var tarea = tareasActivas.Find(t => t.id == id);
            if (tarea != null && !tarea.completada)
            {
                tarea.completada = true;
                RefrescarTextoTareas();
                ReproducirSonidoTareaCompletada();
            }
        }

        public void LimpiarTodasLasTareas()
        {
            tareasActivas.Clear();
            RefrescarTextoTareas();
        }

        /// <summary>
        /// Aplica una línea de tachado real carácter por carácter usando el símbolo Unicode combinatorio \u0336.
        /// Funciona en todas las fuentes estándar de Unity sin requerir TextMeshPro.
        /// </summary>
        public static string TacharTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in texto)
            {
                sb.Append(c);
                if (c != ' ' && c != '\n' && c != '\r' && c != '\t')
                {
                    sb.Append('\u0336'); // Combining Long Stroke Overlay
                }
            }
            return sb.ToString();
        }

        private void RefrescarTextoTareas()
        {
            if (textoListaTareas == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var t in tareasActivas)
            {
                if (t.completada)
                {
                    // Tarea completada: Verde brillante con tachado real
                    string tachado = TacharTexto(t.descripcion);
                    sb.AppendLine($"<color=#4ADE80>[✔] {tachado}</color>");
                }
                else
                {
                    // Tarea pendiente: Blanco nítido con casilla vacía
                    sb.AppendLine($"<color=#FFFFFF>[ ] {t.descripcion}</color>");
                }
            }

            textoListaTareas.text = sb.ToString();
        }

        #endregion

        #region Gestión de Cordura (10 Segmentos = 10% Cada Uno)

        private void AsegurarSegmentosExistentes()
        {
            if (panelCordura == null) return;
            Transform contExistente = panelCordura.transform.Find("Contenedor_Segmentos");
            if (contExistente == null) return;

            if (listaSegmentosCordura.Count != cantidadSegmentos || contExistente.childCount != cantidadSegmentos)
            {
                listaSegmentosCordura.Clear();
                for (int i = contExistente.childCount - 1; i >= 0; i--)
                {
                    if (Application.isPlaying) Destroy(contExistente.GetChild(i).gameObject);
                    else DestroyImmediate(contExistente.GetChild(i).gameObject);
                }

                for (int i = 0; i < cantidadSegmentos; i++)
                {
                    GameObject segObj = new GameObject($"Segmento_{i}", typeof(RectTransform), typeof(Image));
                    segObj.transform.SetParent(contExistente, false);
                    Image imgSeg = segObj.GetComponent<Image>();
                    imgSeg.sprite = spriteVerdeProcedural;
                    imgSeg.type = Image.Type.Simple;
                    imgSeg.color = Color.white;
                    listaSegmentosCordura.Add(imgSeg);
                }
            }
        }

        public void ActualizarCorduraUI(float actual, float maximo)
        {
            AsegurarSegmentosExistentes();

            float ratio = Mathf.Clamp01(actual / maximo);
            // Cada segmento representa 10% exacto cuando cantidadSegmentos = 10
            int segmentosActivos = Mathf.RoundToInt(ratio * cantidadSegmentos);
            if (ratio > 0.001f && segmentosActivos == 0) segmentosActivos = 1;

            Sprite spriteActivo = spriteSegmentoPersonalizado != null 
                ? spriteSegmentoPersonalizado 
                : (ratio > 0.40f ? spriteVerdeProcedural : (ratio > 0.20f ? spriteAmbarProcedural : spriteRojoProcedural));

            Sprite spriteInactivo = spriteVacioPersonalizado != null 
                ? spriteVacioPersonalizado 
                : spriteVacioProcedural;

            for (int i = 0; i < listaSegmentosCordura.Count; i++)
            {
                var img = listaSegmentosCordura[i];
                if (img == null) continue;

                if (i < segmentosActivos)
                {
                    img.sprite = spriteActivo;
                    img.color = Color.white;
                }
                else
                {
                    img.sprite = spriteInactivo;
                    img.color = new Color(0.10f, 0.16f, 0.12f, 0.40f);
                }
            }

            if (textoPorcentajeCordura != null)
            {
                int porcentaje = Mathf.RoundToInt(ratio * 100f);
                bool agotada = jugadorEstamina != null && jugadorEstamina.EstaminaAgotada;
                textoPorcentajeCordura.text = agotada
                    ? $"✦ ESTAMINA: {porcentaje}% (Cansado)"
                    : $"✦ ESTAMINA: {porcentaje}%";

                if (agotada || ratio <= 0.20f)
                {
                    textoPorcentajeCordura.color = colorCritico;
                }
                else if (ratio <= 0.40f)
                {
                    textoPorcentajeCordura.color = colorAlerta;
                }
                else
                {
                    textoPorcentajeCordura.color = Color.white;
                }
            }
        }

        #endregion

        #region Subtítulos con Parser de Rich Text (Sin imprimir código)

        public bool EstaMostrandoSubtitulo => rutinaSubtitulo != null;

        public void MostrarSubtitulo(string texto, float duracion = 4.5f)
        {
            if (rutinaSubtitulo != null) StopCoroutine(rutinaSubtitulo);
            saltarMecanografia = false;
            rutinaSubtitulo = StartCoroutine(RutinaMostrarSubtitulo(texto, duracion));
        }

        public void OcultarSubtitulos()
        {
            if (rutinaSubtitulo != null) StopCoroutine(rutinaSubtitulo);
            if (textoSubtitulos != null) textoSubtitulos.text = string.Empty;
            if (panelSubtitulos != null) panelSubtitulos.SetActive(false);
            rutinaSubtitulo = null;
        }

        private struct TokenTexto
        {
            public string contenido;
            public bool esTag;
        }

        /// <summary>
        /// Divide el texto en tokens separando tags completos de Rich Text (ej. <color=#FF0000> o </color>)
        /// de los caracteres legibles normales. Así los tags se inyectan en 0 segundos y jamás aparecen en pantalla.
        /// </summary>
        private static List<TokenTexto> ParsearTokensRichText(string texto)
        {
            var tokens = new List<TokenTexto>();
            if (string.IsNullOrEmpty(texto)) return tokens;

            int i = 0;
            while (i < texto.Length)
            {
                if (texto[i] == '<')
                {
                    int finTag = texto.IndexOf('>', i);
                    if (finTag != -1)
                    {
                        string tag = texto.Substring(i, finTag - i + 1);
                        tokens.Add(new TokenTexto { contenido = tag, esTag = true });
                        i = finTag + 1;
                        continue;
                    }
                }

                tokens.Add(new TokenTexto { contenido = texto[i].ToString(), esTag = false });
                i++;
            }

            return tokens;
        }

        private static string ObtenerTagCierre(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag.Length < 3 || tag[0] != '<' || tag[1] == '/' || tag.EndsWith("/>"))
                return null;

            int finNombre = 1;
            while (finNombre < tag.Length && tag[finNombre] != '=' && tag[finNombre] != '>' && !char.IsWhiteSpace(tag[finNombre]))
            {
                finNombre++;
            }

            if (finNombre <= 1) return null;
            string nombre = tag.Substring(1, finNombre - 1).ToLowerInvariant();
            if (nombre == "quad") return null;

            return $"</{nombre}>";
        }

        private static string ConstruirTextoConCierres(System.Text.StringBuilder sbActual, Stack<string> pilaCierres)
        {
            if (pilaCierres.Count == 0) return sbActual.ToString();

            var sbTemporal = new System.Text.StringBuilder(sbActual.Length + pilaCierres.Count * 10);
            sbTemporal.Append(sbActual);
            foreach (string cierre in pilaCierres)
            {
                sbTemporal.Append(cierre);
            }
            return sbTemporal.ToString();
        }

        private IEnumerator RutinaMostrarSubtitulo(string textoCompleto, float duracion)
        {
            if (panelSubtitulos != null) panelSubtitulos.SetActive(true);
            if (textoSubtitulos == null) yield break;

            textoSubtitulos.supportRichText = true;

            if (!usarEfectoMecanografia)
            {
                textoSubtitulos.text = textoCompleto;
                yield return new WaitForSeconds(duracion);
            }
            else
            {
                textoSubtitulos.text = "";
                var tokens = ParsearTokensRichText(textoCompleto);
                var sbActual = new System.Text.StringBuilder(textoCompleto.Length);
                var pilaCierres = new Stack<string>();
                float velocidadCached = velocidadMecanografia;
                var esperaCaracter = new WaitForSeconds(velocidadCached);

                int charCount = 0;
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (saltarMecanografia)
                    {
                        textoSubtitulos.text = textoCompleto;
                        break;
                    }

                    var token = tokens[i];
                    if (token.esTag)
                    {
                        sbActual.Append(token.contenido);
                        if (token.contenido.StartsWith("</"))
                        {
                            if (pilaCierres.Count > 0) pilaCierres.Pop();
                        }
                        else
                        {
                            string cierre = ObtenerTagCierre(token.contenido);
                            if (cierre != null) pilaCierres.Push(cierre);
                        }

                        textoSubtitulos.text = ConstruirTextoConCierres(sbActual, pilaCierres);
                        continue;
                    }

                    // Carácter visible: se agrega manteniendo cerrados los tags Rich Text activos
                    sbActual.Append(token.contenido);
                    textoSubtitulos.text = ConstruirTextoConCierres(sbActual, pilaCierres);

                    char c = token.contenido[0];
                    if (char.IsLetterOrDigit(c))
                    {
                        charCount++;
                        if (charCount % cadaCuantosCaracteresSuenaBlip == 0)
                        {
                            ReproducirBlipHabla();
                        }
                    }

                    if (!Mathf.Approximately(velocidadCached, velocidadMecanografia))
                    {
                        velocidadCached = velocidadMecanografia;
                        esperaCaracter = new WaitForSeconds(velocidadCached);
                    }
                    yield return esperaCaracter;
                }

                // Garantizar texto íntegro final
                textoSubtitulos.text = textoCompleto;
                yield return new WaitForSeconds(duracion);
            }

            if (textoSubtitulos != null) textoSubtitulos.text = string.Empty;
            if (panelSubtitulos != null) panelSubtitulos.SetActive(false);
            rutinaSubtitulo = null;
        }

        #endregion

        #region Sistema de Audio del HUD

        private void AsegurarAudioSources()
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length >= 3)
            {
                audioSourceSFX = sources[0];
                audioSourceMusica = sources[1];
                audioSourceHabla = sources[2];
            }
            else
            {
                if (audioSourceSFX == null) audioSourceSFX = gameObject.AddComponent<AudioSource>();
                if (audioSourceMusica == null) audioSourceMusica = gameObject.AddComponent<AudioSource>();
                if (audioSourceHabla == null) audioSourceHabla = gameObject.AddComponent<AudioSource>();
            }

            audioSourceSFX.playOnAwake = false;
            audioSourceSFX.spatialBlend = 0f;

            audioSourceMusica.playOnAwake = false;
            audioSourceMusica.loop = true;
            audioSourceMusica.spatialBlend = 0f;
            audioSourceMusica.volume = volumenMusica;

            audioSourceHabla.playOnAwake = false;
            audioSourceHabla.spatialBlend = 0f;
        }

        public void ReproducirSonidoTareaCompletada()
        {
            if (audioSourceSFX == null) return;
            AudioClip clip = sonidoTareaCompletada != null ? sonidoTareaCompletada : clipChimeProcedural;
            if (clip != null) audioSourceSFX.PlayOneShot(clip, volumenSFX);
        }

        public void ReproducirSonidoNuevaTarea()
        {
            if (audioSourceSFX == null) return;
            AudioClip clip = sonidoNuevaTarea != null ? sonidoNuevaTarea : clipNuevaTareaProcedural;
            if (clip != null) audioSourceSFX.PlayOneShot(clip, volumenSFX * 0.75f);
        }

        private void ReproducirBlipHabla()
        {
            if (audioSourceHabla == null) return;
            AudioClip clip = sonidoHablaBlip != null ? sonidoHablaBlip : clipBlipProcedural;
            if (clip == null) return;

            float pitchOffset = UnityEngine.Random.Range(-variacionTonoHabla, variacionTonoHabla);
            audioSourceHabla.pitch = Mathf.Clamp(tonoBaseHabla + pitchOffset, 0.4f, 2.2f);
            audioSourceHabla.PlayOneShot(clip, volumenBlipHabla);
        }

        public void ReproducirMusica(AudioClip nuevaMusica = null, float volumen = -1f)
        {
            if (audioSourceMusica == null) return;
            if (nuevaMusica != null) musicaFondo = nuevaMusica;
            if (volumen >= 0f) volumenMusica = volumen;

            if (musicaFondo != null)
            {
                audioSourceMusica.clip = musicaFondo;
                audioSourceMusica.volume = volumenMusica;
                if (!audioSourceMusica.isPlaying) audioSourceMusica.Play();
            }
        }

        public void AjustarVolumenMusicaGradual(float volumenObjetivo, float duracion = 15f)
        {
            if (audioSourceMusica == null) return;
            if (!audioSourceMusica.isPlaying && musicaFondo != null)
            {
                audioSourceMusica.clip = musicaFondo;
                audioSourceMusica.Play();
            }
            if (rutinaFadeMusica != null) StopCoroutine(rutinaFadeMusica);
            rutinaFadeMusica = StartCoroutine(RutinaFadeMusica(Mathf.Clamp01(volumenObjetivo), Mathf.Max(0.1f, duracion)));
        }

        public void DetenerMusica(float tiempoFade = 1f)
        {
            if (audioSourceMusica == null || !audioSourceMusica.isPlaying) return;
            if (tiempoFade <= 0f)
            {
                audioSourceMusica.Stop();
                return;
            }

            if (rutinaFadeMusica != null) StopCoroutine(rutinaFadeMusica);
            rutinaFadeMusica = StartCoroutine(RutinaFadeMusica(0f, tiempoFade, () => audioSourceMusica.Stop()));
        }

        private IEnumerator RutinaFadeMusica(float targetVol, float duracion, Action onComplete = null)
        {
            float startVol = audioSourceMusica.volume;
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                audioSourceMusica.volume = Mathf.Lerp(startVol, targetVol, t / duracion);
                yield return null;
            }
            audioSourceMusica.volume = targetVol;
            onComplete?.Invoke();
            rutinaFadeMusica = null;
        }

        #endregion

        #region Generación Procedural de Sprites y Clips de Audio

        private void GenerarAssetsProcedurales()
        {
            Color bordeOscuro = new Color(0.04f, 0.08f, 0.06f, 1f);
            Color centroAmarillo = new Color(0.96f, 1.0f, 0.28f, 1f);

            if (spriteVerdeProcedural == null)
            {
                spriteVerdeProcedural = CrearSpriteSegmento(
                    new Color(0.18f, 0.70f, 0.32f, 1f),
                    centroAmarillo,
                    bordeOscuro
                );
            }

            if (spriteAmbarProcedural == null)
            {
                spriteAmbarProcedural = CrearSpriteSegmento(
                    new Color(0.88f, 0.55f, 0.12f, 1f),
                    new Color(1f, 0.94f, 0.40f, 1f),
                    new Color(0.08f, 0.05f, 0.02f, 1f)
                );
            }

            if (spriteRojoProcedural == null)
            {
                spriteRojoProcedural = CrearSpriteSegmento(
                    new Color(0.85f, 0.20f, 0.20f, 1f),
                    new Color(1f, 0.65f, 0.40f, 1f),
                    new Color(0.08f, 0.02f, 0.02f, 1f)
                );
            }

            if (spriteVacioProcedural == null)
            {
                spriteVacioProcedural = CrearSpriteSegmento(
                    new Color(0.08f, 0.14f, 0.10f, 0.45f),
                    new Color(0.10f, 0.16f, 0.12f, 0.45f),
                    new Color(0.03f, 0.05f, 0.04f, 0.85f)
                );
            }

            if (sonidoTareaCompletada == null && clipChimeProcedural == null)
            {
                int sr = 44100;
                float dur = 0.45f;
                int count = (int)(sr * dur);
                float[] samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)sr;
                    float freq = t < 0.15f ? 783.99f : 1046.50f;
                    float env = t < 0.15f ? Mathf.Exp(-t * 9f) : Mathf.Exp(-(t - 0.15f) * 6f);
                    float sine = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f;
                    samples[i] = sine * env * 0.40f;
                }
                clipChimeProcedural = AudioClip.Create("Chime_Tarea_Procedural", count, 1, sr, false);
                clipChimeProcedural.SetData(samples, 0);
            }

            if (sonidoHablaBlip == null && clipBlipProcedural == null)
            {
                int sr = 44100;
                float dur = 0.040f;
                int count = (int)(sr * dur);
                float[] samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)sr;
                    float freq = 360f + (1f - (t / dur)) * 60f;
                    float env = Mathf.Pow(1f - (t / dur), 2.2f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
                }
                clipBlipProcedural = AudioClip.Create("Blip_Habla_Procedural", count, 1, sr, false);
                clipBlipProcedural.SetData(samples, 0);
            }

            if (sonidoNuevaTarea == null && clipNuevaTareaProcedural == null)
            {
                int sr = 44100;
                float dur = 0.06f;
                int count = (int)(sr * dur);
                float[] samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)sr;
                    float env = Mathf.Exp(-t * 40f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.30f;
                }
                clipNuevaTareaProcedural = AudioClip.Create("Click_NuevaTarea_Procedural", count, 1, sr, false);
                clipNuevaTareaProcedural.SetData(samples, 0);
            }
        }

        private Sprite CrearSpriteSegmento(Color colBase, Color colCentro, Color colBorde)
        {
            int w = 36;
            int h = 24;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixeles = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < 2 || x >= w - 2 || y < 2 || y >= h - 2)
                    {
                        pixeles[y * w + x] = colBorde;
                        continue;
                    }

                    if (y >= 9 && y <= 14)
                    {
                        float factor = 1f - Mathf.Abs((y - 11.5f) / 3f);
                        pixeles[y * w + x] = Color.Lerp(colBase, colCentro, factor * 0.95f);
                    }
                    else if (y > 14)
                    {
                        float grad = (y - 14f) / (h - 16f);
                        pixeles[y * w + x] = Color.Lerp(colBase, colBase * 0.88f, grad * 0.3f);
                    }
                    else
                    {
                        float grad = (9f - y) / 7f;
                        pixeles[y * w + x] = Color.Lerp(colBase, colBase * 0.72f, grad * 0.4f);
                    }
                }
            }

            tex.SetPixels(pixeles);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        #endregion

        #region Estilos Visuales y Posicionamiento

        private void AgregarSombra(Text texto, Vector2 distancia)
        {
            if (texto == null) return;
            var shadow = texto.GetComponent<Shadow>() ?? texto.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
            shadow.effectDistance = distancia;
        }

        public void AplicarEstiloYPosiciones()
        {
            if (fuenteUI == null) fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Panel de Cordura -> SUPERIOR IZQUIERDA
            if (panelCordura != null)
            {
                RectTransform rtCord = panelCordura.GetComponent<RectTransform>();
                if (rtCord != null)
                {
                    rtCord.anchorMin = new Vector2(0f, 1f);
                    rtCord.anchorMax = new Vector2(0f, 1f);
                    rtCord.pivot = new Vector2(0f, 1f);
                    rtCord.anchoredPosition = posicionPanelCordura;
                    rtCord.sizeDelta = tamanoPanelCordura;
                }

                if (textoPorcentajeCordura != null)
                {
                    textoPorcentajeCordura.fontSize = tamanoFuenteCordura;
                    textoPorcentajeCordura.fontStyle = FontStyle.Bold;
                    AgregarSombra(textoPorcentajeCordura, new Vector2(1.5f, -1.5f));
                }
            }

            // 2. Panel de Tareas -> SUPERIOR DERECHA
            if (panelTareas != null)
            {
                RectTransform rtTar = panelTareas.GetComponent<RectTransform>();
                if (rtTar != null)
                {
                    rtTar.anchorMin = new Vector2(1f, 1f);
                    rtTar.anchorMax = new Vector2(1f, 1f);
                    rtTar.pivot = new Vector2(1f, 1f);
                    rtTar.anchoredPosition = posicionPanelTareas;
                    rtTar.sizeDelta = tamanoPanelTareas;
                }

                if (textoTituloTareas != null)
                {
                    textoTituloTareas.fontSize = tamanoFuenteTituloTareas;
                    textoTituloTareas.fontStyle = FontStyle.Bold;
                    AgregarSombra(textoTituloTareas, new Vector2(1.5f, -1.5f));
                }

                if (textoListaTareas != null)
                {
                    textoListaTareas.fontSize = tamanoFuenteListaTareas;
                    textoListaTareas.lineSpacing = 1.3f;
                    AgregarSombra(textoListaTareas, new Vector2(1.2f, -1.2f));
                }
            }

            // 3. Panel de Subtítulos -> INFERIOR CENTRO
            if (panelSubtitulos != null)
            {
                RectTransform rtSub = panelSubtitulos.GetComponent<RectTransform>();
                if (rtSub != null)
                {
                    rtSub.anchorMin = new Vector2(0.5f, 0f);
                    rtSub.anchorMax = new Vector2(0.5f, 0f);
                    rtSub.pivot = new Vector2(0.5f, 0f);
                    rtSub.anchoredPosition = posicionPanelSubtitulos;
                    rtSub.sizeDelta = tamanoPanelSubtitulos;
                }

                if (textoSubtitulos != null)
                {
                    textoSubtitulos.fontSize = tamanoFuenteSubtitulos;
                    textoSubtitulos.fontStyle = FontStyle.Bold;
                    textoSubtitulos.alignment = TextAnchor.MiddleCenter;
                    textoSubtitulos.supportRichText = true;
                    textoSubtitulos.horizontalOverflow = HorizontalWrapMode.Wrap;
                    textoSubtitulos.verticalOverflow = VerticalWrapMode.Overflow;
                    textoSubtitulos.color = new Color(1f, 0.98f, 0.92f);
                    AgregarSombra(textoSubtitulos, new Vector2(2f, -2f));
                    var outline = textoSubtitulos.GetComponent<Outline>() ?? textoSubtitulos.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }
        }

        private void AsegurarSortingOrderCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
            if (canvas == null)
            {
                var go = GameObject.Find("Canvas_Mundo1");
                if (go != null) canvas = go.GetComponent<Canvas>();
            }
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }
        }

        #endregion

        #region Construcción Procedural de la Interfaz

        private void ConstruirUIProcedural()
        {
            GameObject canvasObj = GameObject.Find("Canvas_Mundo1");
            Canvas canvas = null;
            if (canvasObj != null)
            {
                canvas = canvasObj.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                canvasObj = new GameObject("Canvas_Mundo1", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 150;
                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                canvas.sortingOrder = 150;
            }

            // 1. Panel de Tareas (Superior Derecho)
            if (panelTareas == null)
            {
                Transform tExistente = canvas.transform.Find("Panel_Tareas");
                if (tExistente != null)
                {
                    panelTareas = tExistente.gameObject;
                    textoTituloTareas = panelTareas.transform.Find("Texto_Titulo")?.GetComponent<Text>();
                    textoListaTareas = panelTareas.transform.Find("Texto_Lista")?.GetComponent<Text>();
                }
                else
                {
                    GameObject pTareas = new GameObject("Panel_Tareas", typeof(RectTransform), typeof(Image));
                    pTareas.transform.SetParent(canvas.transform, false);
                    RectTransform rt = pTareas.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = posicionPanelTareas;
                    rt.sizeDelta = tamanoPanelTareas;

                    Image img = pTareas.GetComponent<Image>();
                    img.color = new Color(0.04f, 0.07f, 0.05f, 0.88f);

                    // Título
                    GameObject goTitulo = new GameObject("Texto_Titulo", typeof(RectTransform), typeof(Text));
                    goTitulo.transform.SetParent(pTareas.transform, false);
                    RectTransform rtTit = goTitulo.GetComponent<RectTransform>();
                    rtTit.anchorMin = new Vector2(0f, 1f);
                    rtTit.anchorMax = new Vector2(1f, 1f);
                    rtTit.pivot = new Vector2(0.5f, 1f);
                    rtTit.anchoredPosition = new Vector2(0f, -14f);
                    rtTit.sizeDelta = new Vector2(-28f, 34f);

                    textoTituloTareas = goTitulo.GetComponent<Text>();
                    textoTituloTareas.font = fuenteUI;
                    textoTituloTareas.fontSize = tamanoFuenteTituloTareas;
                    textoTituloTareas.fontStyle = FontStyle.Bold;
                    textoTituloTareas.alignment = TextAnchor.MiddleLeft;
                    textoTituloTareas.color = new Color(0.35f, 1f, 0.55f);
                    textoTituloTareas.text = "✦ TURNO NOCTURNO: TAREAS";
                    AgregarSombra(textoTituloTareas, new Vector2(1.5f, -1.5f));

                    // Lista de tareas
                    GameObject goLista = new GameObject("Texto_Lista", typeof(RectTransform), typeof(Text));
                    goLista.transform.SetParent(pTareas.transform, false);
                    RectTransform rtList = goLista.GetComponent<RectTransform>();
                    rtList.anchorMin = new Vector2(0f, 0f);
                    rtList.anchorMax = new Vector2(1f, 1f);
                    rtList.pivot = new Vector2(0.5f, 0.5f);
                    rtList.anchoredPosition = new Vector2(0f, -18f);
                    rtList.sizeDelta = new Vector2(-28f, -64f);

                    textoListaTareas = goLista.GetComponent<Text>();
                    textoListaTareas.font = fuenteUI;
                    textoListaTareas.fontSize = tamanoFuenteListaTareas;
                    textoListaTareas.lineSpacing = 1.3f;
                    textoListaTareas.alignment = TextAnchor.UpperLeft;
                    textoListaTareas.color = Color.white;
                    textoListaTareas.text = "Iniciando turno...";
                    AgregarSombra(textoListaTareas, new Vector2(1.2f, -1.2f));

                    panelTareas = pTareas;
                }
            }

            // 2. Barra de Cordura Segmentada (10 bloques LED)
            if (panelCordura == null)
            {
                Transform cExistente = canvas.transform.Find("Panel_Cordura");
                if (cExistente != null)
                {
                    panelCordura = cExistente.gameObject;
                }
                else
                {
                    panelCordura = new GameObject("Panel_Cordura", typeof(RectTransform), typeof(Image));
                    panelCordura.transform.SetParent(canvas.transform, false);
                }
            }

            RectTransform rtCordura = panelCordura.GetComponent<RectTransform>();
            rtCordura.anchorMin = new Vector2(0f, 1f);
            rtCordura.anchorMax = new Vector2(0f, 1f);
            rtCordura.pivot = new Vector2(0f, 1f);
            rtCordura.anchoredPosition = posicionPanelCordura;
            rtCordura.sizeDelta = tamanoPanelCordura;

            Image imgCardCord = panelCordura.GetComponent<Image>();
            imgCardCord.color = new Color(0.04f, 0.06f, 0.08f, 0.90f);

            // Texto Porcentaje Cordura
            Transform tTxtExistente = panelCordura.transform.Find("Texto_Cordura");
            GameObject goTxtC = tTxtExistente != null ? tTxtExistente.gameObject : new GameObject("Texto_Cordura", typeof(RectTransform), typeof(Text));
            goTxtC.transform.SetParent(panelCordura.transform, false);
            RectTransform rtTC = goTxtC.GetComponent<RectTransform>();
            rtTC.anchorMin = new Vector2(0f, 1f);
            rtTC.anchorMax = new Vector2(1f, 1f);
            rtTC.pivot = new Vector2(0f, 1f);
            rtTC.anchoredPosition = new Vector2(16f, -10f);
            rtTC.sizeDelta = new Vector2(-32f, 26f);

            textoPorcentajeCordura = goTxtC.GetComponent<Text>();
            textoPorcentajeCordura.font = fuenteUI;
            textoPorcentajeCordura.fontSize = tamanoFuenteCordura;
            textoPorcentajeCordura.fontStyle = FontStyle.Bold;
            textoPorcentajeCordura.color = Color.white;
            textoPorcentajeCordura.text = "✦ ESTAMINA: 100%";
            AgregarSombra(textoPorcentajeCordura, new Vector2(1.5f, -1.5f));

            // Eliminar contenedor continuo antiguo si existía
            Transform barraVieja = panelCordura.transform.Find("Barra_Fondo");
            if (barraVieja != null) DestroyImmediate(barraVieja.gameObject);

            // Contenedor de Segmentos con HorizontalLayoutGroup
            Transform contExistente = panelCordura.transform.Find("Contenedor_Segmentos");
            GameObject goContSeg = contExistente != null ? contExistente.gameObject : new GameObject("Contenedor_Segmentos", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            goContSeg.transform.SetParent(panelCordura.transform, false);
            RectTransform rtContSeg = goContSeg.GetComponent<RectTransform>();
            rtContSeg.anchorMin = new Vector2(0f, 0f);
            rtContSeg.anchorMax = new Vector2(1f, 0f);
            rtContSeg.pivot = new Vector2(0.5f, 0f);
            rtContSeg.anchoredPosition = new Vector2(0f, 12f);
            rtContSeg.sizeDelta = new Vector2(-32f, 26f);

            HorizontalLayoutGroup layout = goContSeg.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Crear los 10 segmentos
            listaSegmentosCordura.Clear();
            for (int i = goContSeg.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(goContSeg.transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < cantidadSegmentos; i++)
            {
                GameObject segObj = new GameObject($"Segmento_{i}", typeof(RectTransform), typeof(Image));
                segObj.transform.SetParent(goContSeg.transform, false);
                Image imgSeg = segObj.GetComponent<Image>();
                imgSeg.sprite = spriteVerdeProcedural;
                imgSeg.type = Image.Type.Simple;
                imgSeg.color = Color.white;
                listaSegmentosCordura.Add(imgSeg);
            }

            // 3. Panel de Subtítulos (Inferior Central)
            if (panelSubtitulos == null)
            {
                Transform sExistente = canvas.transform.Find("Panel_Subtitulos");
                if (sExistente != null)
                {
                    panelSubtitulos = sExistente.gameObject;
                    textoSubtitulos = panelSubtitulos.transform.Find("Texto_Subtitulo")?.GetComponent<Text>();
                }
                else
                {
                    GameObject pSub = new GameObject("Panel_Subtitulos", typeof(RectTransform), typeof(Image));
                    pSub.transform.SetParent(canvas.transform, false);
                    RectTransform rtSub = pSub.GetComponent<RectTransform>();
                    rtSub.anchorMin = new Vector2(0.5f, 0f);
                    rtSub.anchorMax = new Vector2(0.5f, 0f);
                    rtSub.pivot = new Vector2(0.5f, 0f);
                    rtSub.anchoredPosition = posicionPanelSubtitulos;
                    rtSub.sizeDelta = tamanoPanelSubtitulos;

                    Image imgSub = pSub.GetComponent<Image>();
                    imgSub.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);

                    GameObject goTxtS = new GameObject("Texto_Subtitulo", typeof(RectTransform), typeof(Text));
                    goTxtS.transform.SetParent(pSub.transform, false);
                    RectTransform rtTS = goTxtS.GetComponent<RectTransform>();
                    rtTS.anchorMin = Vector2.zero;
                    rtTS.anchorMax = Vector2.one;
                    rtTS.sizeDelta = new Vector2(-40f, -20f);

                    textoSubtitulos = goTxtS.GetComponent<Text>();
                    textoSubtitulos.font = fuenteUI;
                    textoSubtitulos.fontSize = tamanoFuenteSubtitulos;
                    textoSubtitulos.fontStyle = FontStyle.Bold;
                    textoSubtitulos.alignment = TextAnchor.MiddleCenter;
                    textoSubtitulos.supportRichText = true;
                    textoSubtitulos.horizontalOverflow = HorizontalWrapMode.Wrap;
                    textoSubtitulos.verticalOverflow = VerticalWrapMode.Overflow;
                    textoSubtitulos.color = new Color(1f, 0.98f, 0.92f);
                    textoSubtitulos.text = "";
                    AgregarSombra(textoSubtitulos, new Vector2(2f, -2f));

                    var outline = goTxtS.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);

                    pSub.SetActive(false);
                    panelSubtitulos = pSub;
                }
            }
        }

        #endregion
    }
}
