using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tienda.SistemaVentas
{
    /// <summary>
    /// Gestiona el HUD permanente en pantalla que muestra el dinero actual en la caja registradora.
    /// Incluye animaciones de rebote (pop) y texto flotante (+$$$) cada vez que se concreta una venta.
    /// </summary>
    public class HUDTienda : MonoBehaviour
    {
        public static HUDTienda Instance { get; private set; }

        [Header("Configuración Visual")]
        [SerializeField] private Vector2 posicionEnPantalla = new Vector2(-25, -25); // Esquina superior derecha
        [SerializeField] private Vector2 tamanoBadge = new Vector2(250, 64);

        private GameObject canvasObj;
        private Canvas canvas;
        private RectTransform panelBadge;
        private Text textoDinero;
        private Text textoTitulo;
        private Text textoFlotanteCambio;
        private Font fuenteUI;
        private float ultimoDinero = 100f;
        private Coroutine rutinaAnimacion;
        private Coroutine rutinaFlotante;

        private static Sprite spriteBordeRedondeado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializar()
        {
            if (Instance == null && FindObjectOfType<HUDTienda>() == null)
            {
                GameObject hudObj = new GameObject("HUD_Tienda_Auto", typeof(HUDTienda));
                if (Application.isPlaying) DontDestroyOnLoad(hudObj);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConstruirHUDProcedural();
        }

        private void Start()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado += ActualizarDineroCaja;
                ultimoDinero = TiendaManager.Instance.DineroEnCaja;
                ActualizarTextoVisual(ultimoDinero);
            }
        }

        private void OnDestroy()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnDineroCajaModificado -= ActualizarDineroCaja;
            }
        }

        private void ConstruirHUDProcedural()
        {
            // 1. Canvas ScreenSpaceOverlay
            canvasObj = new GameObject("Canvas_HUD_Tienda");
            canvasObj.transform.SetParent(transform, false);

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Por debajo del modal de la caja registradora (999)

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Sprite spriteRedondo = ObtenerSpriteRedondeado();

            // 2. Contenedor Badge en esquina superior derecha
            GameObject badgeObj = new GameObject("Badge_CajaRegistradora");
            badgeObj.transform.SetParent(canvasObj.transform, false);

            panelBadge = badgeObj.AddComponent<RectTransform>();
            panelBadge.anchorMin = new Vector2(1, 1);
            panelBadge.anchorMax = new Vector2(1, 1);
            panelBadge.pivot = new Vector2(1, 1);
            panelBadge.anchoredPosition = posicionEnPantalla;
            panelBadge.sizeDelta = tamanoBadge;

            // Fondo translúcido elegante estilo Kiosk/Glassmorphism
            var imgFondo = badgeObj.AddComponent<Image>();
            imgFondo.sprite = spriteRedondo;
            imgFondo.type = Image.Type.Sliced;
            imgFondo.color = new Color(0.07f, 0.09f, 0.12f, 0.88f);
            imgFondo.raycastTarget = false;

            // Borde / Acento decorativo sutil
            GameObject bordeObj = new GameObject("Borde_Acento");
            bordeObj.transform.SetParent(badgeObj.transform, false);
            var rectBorde = bordeObj.AddComponent<RectTransform>();
            rectBorde.anchorMin = Vector2.zero;
            rectBorde.anchorMax = Vector2.one;
            rectBorde.sizeDelta = Vector2.zero;
            var imgBorde = bordeObj.AddComponent<Image>();
            imgBorde.sprite = spriteRedondo;
            imgBorde.type = Image.Type.Sliced;
            imgBorde.color = new Color(0.25f, 0.80f, 0.45f, 0.25f);
            imgBorde.raycastTarget = false;

            // 3. Icono de Dinero / Billete a la izquierda
            GameObject iconObj = new GameObject("Icono_Dinero");
            iconObj.transform.SetParent(badgeObj.transform, false);
            var rectIcon = iconObj.AddComponent<RectTransform>();
            rectIcon.anchorMin = new Vector2(0, 0.5f);
            rectIcon.anchorMax = new Vector2(0, 0.5f);
            rectIcon.pivot = new Vector2(0.5f, 0.5f);
            rectIcon.anchoredPosition = new Vector2(34, 0);
            rectIcon.sizeDelta = new Vector2(44, 44);

            var txtIcon = iconObj.AddComponent<Text>();
            txtIcon.font = fuenteUI;
            txtIcon.text = "💵";
            txtIcon.fontSize = 28;
            txtIcon.alignment = TextAnchor.MiddleCenter;
            txtIcon.color = new Color(0.40f, 0.95f, 0.60f);
            txtIcon.raycastTarget = false;

            // 4. Subtítulo "CAJA REGISTRADORA"
            GameObject tituloObj = new GameObject("Texto_Titulo");
            tituloObj.transform.SetParent(badgeObj.transform, false);
            var rectTitulo = tituloObj.AddComponent<RectTransform>();
            rectTitulo.anchorMin = new Vector2(0, 1);
            rectTitulo.anchorMax = new Vector2(1, 1);
            rectTitulo.pivot = new Vector2(0, 1);
            rectTitulo.anchoredPosition = new Vector2(62, -10);
            rectTitulo.sizeDelta = new Vector2(-70, 18);

            textoTitulo = tituloObj.AddComponent<Text>();
            textoTitulo.font = fuenteUI;
            textoTitulo.text = "CAJA REGISTRADORA";
            textoTitulo.fontSize = 12;
            textoTitulo.alignment = TextAnchor.MiddleLeft;
            textoTitulo.fontStyle = FontStyle.Bold;
            textoTitulo.color = new Color(0.65f, 0.72f, 0.80f, 0.85f);
            textoTitulo.raycastTarget = false;

            // 5. Total de Dinero principal "$ 100.00"
            GameObject totalObj = new GameObject("Texto_Total");
            totalObj.transform.SetParent(badgeObj.transform, false);
            var rectTotal = totalObj.AddComponent<RectTransform>();
            rectTotal.anchorMin = new Vector2(0, 0);
            rectTotal.anchorMax = new Vector2(1, 0);
            rectTotal.pivot = new Vector2(0, 0);
            rectTotal.anchoredPosition = new Vector2(62, 10);
            rectTotal.sizeDelta = new Vector2(-70, 30);

            textoDinero = totalObj.AddComponent<Text>();
            textoDinero.font = fuenteUI;
            textoDinero.text = "$ 100.00";
            textoDinero.fontSize = 22;
            textoDinero.fontStyle = FontStyle.Bold;
            textoDinero.alignment = TextAnchor.MiddleLeft;
            textoDinero.color = new Color(0.30f, 0.95f, 0.50f); // Verde dinero nítido
            textoDinero.raycastTarget = false;

            // 6. Texto Flotante "+$XX.XX" que aparece al vender
            GameObject flotanteObj = new GameObject("Texto_Flotante_Venta");
            flotanteObj.transform.SetParent(badgeObj.transform, false);
            var rectFlotante = flotanteObj.AddComponent<RectTransform>();
            rectFlotante.anchorMin = new Vector2(0.5f, 0f);
            rectFlotante.anchorMax = new Vector2(0.5f, 0f);
            rectFlotante.pivot = new Vector2(0.5f, 1f);
            rectFlotante.anchoredPosition = new Vector2(20, -6);
            rectFlotante.sizeDelta = new Vector2(180, 28);

            textoFlotanteCambio = flotanteObj.AddComponent<Text>();
            textoFlotanteCambio.font = fuenteUI;
            textoFlotanteCambio.text = "";
            textoFlotanteCambio.fontSize = 18;
            textoFlotanteCambio.fontStyle = FontStyle.Bold;
            textoFlotanteCambio.alignment = TextAnchor.MiddleCenter;
            textoFlotanteCambio.color = new Color(0.35f, 1f, 0.55f, 0f);
            textoFlotanteCambio.raycastTarget = false;
        }

        public void ActualizarDineroCaja(float nuevoMonto)
        {
            float diferencia = nuevoMonto - ultimoDinero;
            ultimoDinero = nuevoMonto;

            ActualizarTextoVisual(nuevoMonto);

            // Si hubo cambio positivo o negativo, disparar efectos visuales
            if (Mathf.Abs(diferencia) > 0.001f)
            {
                DispararAnimacionBadge();
                DispararTextoFlotante(diferencia);
            }
        }

        private void ActualizarTextoVisual(float monto)
        {
            if (textoDinero != null)
            {
                textoDinero.text = $"$ {monto:F2}";
            }
        }

        private void DispararAnimacionBadge()
        {
            if (panelBadge == null) return;
            if (rutinaAnimacion != null) StopCoroutine(rutinaAnimacion);
            rutinaAnimacion = StartCoroutine(RutinaPopBadge());
        }

        private IEnumerator RutinaPopBadge()
        {
            Vector3 escalaOriginal = Vector3.one;
            float tiempo = 0f;
            float duracion = 0.22f;

            while (tiempo < duracion)
            {
                tiempo += Time.unscaledDeltaTime;
                float t = tiempo / duracion;
                float factor = 1f + Mathf.Sin(t * Mathf.PI) * 0.14f; // Rebote 1.14x
                panelBadge.localScale = escalaOriginal * factor;
                yield return null;
            }

            panelBadge.localScale = escalaOriginal;
        }

        private void DispararTextoFlotante(float diferencia)
        {
            if (textoFlotanteCambio == null) return;
            if (rutinaFlotante != null) StopCoroutine(rutinaFlotante);
            rutinaFlotante = StartCoroutine(RutinaFlotante(diferencia));
        }

        private IEnumerator RutinaFlotante(float diferencia)
        {
            bool esPositivo = diferencia > 0f;
            string signo = esPositivo ? "+" : "-";
            Color colorBase = esPositivo ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.35f, 0.35f);

            textoFlotanteCambio.text = $"{signo}${Mathf.Abs(diferencia):F2}";

            RectTransform rect = textoFlotanteCambio.GetComponent<RectTransform>();
            Vector2 posInicial = new Vector2(20, -6);
            Vector2 posFinal = new Vector2(20, -32); // Desciende suavemente

            float tiempo = 0f;
            float duracion = 1.3f;

            while (tiempo < duracion)
            {
                tiempo += Time.unscaledDeltaTime;
                float t = tiempo / duracion;

                rect.anchoredPosition = Vector2.Lerp(posInicial, posFinal, t);

                // Fade out en la segunda mitad
                float alfa = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                textoFlotanteCambio.color = new Color(colorBase.r, colorBase.g, colorBase.b, alfa);

                yield return null;
            }

            textoFlotanteCambio.text = "";
            textoFlotanteCambio.color = new Color(colorBase.r, colorBase.g, colorBase.b, 0f);
        }

        /// <summary>
        /// Genera proceduralmente un Sprite redondeado de alta calidad para paneles y badges UI.
        /// </summary>
        public static Sprite ObtenerSpriteRedondeado(int ancho = 64, int alto = 64, int radio = 14)
        {
            if (spriteBordeRedondeado != null) return spriteBordeRedondeado;

            Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixeles = new Color32[ancho * alto];

            for (int y = 0; y < alto; y++)
            {
                for (int x = 0; x < ancho; x++)
                {
                    // Determinar distancia al borde redondeado más cercano
                    float dx = Mathf.Max(0, Mathf.Max(radio - x, x - (ancho - 1 - radio)));
                    float dy = Mathf.Max(0, Mathf.Max(radio - y, y - (alto - 1 - radio)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alfa = Mathf.Clamp01(radio - dist + 0.5f);
                    pixeles[y * ancho + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
                }
            }

            tex.SetPixels32(pixeles);
            tex.Apply();

            // Bordes 9-slice para que escale a cualquier resolución sin deformar esquinas
            Vector4 borders = new Vector4(radio, radio, radio, radio);
            spriteBordeRedondeado = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borders);
            return spriteBordeRedondeado;
        }
    }
}
