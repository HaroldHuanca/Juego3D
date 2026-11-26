using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mundo1.UI;

namespace Mundo1
{
    /// <summary>
    /// Gestiona el orden secuencial (1 -> 4) de los paquetes en Mundo 1,
    /// su estado de entrega en anaqueles y el marcador visual en pantalla hacia la caja rastreada.
    /// </summary>
    [DisallowMultipleComponent]
    public class SistemaPaquetesMundo1 : MonoBehaviour
    {
        public static SistemaPaquetesMundo1 Instance { get; private set; }

        public enum EstadoPaquete
        {
            Bloqueado,
            Disponible,
            Entregado
        }

        [Serializable]
        public class InfoPaquete
        {
            public int numero;
            public string nombre;
            public string ubicacion;
            public PaqueteMundo1 objetoPaquete;
            public bool entregado;
        }

        [Header("Opciones del Marcador en Vista")]
        [SerializeField] private Color colorMarcadorCaja = new Color(0.25f, 0.90f, 1f, 1f);
        [SerializeField] private Color colorMarcadorAnaquel = new Color(0.35f, 1f, 0.55f, 1f);

        private readonly List<InfoPaquete> paquetes = new List<InfoPaquete>();
        private int indiceActual = 0; // 0..3; >= 4 significa todos entregados
        private int numeroRastreado = -1; // -1 = ninguno rastreado actualmente
        private bool cajaSostenidaActualmente = false;
        private bool estuvoDentroConPaquete4 = false;
        private bool dialogoTerrorAlmacenMostrado = false;
        private MonstruoWarehouse monstruoAlmacen;
        private Transform jugadorTransform;

        // UI del marcador proyectado en pantalla
        private Canvas canvasMarcador;
        private RectTransform rectContenedorMarcador;
        private Image imagenFondoMarcador;
        private Text textoTituloMarcador;
        private Text textoDistanciaMarcador;
        private Text textoFlechaMarcador;
        private Font fuenteUI;
        private Camera camaraPrincipal;

        public event Action OnEstadoPaquetesCambiado;

        public int IndiceActual => indiceActual;
        public int NumeroRastreado => numeroRastreado;
        public int TotalPaquetes => paquetes.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConstruirUIMarcador();
            InicializarListaDesdeEscena();
        }

        private void Start()
        {
            if (paquetes.Count == 0)
            {
                InicializarListaDesdeEscena();
            }
            ActualizarVisibilidadMarcador();
        }

        public void InicializarListaDesdeEscena()
        {
            paquetes.Clear();
            var encontrados = FindObjectsByType<PaqueteMundo1>(FindObjectsInactive.Include);
            Array.Sort(encontrados, (a, b) => a.NumeroPaquete.CompareTo(b.NumeroPaquete));

            for (int i = 0; i < encontrados.Length; i++)
            {
                var p = encontrados[i];
                if (p == null) continue;
                paquetes.Add(new InfoPaquete
                {
                    numero = p.NumeroPaquete,
                    nombre = p.NombrePaquete,
                    ubicacion = p.UbicacionDescripcion,
                    objetoPaquete = p,
                    entregado = false
                });
            }

            if (paquetes.Count == 0)
            {
                // Respaldo informativo si aún no se asignaron PaqueteMundo1
                string[] ubicaciones =
                {
                    "Almacén interior de la tienda",
                    "Almacén exterior · Entrada",
                    "Almacén exterior · Pasillo Sur",
                    "Almacén exterior · Fondo"
                };
                for (int i = 0; i < 4; i++)
                {
                    paquetes.Add(new InfoPaquete
                    {
                        numero = i + 1,
                        nombre = $"Paquete #{i + 1}",
                        ubicacion = ubicaciones[i],
                        objetoPaquete = null,
                        entregado = false
                    });
                }
            }

            indiceActual = 0;
            numeroRastreado = -1;
            cajaSostenidaActualmente = false;
            OnEstadoPaquetesCambiado?.Invoke();
        }

        public IReadOnlyList<InfoPaquete> ObtenerPaquetes() => paquetes;

        public bool EstaEnFaseReposicion()
        {
            return DirectorTurno.Instance != null &&
                   DirectorTurno.Instance.FaseActual == DirectorTurno.FaseTurno.ReponiendoAnaqueles;
        }

