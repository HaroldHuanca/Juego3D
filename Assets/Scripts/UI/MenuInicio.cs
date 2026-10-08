using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menú independiente del escenario de juego; no crea ni reproduce audio.</summary>
public sealed class MenuInicio : MonoBehaviour
{
    [SerializeField] private string primerEscenario = "Assets/Scenes/SampleScene.unity";
    [SerializeField] private string titulo = "Closing Time";
    [SerializeField, Range(0f, 1f)] private float intensidadParpadeo = 0.12f;

    private readonly Color marfil = new Color(0.86f, 0.85f, 0.79f);
    private readonly Color acento = new Color(0.65f, 0.26f, 0.22f);
    private GameObject inicio;
    private GameObject creditos;
    private Button jugar;
    private Button regresar;
    private Text textoJugar;
    private Text textoTitulo;
    private Text estado;
    private Image sombra;
    private RawImage fondo;
    private RawImage grano;
    private Texture2D texturaGrano;
    private Texture2D texturaVineta;
    private Font fuente;
    private Font fuenteTerror;
    private bool cargando;
    private float siguienteParpadeo;
    private float finParpadeo;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fuenteTerror = Resources.Load<Font>("MainMenu/Creepster-Regular");
        CrearInterfaz();
        siguienteParpadeo = Time.unscaledTime + 3f;
    }

    private void Start() => EventSystem.current.SetSelectedGameObject(jugar.gameObject);

    private void Update()
    {
        float tiempo = Time.unscaledTime;
        if (tiempo >= siguienteParpadeo)
        {
            finParpadeo = tiempo + Random.Range(0.16f, 0.34f);
            siguienteParpadeo = tiempo + Random.Range(4f, 8f);
        }

        // Caídas breves de luz, sin destellos blancos ni interrupciones de los botones.
        float pulso = tiempo < finParpadeo ? Mathf.Abs(Mathf.Sin(tiempo * 43f)) : 0f;
        sombra.color = new Color(0f, 0f, 0f, 0.08f + pulso * intensidadParpadeo);
        textoTitulo.color = Color.Lerp(marfil, marfil * new Color(0.75f, 0.75f, 0.75f, 1f), pulso);
        fondo.rectTransform.localScale = Vector3.one * (1.035f + Mathf.Sin(tiempo * 0.16f) * 0.005f);
        grano.uvRect = new Rect(tiempo * 0.021f, tiempo * 0.017f, 8f, 5f);

        if (!cargando && creditos.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            MostrarInicio();
    }

    private void CrearInterfaz()
    {
        var canvasObject = new GameObject("Menú de inicio", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        RectTransform pantalla = canvasObject.GetComponent<RectTransform>();
        fondo = Elemento("Fondo IA", pantalla).gameObject.AddComponent<RawImage>();
        Estirar(fondo.rectTransform);
        fondo.texture = Resources.Load<Texture2D>("MainMenu/HorrorBackground");
        fondo.color = fondo.texture != null ? Color.white : new Color(0.07f, 0.08f, 0.07f);
        fondo.raycastTarget = false;
        if (fondo.texture != null)
        {
            var aspect = fondo.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = (float)fondo.texture.width / fondo.texture.height;
        }

        sombra = Placa("Luz intermitente", pantalla, Color.black);
        Estirar(sombra.rectTransform);
        var vineta = Elemento("Viñeta", pantalla).gameObject.AddComponent<RawImage>();
        Estirar(vineta.rectTransform);
        texturaVineta = CrearVineta();
        vineta.texture = texturaVineta;
        vineta.raycastTarget = false;

        grano = Elemento("Grano de película", pantalla).gameObject.AddComponent<RawImage>();
        Estirar(grano.rectTransform);
        texturaGrano = CrearGrano();
        grano.texture = texturaGrano;
        grano.color = new Color(1f, 1f, 1f, 0.007f);
        grano.raycastTarget = false;

        var contenido = Elemento("Contenido", pantalla);
        contenido.anchorMin = contenido.anchorMax = new Vector2(0.075f, 0.5f);
        contenido.pivot = new Vector2(0f, 0.5f);
        contenido.sizeDelta = new Vector2(680f, 820f);

        inicio = Elemento("Inicio", contenido).gameObject;
        Estirar((RectTransform)inicio.transform);
        Etiqueta("Sección", inicio.transform, "01  /  INICIO", 17, new Vector2(0, -25), new Vector2(600, 30), acento);
        textoTitulo = Etiqueta("Título", inicio.transform, titulo, 96, new Vector2(-3, -85), new Vector2(670, 260), marfil, true);
        var linea = Placa("Línea de acento", inicio.transform, acento);
        Posicionar(linea.rectTransform, new Vector2(0, -345), new Vector2(64, 2));
        jugar = CrearBoton(inicio.transform, "Jugar", "JUGAR", -402f, Jugar);
        textoJugar = jugar.GetComponentInChildren<Text>();
        // Se muestra como adorno y se omite en la navegación del teclado.
        var opciones = CrearBoton(inicio.transform, "Opciones", "OPCIONES", -486f, null);
        opciones.interactable = false;
        CrearBoton(inicio.transform, "Créditos", "CRÉDITOS", -570f, MostrarCreditos);
        CrearBoton(inicio.transform, "Salir", "SALIR", -654f, Salir);
        estado = Etiqueta("Estado", inicio.transform, "", 18, new Vector2(0, -760), new Vector2(660, 45), marfil);

        creditos = Elemento("Créditos", contenido).gameObject;
        Estirar((RectTransform)creditos.transform);
        Etiqueta("Sección", creditos.transform, "02  /  EL EQUIPO", 17, new Vector2(0, -25), new Vector2(650, 30), acento);
        Etiqueta("Título créditos", creditos.transform, "CRÉDITOS", 90, new Vector2(-3, -105), new Vector2(670, 120), marfil, true);
        Etiqueta("Desarrolladores", creditos.transform, "DESARROLLADORES", 19, new Vector2(0, -290), new Vector2(650, 35), acento);
        string[] nombres = { "Angelo Ccama Condori", "Harold Huanca Ccasa", "Jose Leonardo Cruz Gonzales", "Jorge Salvador Rodrigo Chipa" };
        for (int i = 0; i < nombres.Length; i++)
            Etiqueta("Desarrollador " + (i + 1), creditos.transform, nombres[i], 29, new Vector2(0, -358 - i * 65), new Vector2(670, 45), marfil);
        regresar = CrearBoton(creditos.transform, "Regresar", "REGRESAR AL INICIO", -680f, MostrarInicio);
        creditos.SetActive(false);

        var pie = Etiqueta("Pie", pantalla, "EN DESARROLLO", 15, Vector2.zero, new Vector2(300, 30), new Color(0.52f, 0.53f, 0.48f));
        pie.rectTransform.anchorMin = pie.rectTransform.anchorMax = new Vector2(0.075f, 0f);
        pie.rectTransform.pivot = Vector2.zero;
        pie.rectTransform.anchoredPosition = new Vector2(0f, 30f);
    }

    public void MostrarCreditos()
    {
        if (cargando) return;
        inicio.SetActive(false);
        creditos.SetActive(true);
        EventSystem.current.SetSelectedGameObject(regresar.gameObject);
    }

    public void MostrarInicio()
    {
        creditos.SetActive(false);
        inicio.SetActive(true);
        EventSystem.current.SetSelectedGameObject(jugar.gameObject);
    }

    public void Jugar()
    {
        if (cargando) return;
        if (!Application.CanStreamedLevelBeLoaded(primerEscenario))
        {
            estado.text = "No se pudo encontrar el primer escenario.";
            Debug.LogError("Agrega el escenario a Build Profiles: " + primerEscenario);
            return;
        }
        StartCoroutine(CargarEscenario());
    }

    private IEnumerator CargarEscenario()
    {
        cargando = true;
        textoJugar.text = "CARGANDO...";
        foreach (var boton in inicio.GetComponentsInChildren<Button>()) boton.interactable = false;
        // Permite dibujar el indicador antes de iniciar la carga.
        yield return null;
        yield return SceneManager.LoadSceneAsync(primerEscenario, LoadSceneMode.Single);
    }

    public void Salir()
    {
        if (cargando) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Button CrearBoton(Transform parent, string nombre, string texto, float y, UnityEngine.Events.UnityAction accion)
    {
        var placa = Placa(nombre, parent, new Color(0.12f, 0.11f, 0.10f, 0.18f));
        placa.raycastTarget = true;
        Posicionar(placa.rectTransform, new Vector2(0f, y), new Vector2(570f, 68f));
        var boton = placa.gameObject.AddComponent<Button>();
        boton.targetGraphic = placa;
        var colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = new Color(2.8f, 1.6f, 1.3f, 1f);
        colores.selectedColor = colores.highlightedColor;
        colores.pressedColor = new Color(4f, 1.7f, 1.3f, 1f);
        colores.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.25f);
        colores.fadeDuration = 0.12f;
        boton.colors = colores;
        var etiqueta = Etiqueta("Texto", placa.transform, texto, 31, new Vector2(22f, -12f), new Vector2(530f, 44f), accion == null ? new Color(0.40f, 0.41f, 0.38f) : marfil);
        etiqueta.alignment = TextAnchor.MiddleLeft;
        if (accion != null) boton.onClick.AddListener(accion);
        return boton;
    }

    private Text Etiqueta(string nombre, Transform parent, string texto, int size, Vector2 position, Vector2 dimensions, Color color, bool terror = false)
    {
        var rect = Elemento(nombre, parent);
        Posicionar(rect, position, dimensions);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = terror && fuenteTerror != null ? fuenteTerror : fuente;
        label.text = texto;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = terror ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        label.lineSpacing = terror ? 0.9f : 1f;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Elemento(string nombre, Transform parent)
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image Placa(string nombre, Transform parent, Color color)
    {
        var image = Elemento(nombre, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Posicionar(RectTransform rect, Vector2 position, Vector2 dimensions)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
    }

    private static void Estirar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Texture2D CrearGrano()
    {
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        var pixels = new Color32[128 * 128];
        var random = new System.Random(17);
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = (byte)random.Next(30, 225);
            pixels[i] = new Color32(value, value, value, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CrearVineta()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x / (size - 1f) - 0.5f) * 1.6f, (y / (size - 1f) - 0.5f) * 1.6f).magnitude;
                pixels[y * size + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.62f, radius));
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (texturaGrano != null) Destroy(texturaGrano);
        if (texturaVineta != null) Destroy(texturaVineta);
    }
}
