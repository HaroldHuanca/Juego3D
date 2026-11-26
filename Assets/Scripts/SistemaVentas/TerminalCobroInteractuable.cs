using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tienda.SistemaVentas
{
    public class TerminalCobroInteractuable : MonoBehaviour
    {
        private const float DistanciaMinimaEfectiva = 3.2f;
        private const float RadioSphereCast = 0.35f;
        private const float UmbralAnguloVision = 0.25f;

        [Header("Configuración de Interacción")]
        [Tooltip("Distancia máxima a la que el jugador puede estar de la PC")]
        [SerializeField] private float distanciaInteraccion = 3.8f;

        [Tooltip("Texto base cuando no hay cliente esperando cobro")]
        [SerializeField] private string mensajeInteraccion = "E · Usar Terminal PC";

        [Tooltip("Texto cuando hay un cliente listo para pagar")]
        [SerializeField] private string mensajeCobroListo = "E · Abrir Terminal (¡Cliente esperando cobro!)";

        [Header("Brillo de Aviso para Cobranza")]
        [SerializeField] private Color colorBrilloCobro = new Color(0.15f, 0.92f, 0.55f, 1f);
        [SerializeField] private float intensidadEmisionMin = 0.9f;
        [SerializeField] private float intensidadEmisionMax = 2.4f;
        [SerializeField] private float velocidadPulso = 3.2f;
        [SerializeField] private float grosorContorno = 0.028f;

        private Camera camaraJugador;
        private PlayerMovement scriptMovimiento;
        private GUIStyle estiloGUI;
        private Vector3 centroRealCacheado;
        private bool tieneCentroCacheado;
        private bool estaMirandoCacheado;
        private readonly RaycastHit[] hitsBuffer = new RaycastHit[8];

        private struct DatosRendererOriginal
        {
            public Renderer renderer;
            public Material[] materialesOriginales;
            public Material[] materialesConBrillo;
            public Material materialContorno;
        }

        private readonly List<DatosRendererOriginal> renderersBrillo = new List<DatosRendererOriginal>();
        private Light luzAviso;
        private Shader shaderContorno;
        private bool brilloInicializado;
        private bool brilloActivo;
        private bool eventosSuscritos;

        public float DistanciaInteraccion => Mathf.Max(distanciaInteraccion, DistanciaMinimaEfectiva);
        public string MensajeInteraccion => HayCobroPendiente() ? mensajeCobroListo : mensajeInteraccion;

        private void Start()
        {
            BuscarCamara();
            InicializarBrillo();
            SuscribirEventosTienda();
            ActualizarEstadoBrillo(HayCobroPendiente());

            Vector3 centro = ObtenerCentroReal();
            string nombreCam = camaraJugador != null ? camaraJugador.name : "Ninguna";
            Debug.Log($"<color=cyan>[Terminal]</color> Inicializada en '{gameObject.name}'. Centro geométrico: {centro}. Rango efectivo: {DistanciaInteraccion:F2}m. Cámara detectada: {nombreCam}");
        }

        private void OnEnable()
        {
            SuscribirEventosTienda();
        }

        private void OnDisable()
        {
            DesuscribirEventosTienda();
        }

        private void OnDestroy()
        {
            DesuscribirEventosTienda();
            LimpiarMaterialesInstanciados();
        }

        private void SuscribirEventosTienda()
        {
            if (eventosSuscritos || TiendaManager.Instance == null) return;

            TiendaManager.Instance.OnNuevoPedidoGenerado += AlLlegarNuevoPedido;
            TiendaManager.Instance.OnVentaFinalizadaConExito += AlTerminarCobro;
            TiendaManager.Instance.OnVentaCancelada += AlCancelarCobro;
            eventosSuscritos = true;
        }

        private void DesuscribirEventosTienda()
        {
            if (!eventosSuscritos || TiendaManager.Instance == null) return;

            TiendaManager.Instance.OnNuevoPedidoGenerado -= AlLlegarNuevoPedido;
            TiendaManager.Instance.OnVentaFinalizadaConExito -= AlTerminarCobro;
            TiendaManager.Instance.OnVentaCancelada -= AlCancelarCobro;
            eventosSuscritos = false;
        }

        private void AlLlegarNuevoPedido(PedidoCliente _) => ActualizarEstadoBrillo(true);
        private void AlTerminarCobro() => ActualizarEstadoBrillo(false);
        private void AlCancelarCobro(string _) => ActualizarEstadoBrillo(HayCobroPendiente());

        private static bool HayCobroPendiente()
        {
            return TiendaManager.Instance != null && TiendaManager.Instance.HayPedidoPendiente;
        }

        private void BuscarCamara()
        {
            Camara scriptCam = FindAnyObjectByType<Camara>();
            if (scriptCam != null)
            {
                camaraJugador = scriptCam.GetComponent<Camera>();
            }

            if (camaraJugador == null)
            {
                camaraJugador = Camera.main;
            }

            if (camaraJugador == null)
            {
                foreach (var cam in Camera.allCameras)
                {
                    if (cam.gameObject.name != "Camara de brazos")
                    {
                        camaraJugador = cam;
                        break;
                    }
                }
            }
        }

        public Vector3 ObtenerCentroReal()
        {
            if (Application.isPlaying && tieneCentroCacheado) return centroRealCacheado;

            Vector3 centro = transform.position;
            var rend = GetComponent<Renderer>();
            if (rend != null) centro = rend.bounds.center;
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) centro = col.bounds.center;
                else
                {
                    var rendHijo = GetComponentInChildren<Renderer>();
                    if (rendHijo != null) centro = rendHijo.bounds.center;
                }
            }

            if (Application.isPlaying)
            {
                centroRealCacheado = centro;
                tieneCentroCacheado = true;
            }

            return centro;
        }

        private void InicializarBrillo()
        {
            if (brilloInicializado) return;
            brilloInicializado = true;

            shaderContorno = Shader.Find("Custom/URP_Outline");

            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;

                Material[] originales = rend.sharedMaterials;
                if (originales == null || originales.Length == 0) continue;

                bool tieneContorno = shaderContorno != null;
                Material[] conBrillo = new Material[originales.Length + (tieneContorno ? 1 : 0)];

                for (int i = 0; i < originales.Length; i++)
                {
                    if (originales[i] != null)
                    {
                        Material instancia = new Material(originales[i]);
                        instancia.EnableKeyword("_EMISSION");
                        conBrillo[i] = instancia;
                    }
                }

                Material matContorno = null;
                if (tieneContorno)
                {
                    matContorno = new Material(shaderContorno);
                    if (matContorno.HasProperty("_OutlineColor")) matContorno.SetColor("_OutlineColor", colorBrilloCobro);
                    if (matContorno.HasProperty("_OutlineWidth")) matContorno.SetFloat("_OutlineWidth", grosorContorno);
                    conBrillo[originales.Length] = matContorno;
                }

                renderersBrillo.Add(new DatosRendererOriginal
                {
                    renderer = rend,
                    materialesOriginales = originales,
                    materialesConBrillo = conBrillo,
                    materialContorno = matContorno
                });
            }

            GameObject goLuz = new GameObject("LuzAvisoTerminal");
            goLuz.transform.SetParent(transform, false);
            goLuz.transform.position = ObtenerCentroReal() + Vector3.up * 0.28f + transform.forward * 0.2f;
            luzAviso = goLuz.AddComponent<Light>();
            luzAviso.type = LightType.Point;
            luzAviso.color = colorBrilloCobro;
            luzAviso.range = 2.8f;
            luzAviso.intensity = 0f;
            luzAviso.enabled = false;
        }

        private void LimpiarMaterialesInstanciados()
        {
            foreach (var datos in renderersBrillo)
            {
                if (datos.renderer != null && datos.materialesOriginales != null)
                {
                    datos.renderer.sharedMaterials = datos.materialesOriginales;
                }

                if (datos.materialesConBrillo != null)
                {
                    foreach (var mat in datos.materialesConBrillo)
                    {
                        if (mat != null) Destroy(mat);
                    }
                }
            }
            renderersBrillo.Clear();
        }

        private void ActualizarEstadoBrillo(bool activar)
        {
            if (!brilloInicializado) InicializarBrillo();
            if (brilloActivo == activar) return;

            brilloActivo = activar;

            foreach (var datos in renderersBrillo)
            {
                if (datos.renderer == null) continue;
                datos.renderer.sharedMaterials = activar ? datos.materialesConBrillo : datos.materialesOriginales;
            }

            if (luzAviso != null)
            {
                luzAviso.enabled = activar;
                if (!activar) luzAviso.intensity = 0f;
            }
        }

        private void ActualizarPulsoBrillo()
        {
            if (!brilloActivo) return;

            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * velocidadPulso);
            float intensidad = Mathf.Lerp(intensidadEmisionMin, intensidadEmisionMax, t);
            Color emision = colorBrilloCobro * intensidad;

            foreach (var datos in renderersBrillo)
            {
                if (datos.materialesConBrillo == null) continue;

                foreach (var mat in datos.materialesConBrillo)
                {
                    if (mat == null || mat == datos.materialContorno) continue;
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", emision);
                    }
                }

                if (datos.materialContorno != null && datos.materialContorno.HasProperty("_OutlineColor"))
                {
                    Color cContorno = colorBrilloCobro * Mathf.Lerp(0.85f, 1.35f, t);
                    cContorno.a = 1f;
                    datos.materialContorno.SetColor("_OutlineColor", cContorno);
                }
            }

            if (luzAviso != null)
            {
                luzAviso.intensity = Mathf.Lerp(0.75f, 1.9f, t);
            }
        }

        private void Update()
        {
            if (!eventosSuscritos) SuscribirEventosTienda();

            bool debeBrillar = HayCobroPendiente();
            if (debeBrillar != brilloActivo)
            {
                ActualizarEstadoBrillo(debeBrillar);
            }
            if (brilloActivo)
            {
                ActualizarPulsoBrillo();
            }

            if (EsInteraccionBloqueada() || (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta))
            {
                estaMirandoCacheado = false;
                return;
            }

            estaMirandoCacheado = EstaMirandoLaPantalla(out float dist, out float angulo);

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log($"<color=yellow>[Terminal]</color> Tecla [E] presionada. Distancia: {dist:F2}m (Máx: {DistanciaInteraccion:F2}m) | Ángulo: {angulo:F2} | ¿Válido?: {estaMirandoCacheado}");

                if (estaMirandoCacheado)
                {
                    AbrirTerminal();
                }
            }
        }

        public bool EstaMirandoLaPantalla(out float distancia, out float anguloVision)
        {
            distancia = 999f;
            anguloVision = -1f;

            if (camaraJugador == null) BuscarCamara();
            if (camaraJugador == null) return false;

            float rangoEfectivo = DistanciaInteraccion;
            Transform camT = camaraJugador.transform;
            Vector3 centroReal = ObtenerCentroReal();

            Vector3 diff = centroReal - camT.position;
            float sqrDist = diff.sqrMagnitude;
            if (sqrDist > rangoEfectivo * rangoEfectivo) return false;

            distancia = Mathf.Sqrt(sqrDist);

            // 1. Ángulo de visión amplio hacia el monitor
            Vector3 direccionHaciaPC = distancia > 0.0001f ? diff / distancia : camT.forward;
            anguloVision = Vector3.Dot(camT.forward, direccionHaciaPC);

            if (anguloVision > UmbralAnguloVision)
            {
                return true;
            }

            // 2. SphereCast amplio para que la pantalla sea cómoda de accionar
            Ray ray = new Ray(camT.position, camT.forward);
            int hitCount = Physics.SphereCastNonAlloc(ray, RadioSphereCast, hitsBuffer, rangoEfectivo, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitT = hitsBuffer[i].transform;
                if (hitT == transform || hitT.IsChildOf(transform) || transform.IsChildOf(hitT))
                {
                    return true;
                }
            }

            return false;
        }

        private bool EsInteraccionBloqueada()
        {
            if (Mundo1.CinematicaGerente.Instance != null && Mundo1.CinematicaGerente.Instance.EstaEnCinematica)
            {
                return true;
            }

            if (scriptMovimiento == null)
            {
                scriptMovimiento = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            }

            // Si el jugador tiene el movimiento desactivado por una cinemática o transición, bloquear la PC
            if (scriptMovimiento != null && !scriptMovimiento.enabled)
            {
                return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!estaMirandoCacheado || EsInteraccionBloqueada()) return;
            if (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta) return;

            if (estiloGUI == null)
            {
                estiloGUI = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };
            }

            bool listoCobrar = HayCobroPendiente();
            estiloGUI.normal.textColor = listoCobrar
                ? new Color(0.35f, 1f, 0.65f, 1f)
                : new Color(1f, 0.95f, 0.55f, 1f);

            GUI.Label(new Rect(Screen.width / 2f - 280, Screen.height / 2f + 28, 560, 44), MensajeInteraccion, estiloGUI);
        }

        public void AbrirTerminal()
        {
            if (EsInteraccionBloqueada()) return;

            var ui = CajaRegistradoraUI.Instance;
            if (ui == null) ui = FindAnyObjectByType<CajaRegistradoraUI>(FindObjectsInactive.Include);

            if (ui != null)
            {
                Debug.Log("<color=green>[Terminal]</color> ¡Abriendo interfaz de terminal con éxito!");
                ui.AbrirCaja();
            }
            else
            {
                Debug.LogError("<color=red>[Terminal ERROR]</color> No se encontró 'CajaRegistradoraUI' en la escena. Asegúrate de tener el objeto '-- SISTEMA TIENDA --' con el componente CajaRegistradoraUI.");
            }
        }

        private void OnDrawGizmos()
        {
            Vector3 centro = ObtenerCentroReal();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centro, 0.85f);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireSphere(centro, DistanciaInteraccion);
        }
    }
}