        public EstadoPaquete ObtenerEstado(int numeroPaquete)
        {
            int idx = numeroPaquete - 1;
            if (idx < 0 || idx >= paquetes.Count) return EstadoPaquete.Bloqueado;
            if (paquetes[idx].entregado) return EstadoPaquete.Entregado;
            if (!EstaEnFaseReposicion()) return EstadoPaquete.Bloqueado;
            return idx == indiceActual ? EstadoPaquete.Disponible : EstadoPaquete.Bloqueado;
        }

        public bool EstaRastreado(int numeroPaquete)
        {
            return numeroRastreado == numeroPaquete && ObtenerEstado(numeroPaquete) == EstadoPaquete.Disponible;
        }

        /// <summary>
        /// Activa el marcador en la vista del jugador para el paquete actual permitido.
        /// </summary>
        public bool SeleccionarYRastrearPaquete(int numeroPaquete, out string mensajeFeedback)
        {
            if (!EstaEnFaseReposicion())
            {
                mensajeFeedback = "⏳ Primero atiende a los clientes en caja hasta que se vacíen los anaqueles.";
                return false;
            }

            EstadoPaquete estado = ObtenerEstado(numeroPaquete);
            if (estado == EstadoPaquete.Entregado)
            {
                mensajeFeedback = $"✅ El Paquete #{numeroPaquete} ya fue entregado en su anaquel.";
                return false;
            }

            if (estado == EstadoPaquete.Bloqueado)
            {
                int requerido = Mathf.Clamp(indiceActual + 1, 1, Mathf.Max(1, paquetes.Count));
                mensajeFeedback = $"🔒 Bloqueado: primero entrega el Paquete #{requerido}.";
                return false;
            }

            numeroRastreado = numeroPaquete;
            ActualizarVisibilidadMarcador();
            OnEstadoPaquetesCambiado?.Invoke();

            InfoPaquete info = paquetes[numeroPaquete - 1];
            mensajeFeedback = $"📍 Ubicación marcada en pantalla: {info.nombre} ({info.ubicacion}).";
            return true;
        }

        /// <summary>
        /// Comprueba si el jugador puede recoger esta caja física en el mapa.
        /// </summary>
        public bool PuedeRecogerCaja(GameObject cajaObj, out string motivoBloqueo)
        {
            motivoBloqueo = "";
            if (cajaObj == null) return false;

            var paquete = cajaObj.GetComponent<PaqueteMundo1>();
            if (paquete == null) return true;

            if (!EstaEnFaseReposicion())
            {
                motivoBloqueo = "⏳ Aún no es momento de reponer anaqueles.";
                return false;
            }

            EstadoPaquete estado = ObtenerEstado(paquete.NumeroPaquete);
            if (estado != EstadoPaquete.Disponible)
            {
                int requerido = Mathf.Clamp(indiceActual + 1, 1, Mathf.Max(1, paquetes.Count));
                motivoBloqueo = $"🔒 Entrega primero el Paquete #{requerido} (consulta la PC)";
                return false;
            }

            return true;
        }

        public void NotificarCajaRecogida(GameObject cajaObj)
        {
            var paquete = cajaObj != null ? cajaObj.GetComponent<PaqueteMundo1>() : null;
            if (paquete != null)
            {
                numeroRastreado = paquete.NumeroPaquete;
            }
            cajaSostenidaActualmente = true;
            ActualizarVisibilidadMarcador();
            OnEstadoPaquetesCambiado?.Invoke();
        }

        public void NotificarCajaSoltada()
        {
            cajaSostenidaActualmente = false;
            ActualizarVisibilidadMarcador();
            OnEstadoPaquetesCambiado?.Invoke();
        }

        /// <summary>
        /// Marca el paquete actual como entregado al rellenar un anaquel y desbloquea el siguiente.
        /// </summary>
        public void NotificarPaqueteEntregado(GameObject cajaObj)
        {
            cajaSostenidaActualmente = false;

            int idxEntregado = indiceActual;
            var paquete = cajaObj != null ? cajaObj.GetComponent<PaqueteMundo1>() : null;
            if (paquete != null)
            {
                int candidate = paquete.NumeroPaquete - 1;
                if (candidate >= 0 && candidate < paquetes.Count) idxEntregado = candidate;
            }

            if (idxEntregado >= 0 && idxEntregado < paquetes.Count)
            {
                paquetes[idxEntregado].entregado = true;
            }

            // Avanzar al siguiente paquete pendiente
            indiceActual = paquetes.Count;
            for (int i = 0; i < paquetes.Count; i++)
            {
                if (!paquetes[i].entregado)
                {
                    indiceActual = i;
                    break;
                }
            }

            numeroRastreado = -1;
            ActualizarVisibilidadMarcador();
            OnEstadoPaquetesCambiado?.Invoke();

            if (HUDTareasMundo1.Instance != null)
            {
                if (indiceActual < paquetes.Count)
                {
                    int sigNum = paquetes[indiceActual].numero;
                    HUDTareasMundo1.Instance.MostrarSubtitulo(
                        $"✔ Paquete #{idxEntregado + 1} entregado. Consulta la PC para localizar el Paquete #{sigNum}.",
                        4.5f
                    );
                }
                else
                {
                    HUDTareasMundo1.Instance.MostrarSubtitulo(
                        "✔ ¡Todos los paquetes han sido entregados en los anaqueles!",
                        4.0f
                    );
                }
            }
        }

