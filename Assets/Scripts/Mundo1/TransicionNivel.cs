using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mundo1
{
    /// <summary>
    /// Maneja la transición cinemática hacia el siguiente nivel (pantalla a negro,
    /// textos narrativos de conclusión y carga asíncrona de la siguiente escena).
    /// </summary>
    [DisallowMultipleComponent]
    public class TransicionNivel : MonoBehaviour
    {
        public static TransicionNivel Instance { get; private set; }

        [Header("Configuración de Escena")]
        [Tooltip("Nombre de la siguiente escena a cargar (ej. NIvelHarold)")]
        [SerializeField] private string siguienteEscena = "NIvelHarold";

        [Header("Tiempos y Mensajes")]
        [SerializeField] private float duracionFundido = 2.0f;
        [SerializeField] private string textoVictoria = "NOCHE 1: LA DESAPARICIÓN\n\nAlgo te arrastró hacia las profundidades...";
        [SerializeField] private string textoColapsoCordura = "COLAPSO MENTAL\n\nTu mente no resistió el terror...\nDespiertas en un lugar desconocido.";

        private CanvasGroup canvasGroupFundido;
        private Text textoMensajeFinal;
        private bool transicionIniciada = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ConstruirPantallaNegro();
        }

        public void IniciarTransicionSiguienteNivel(bool porColapsoCordura = false)
        {
            if (transicionIniciada) return;
            transicionIniciada = true;

            string mensaje = porColapsoCordura ? textoColapsoCordura : textoVictoria;
            StartCoroutine(RutinaTransicion(mensaje));
        }

        private IEnumerator RutinaTransicion(string mensaje)
        {
            if (textoMensajeFinal != null)
            {
                textoMensajeFinal.text = mensaje;
            }

            // 1. Fundido gradual a negro
            float transcurrido = 0f;
            while (transcurrido < duracionFundido)
            {
                transcurrido += Time.unscaledDeltaTime;
                if (canvasGroupFundido != null)
                {
                    canvasGroupFundido.alpha = Mathf.Clamp01(transcurrido / duracionFundido);
                }
                yield return null;
            }

            if (canvasGroupFundido != null) canvasGroupFundido.alpha = 1f;

            // 2. Pausa con el texto en pantalla para que el jugador asimile el clímax
            yield return new WaitForSecondsRealtime(3.5f);

            // 3. Cargar la siguiente escena (priorizando ConfiguracionSiguienteMapa si existe en escena)
            string escenaObjetivo = siguienteEscena;
            if (ConfiguracionSiguienteMapa.Instance != null && !string.IsNullOrWhiteSpace(ConfiguracionSiguienteMapa.Instance.NombreSiguienteEscena))
            {
                escenaObjetivo = ConfiguracionSiguienteMapa.Instance.NombreSiguienteEscena.Trim();
            }

            if (Application.CanStreamedLevelBeLoaded(escenaObjetivo))
            {
                Debug.Log($"<color=cyan>[Transición] Cargando siguiente escena: '{escenaObjetivo}'</color>");
                SceneManager.LoadSceneAsync(escenaObjetivo);
            }
            else
            {
                Debug.LogWarning($"[Transición] La escena '{escenaObjetivo}' no está agregada a Build Settings. Reiniciando escena actual.");
                SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void ConstruirPantallaNegro()
        {
            var canvasObj = new GameObject("Canvas_Transicion", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Por encima de todo

            var panelNegro = new GameObject("Panel_Negro", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelNegro.transform.SetParent(canvasObj.transform, false);
            var rt = panelNegro.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = panelNegro.GetComponent<Image>();
            img.color = Color.black;

            canvasGroupFundido = panelNegro.GetComponent<CanvasGroup>();
            canvasGroupFundido.alpha = 0f;
            canvasGroupFundido.blocksRaycasts = false;

            // Texto en pantalla
            var goTxt = new GameObject("Texto_Final", typeof(RectTransform), typeof(Text));
            goTxt.transform.SetParent(panelNegro.transform, false);
            var rtTxt = goTxt.GetComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.sizeDelta = new Vector2(-120f, -120f);

            textoMensajeFinal = goTxt.GetComponent<Text>();
            textoMensajeFinal.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoMensajeFinal.fontSize = 24;
            textoMensajeFinal.fontStyle = FontStyle.Bold;
            textoMensajeFinal.alignment = TextAnchor.MiddleCenter;
            textoMensajeFinal.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
            textoMensajeFinal.text = "";
        }
    }
}
