using UnityEngine;
using UnityEngine.InputSystem;

namespace Tienda.SistemaVentas
{
    public class TerminalCobroInteractuable : MonoBehaviour
    {
        [Header("Configuración de Interacción")]
        [Tooltip("Distancia máxima a la que el jugador puede estar de la PC")]
        [SerializeField] private float distanciaInteraccion = 4.5f;

        [Tooltip("Texto que aparece en pantalla al apuntar al monitor de la PC")]
        [SerializeField] private string mensajeInteraccion = "E · Cobrar en Terminal";

        private Camera camaraJugador;
        private GUIStyle estiloGUI;

        public float DistanciaInteraccion => distanciaInteraccion;
        public string MensajeInteraccion => mensajeInteraccion;

        private void Start()
        {
            BuscarCamara();
            Vector3 centro = ObtenerCentroReal();
            string nombreCam = camaraJugador != null ? camaraJugador.name : "Ninguna";
            Debug.Log($"<color=cyan>[Terminal]</color> Inicializada en '{gameObject.name}'. Centro geométrico: {centro}. Cámara detectada: {nombreCam}");
        }

        private void BuscarCamara()
        {
            // 1. Buscar la cámara principal del jugador que tiene el script Camara.cs
            Camara scriptCam = FindObjectOfType<Camara>();
            if (scriptCam != null)
            {
                camaraJugador = scriptCam.GetComponent<Camera>();
            }

            // 2. Si no se encuentra, usar Camera.main
            if (camaraJugador == null)
            {
                camaraJugador = Camera.main;
            }

            // 3. Fallback: buscar cualquier cámara que no sea la de brazos
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
            var rend = GetComponent<Renderer>();
            if (rend != null) return rend.bounds.center;

            var col = GetComponent<Collider>();
            if (col != null) return col.bounds.center;

            var rendHijo = GetComponentInChildren<Renderer>();
            if (rendHijo != null) return rendHijo.bounds.center;

            return transform.position;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Si la caja ya está abierta, no procesar aquí (CajaRegistradoraUI la cierra)
            if (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta)
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                // Imprimir diagnóstico exacto en la consola al presionar E
                bool mirando = EstaMirandoLaPantalla(out float dist, out float angulo);
                Debug.Log($"<color=yellow>[Terminal]</color> Tecla [E] presionada. Distancia: {dist:F2}m (Máx: {distanciaInteraccion}m) | Ángulo: {angulo:F2} | ¿Válido?: {mirando}");

                if (mirando)
                {
                    AbrirTerminal();
                }
            }
        }

        public bool EstaMirandoLaPantalla()
        {
            return EstaMirandoLaPantalla(out _, out _);
        }

        public bool EstaMirandoLaPantalla(out float distancia, out float anguloVision)
        {
            distancia = 999f;
            anguloVision = -1f;

            if (camaraJugador == null) BuscarCamara();
            if (camaraJugador == null) return false;

            Transform camT = camaraJugador.transform;
            Vector3 centroReal = ObtenerCentroReal();

            distancia = Vector3.Distance(camT.position, centroReal);
            if (distancia > distanciaInteraccion) return false;

            // 1. Ángulo de visión hacia el monitor
            Vector3 direccionHaciaPC = (centroReal - camT.position).normalized;
            anguloVision = Vector3.Dot(camT.forward, direccionHaciaPC);

            // Si está dentro de la distancia y mirando en dirección al monitor
            if (anguloVision > 0.35f)
            {
                return true;
            }

            // 2. Chequeo por Raycast
            Ray ray = new Ray(camT.position, camT.forward);
            RaycastHit[] impactos = Physics.RaycastAll(ray, distanciaInteraccion);
            foreach (var impacto in impactos)
            {
                if (impacto.transform == transform || impacto.transform.IsChildOf(transform) || transform.IsChildOf(impacto.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnGUI()
        {
            if (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta) return;

            if (EstaMirandoLaPantalla())
            {
                if (estiloGUI == null)
                {
                    estiloGUI = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.95f, 0.4f, 1f) }
                    };
                }

                GUI.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f + 30, 360, 35), mensajeInteraccion, estiloGUI);
            }
        }

        public void AbrirTerminal()
        {
            var ui = CajaRegistradoraUI.Instance;
            if (ui == null) ui = FindObjectOfType<CajaRegistradoraUI>();

            if (ui != null)
            {
                Debug.Log("<color=green>[Terminal]</color> ¡Abriendo interfaz de cobro con éxito!");
                ui.AbrirCaja();
            }
            else
            {
                Debug.LogError("<color=red>[Terminal ERROR]</color> No se encontró 'CajaRegistradoraUI' en la escena. Asegúrate de tener el objeto '-- SISTEMA TIENDA --' con el componente CajaRegistradoraUI.");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ObtenerCentroReal(), 0.35f);
        }
    }
}