        private void LateUpdate()
        {
            VerificarSalidaAlmacenConUltimoPaquete();
            ActualizarPosicionMarcadorEnPantalla();
        }

        private void VerificarSalidaAlmacenConUltimoPaquete()
        {
            if (dialogoTerrorAlmacenMostrado || !cajaSostenidaActualmente || numeroRastreado != 4) return;

            if (monstruoAlmacen == null) monstruoAlmacen = FindAnyObjectByType<MonstruoWarehouse>(FindObjectsInactive.Include);
            if (jugadorTransform == null)
            {
                var mov = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
                if (mov != null) jugadorTransform = mov.transform;
            }
            if (monstruoAlmacen == null || jugadorTransform == null) return;

            bool dentroAlmacen = monstruoAlmacen.Dentro(jugadorTransform.position);
            if (dentroAlmacen)
            {
                estuvoDentroConPaquete4 = true;
            }
            else if (estuvoDentroConPaquete4)
            {
                dialogoTerrorAlmacenMostrado = true;
                if (HUDTareasMundo1.Instance != null)
                {
                    HUDTareasMundo1.Instance.MostrarSubtitulo(
                        "Tú: '¿Qué diablos fue eso ahí adentro...? Era aterrador... Tengo que terminar el turno ya.'",
                        4.8f
                    );
                }
            }
        }

        private void ActualizarVisibilidadMarcador()
        {
            if (rectContenedorMarcador == null) return;
            bool mostrar = EstaEnFaseReposicion() && numeroRastreado > 0 && indiceActual < paquetes.Count;
            rectContenedorMarcador.gameObject.SetActive(mostrar);
        }

        private void ActualizarPosicionMarcadorEnPantalla()
        {
            if (rectContenedorMarcador == null || !rectContenedorMarcador.gameObject.activeSelf) return;

            if (camaraPrincipal == null || !camaraPrincipal.isActiveAndEnabled)
            {
                camaraPrincipal = Camera.main;
                if (camaraPrincipal == null)
                {
                    var camScript = FindAnyObjectByType<Camara>();
                    if (camScript != null) camaraPrincipal = camScript.GetComponent<Camera>();
                }
            }
            if (camaraPrincipal == null) return;

            Transform objetivo = ObtenerObjetivoMarcador(out bool esHaciaAnaquel);
            if (objetivo == null)
            {
                rectContenedorMarcador.gameObject.SetActive(false);
                return;
            }

            Vector3 destinoMundo = objetivo.position + Vector3.up * (esHaciaAnaquel ? 0.25f : 0.45f);
            float distancia = Vector3.Distance(camaraPrincipal.transform.position, destinoMundo);
            Vector3 screenPos = camaraPrincipal.WorldToScreenPoint(destinoMundo);

            bool detras = screenPos.z < 0f;
            if (detras)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            float margenX = 120f;
            float margenY = 75f;
            bool fueraDePantalla = detras ||
                                   screenPos.x < margenX || screenPos.x > Screen.width - margenX ||
                                   screenPos.y < margenY || screenPos.y > Screen.height - margenY;

            screenPos.x = Mathf.Clamp(screenPos.x, margenX, Screen.width - margenX);
            screenPos.y = Mathf.Clamp(screenPos.y, margenY, Screen.height - margenY);

            rectContenedorMarcador.position = new Vector3(screenPos.x, screenPos.y, 0f);

            if (imagenFondoMarcador != null)
            {
                imagenFondoMarcador.color = esHaciaAnaquel
                    ? new Color(0.06f, 0.24f, 0.14f, 0.90f)
                    : new Color(0.05f, 0.14f, 0.24f, 0.90f);
            }

            if (textoTituloMarcador != null)
            {
                textoTituloMarcador.text = esHaciaAnaquel
                    ? "🛒 ANAQUEL VACÍO"
                    : $"📦 PAQUETE #{numeroRastreado}";
                textoTituloMarcador.color = esHaciaAnaquel ? colorMarcadorAnaquel : colorMarcadorCaja;
            }

            if (textoDistanciaMarcador != null)
            {
                textoDistanciaMarcador.text = $"{distancia:F1} m";
            }

            if (textoFlechaMarcador != null)
            {
                if (!fueraDePantalla)
                {
                    textoFlechaMarcador.text = "▼";
                }
                else
                {
                    if (detras) textoFlechaMarcador.text = screenPos.x < Screen.width * 0.5f ? "◄ ATRÁS" : "ATRÁS ►";
                    else if (screenPos.x <= margenX + 1f) textoFlechaMarcador.text = "◄";
                    else if (screenPos.x >= Screen.width - margenX - 1f) textoFlechaMarcador.text = "►";
                    else if (screenPos.y >= Screen.height - margenY - 1f) textoFlechaMarcador.text = "▲";
                    else textoFlechaMarcador.text = "▼";
                }
            }
        }

