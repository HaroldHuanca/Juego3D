using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tienda.Clientes
{
    public class IconoFlotanteCliente : MonoBehaviour
    {
        [Header("Configuración Visual")]
        [Tooltip("Altura en metros por encima de los pies del personaje")]
        [SerializeField] private float alturaSobreCabeza = 2.12f;

        [Tooltip("Escala del icono flotante en el mundo")]
        [SerializeField] private float escalaIcono = 0.003f;

        private GameObject canvasObj;
        private Canvas canvas;
        private Image fondoBocadillo;
        private Image bordeBocadillo;
        private Text textoIcono;
        private Camera camaraPrincipal;
        private Coroutine rutinaAnimacion;

        private static Sprite spriteCirculo;

        private void Awake()
        {
            BuscarCamara();
            ConstruirUIFlotante();
            Ocultar();
        }

        private void BuscarCamara()
        {
            if (Camera.main != null) camaraPrincipal = Camera.main;
            else camaraPrincipal = FindObjectOfType<Camera>();
        }

        private void LateUpdate()
        {
            if (canvasObj == null || !canvasObj.activeSelf) return;

            if (camaraPrincipal == null)
            {
                BuscarCamara();
                if (camaraPrincipal == null) return;
            }

            // Billboard: Hacer que siempre mire exactamente de frente a la cámara del jugador
            canvasObj.transform.forward = camaraPrincipal.transform.forward;

            // Mantener la posición siempre fija sobre la cabeza del personaje
            canvasObj.transform.position = transform.position + (Vector3.up * alturaSobreCabeza);
        }

        private void ConstruirUIFlotante()
        {
            Sprite spriteRedondo = ObtenerSpriteCirculo();

            // 1. Crear Canvas en World Space
            canvasObj = new GameObject("Icono_Flotante_HeadUI");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = new Vector3(0, alturaSobreCabeza, 0);
            canvasObj.transform.localScale = Vector3.one * escalaIcono;

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 25;

            // 2. Borde exterior (Círculo con radio ligeramente mayor para dar contorno limpio)
            GameObject bordeObj = new GameObject("Borde_Contorno");
            bordeObj.transform.SetParent(canvasObj.transform, false);
            var rectBorde = bordeObj.AddComponent<RectTransform>();
            rectBorde.sizeDelta = new Vector2(52, 52);
            bordeBocadillo = bordeObj.AddComponent<Image>();
            bordeBocadillo.sprite = spriteRedondo;
            bordeBocadillo.color = new Color(1f, 1f, 1f, 0.4f);

            // 3. Fondo interior del Bocadillo
            GameObject fondoObj = new GameObject("Bocadillo_Fondo");
            fondoObj.transform.SetParent(bordeObj.transform, false);

            var rectFondo = fondoObj.AddComponent<RectTransform>();
            rectFondo.sizeDelta = new Vector2(46, 46);

            fondoBocadillo = fondoObj.AddComponent<Image>();
            fondoBocadillo.sprite = spriteRedondo;
            fondoBocadillo.color = new Color(0.08f, 0.10f, 0.12f, 0.90f);

            // 4. Texto/Icono central
            GameObject textoObj = new GameObject("Texto_Icono");
            textoObj.transform.SetParent(fondoObj.transform, false);

            var rectTexto = textoObj.AddComponent<RectTransform>();
            rectTexto.anchorMin = Vector2.zero;
            rectTexto.anchorMax = Vector2.one;
            rectTexto.offsetMin = rectTexto.offsetMax = Vector2.zero;

            textoIcono = textoObj.AddComponent<Text>();
            textoIcono.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoIcono.fontSize = 28;
            textoIcono.alignment = TextAnchor.MiddleCenter;
            textoIcono.fontStyle = FontStyle.Bold;
            textoIcono.color = Color.white;
            textoIcono.raycastTarget = false;
        }

        public void Ocultar()
        {
            if (canvasObj != null) canvasObj.SetActive(false);
            if (rutinaAnimacion != null) StopCoroutine(rutinaAnimacion);
        }

        /// <summary>
        /// Muestra un reloj de espera "···" mientras espera su turno en la fila.
        /// </summary>
        public void MostrarIconoEspera()
        {
            if (canvasObj == null) return;
            canvasObj.SetActive(true);

            textoIcono.text = "···";
            textoIcono.fontSize = 28;
            textoIcono.color = new Color(1f, 0.85f, 0.25f); // Ámbar brillante
            fondoBocadillo.color = new Color(0.08f, 0.10f, 0.12f, 0.90f);
            if (bordeBocadillo != null) bordeBocadillo.color = new Color(1f, 0.85f, 0.25f, 0.6f);

            IniciarAnimacionPop();
        }

        /// <summary>
        /// Muestra el icono de dinero "$" cuando llega a la caja y quiere pagar.
        /// </summary>
        public void MostrarIconoPago()
        {
            if (canvasObj == null) return;
            canvasObj.SetActive(true);

            textoIcono.text = "$";
            textoIcono.fontSize = 32;
            textoIcono.color = new Color(0.35f, 1f, 0.55f); // Verde dinero brillante
            fondoBocadillo.color = new Color(0.06f, 0.13f, 0.08f, 0.92f);
            if (bordeBocadillo != null) bordeBocadillo.color = new Color(0.35f, 1f, 0.55f, 0.7f);

            IniciarAnimacionPop();
        }

        /// <summary>
        /// Muestra un check verde "✓" cuando el cobro fue exitoso y da las gracias.
        /// </summary>
        public void MostrarIconoExito()
        {
            if (canvasObj == null) return;
            canvasObj.SetActive(true);

            textoIcono.text = "✓";
            textoIcono.fontSize = 30;
            textoIcono.color = Color.white;
            fondoBocadillo.color = new Color(0.12f, 0.65f, 0.25f, 0.95f); // Verde éxito vibrante
            if (bordeBocadillo != null) bordeBocadillo.color = new Color(0.4f, 1f, 0.6f, 0.9f);

            IniciarAnimacionPop();
        }

        private void IniciarAnimacionPop()
        {
            if (rutinaAnimacion != null) StopCoroutine(rutinaAnimacion);
            rutinaAnimacion = StartCoroutine(RutinaPopEscala());
        }

        private IEnumerator RutinaPopEscala()
        {
            Vector3 escalaOriginal = Vector3.one * escalaIcono;
            canvasObj.transform.localScale = escalaOriginal * 0.2f;

            float tiempo = 0f;
            float duracion = 0.25f;

            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float t = tiempo / duracion;
                // Efecto de rebote elástico (Overshoot)
                float factor = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.25f;
                if (t >= 0.8f) factor = Mathf.Lerp(1.25f, 1.0f, (t - 0.8f) / 0.2f);

                canvasObj.transform.localScale = escalaOriginal * factor;
                yield return null;
            }

            canvasObj.transform.localScale = escalaOriginal;
        }

        /// <summary>
        /// Genera proceduralmente un Sprite circular suave y anti-aliased para evitar esquinas cuadradas.
        /// </summary>
        public static Sprite ObtenerSpriteCirculo(int resolucion = 64)
        {
            if (spriteCirculo != null) return spriteCirculo;

            Texture2D tex = new Texture2D(resolucion, resolucion, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixeles = new Color32[resolucion * resolucion];
            float centro = (resolucion - 1) * 0.5f;
            float radio = centro - 1.5f;

            for (int y = 0; y < resolucion; y++)
            {
                for (int x = 0; x < resolucion; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                    float delta = radio - dist;
                    byte alpha = (byte)(Mathf.Clamp01(delta + 0.5f) * 255);
                    pixeles[y * resolucion + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixeles);
            tex.Apply();
            spriteCirculo = Sprite.Create(tex, new Rect(0, 0, resolucion, resolucion), new Vector2(0.5f, 0.5f), 100f);
            return spriteCirculo;
        }

        private void OnDestroy()
        {
            if (canvasObj != null)
            {
                Destroy(canvasObj);
            }
        }
    }
}