        private Transform ObtenerObjetivoMarcador(out bool esHaciaAnaquel)
        {
            esHaciaAnaquel = false;

            if (cajaSostenidaActualmente)
            {
                esHaciaAnaquel = true;
                var objetos = FindObjectsByType<ObjetoAleatorio>(FindObjectsInactive.Exclude);
                ObjetoAleatorio masCercano = null;
                float mejorDist = float.MaxValue;
                Vector3 refPos = camaraPrincipal != null ? camaraPrincipal.transform.position : Vector3.zero;

                for (int i = 0; i < objetos.Length; i++)
                {
                    var oa = objetos[i];
                    if (oa == null || oa.EstaVisible) continue;
                    if (oa.GetComponent<SensorAnaquelMundo1>() == null) continue;

                    float d = (oa.transform.position - refPos).sqrMagnitude;
                    if (d < mejorDist)
                    {
                        mejorDist = d;
                        masCercano = oa;
                    }
                }
                return masCercano != null ? masCercano.transform : null;
            }

            int idx = numeroRastreado - 1;
            if (idx >= 0 && idx < paquetes.Count && paquetes[idx].objetoPaquete != null)
            {
                return paquetes[idx].objetoPaquete.transform;
            }

            return null;
        }

        private void ConstruirUIMarcador()
        {
            var goCanvas = new GameObject("Canvas_MarcadorPaquete", typeof(Canvas), typeof(CanvasScaler));
            goCanvas.transform.SetParent(transform, false);
            canvasMarcador = goCanvas.GetComponent<Canvas>();
            canvasMarcador.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasMarcador.sortingOrder = 50;

            var scaler = goCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var goPanel = new GameObject("Marcador_Paquete", typeof(RectTransform), typeof(Image));
            goPanel.transform.SetParent(goCanvas.transform, false);
            rectContenedorMarcador = goPanel.GetComponent<RectTransform>();
            rectContenedorMarcador.sizeDelta = new Vector2(230f, 82f);

            imagenFondoMarcador = goPanel.GetComponent<Image>();
            imagenFondoMarcador.color = new Color(0.05f, 0.14f, 0.24f, 0.90f);
            imagenFondoMarcador.raycastTarget = false;

            textoTituloMarcador = CrearTexto("Titulo", goPanel.transform, "📦 PAQUETE #1", 19, new Vector2(0f, 18f), new Vector2(215f, 28f), colorMarcadorCaja);
            textoDistanciaMarcador = CrearTexto("Distancia", goPanel.transform, "0.0 m", 18, new Vector2(0f, -6f), new Vector2(215f, 24f), Color.white);
            textoFlechaMarcador = CrearTexto("Flecha", goPanel.transform, "▼", 16, new Vector2(0f, -27f), new Vector2(215f, 22f), new Color(1f, 0.90f, 0.4f));

            rectContenedorMarcador.gameObject.SetActive(false);
        }

        private Text CrearTexto(string nombre, Transform padre, string contenido, int size, Vector2 pos, Vector2 tam, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;

            var txt = go.GetComponent<Text>();
            txt.font = fuenteUI;
            txt.text = contenido;
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
