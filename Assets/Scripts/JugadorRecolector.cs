using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;
using UnityEditor.Animations;

public class JugadorRecolector : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private Camera camara;
    [SerializeField] private float distanciaInteraccion = 2.5f;
    [SerializeField] private LayerMask capasDetectables = ~0;

    [Header("Sujeción")]
    [SerializeField] private Transform puntoSujecion;

    [Header("Indicador Visual")]
    [SerializeField] private bool mostrarIndicador = true;
    [Header("Sistema de Llenado")]
    [SerializeField] private float duracionLlenado = 5f;
    [SerializeField] private Slider barraLlenado;

    [Header("Animación de la Caja")]
    [SerializeField] private string nombreEstadoAnimacion = "Base Layer.Scene";
    [SerializeField] private float velocidadAnimacion = 2f;

    private bool estaLlenando = false;

    private CajaInteractuable cajaDetectada;
    private CajaInteractuable cajaSostenida;
    private GUIStyle estiloGUI;
    private ObjetoAleatorio objetoDetectado;

    private void Start()
    {
        if (barraLlenado != null)
        {
            barraLlenado.value = 0f;
            barraLlenado.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (camara == null)
            return;

        // Si ya tenemos una caja cargada: se suelta con la tecla [G]
        if (cajaSostenida != null)
        {
            if (!estaLlenando)
            {
                DetectarObjetoAleatorio();

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.gKey.wasPressedThisFrame)
                    {
                        SoltarCaja();
                    }

                    if (Keyboard.current.eKey.wasPressedThisFrame &&
                        objetoDetectado != null)
                    {
                        StartCoroutine(LlenarObjeto(objetoDetectado));
                    }
                }
            }

            return;
        }
        // Detectar si estamos apuntando a una caja
        DetectarCaja();

        // Si estamos apuntando a una caja: se recoge con CLICK DERECHO
        if (Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame &&
            cajaDetectada != null)
        {
            RecogerCaja();
        }
    }

    private void DetectarCaja()
    {
        Ray rayo = new Ray(
            camara.transform.position,
            camara.transform.forward
        );

        bool impactoDetectado = Physics.Raycast(
            rayo,
            out RaycastHit impacto,
            distanciaInteraccion,
            capasDetectables,
            QueryTriggerInteraction.Ignore
        );

        CajaInteractuable nuevaCaja = null;

        if (impactoDetectado)
        {
            nuevaCaja = impacto.collider.GetComponentInParent<CajaInteractuable>();
        }

        // Si la caja apuntada cambió
        if (nuevaCaja != cajaDetectada)
        {
            // Apagar brillo en la caja que dejamos de mirar
            if (cajaDetectada != null)
            {
                cajaDetectada.SetResaltado(false);
            }

            cajaDetectada = nuevaCaja;

            // Encender brillo en la nueva caja que estamos mirando
            if (cajaDetectada != null)
            {
                cajaDetectada.SetResaltado(true);
            }
        }
    }

    private void RecogerCaja()
    {
        if (cajaDetectada == null || puntoSujecion == null)
            return;

        // Apagar el brillo de interacción antes de cargarla
        cajaDetectada.SetResaltado(false);

        cajaSostenida = cajaDetectada;
        Animator animador = cajaSostenida.GetComponentInChildren<Animator>();

        MostrarEstadosAnimator(animador);
        cajaDetectada = null;

        Rigidbody rb = cajaSostenida.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Escala antes de cambiar la jerarquía.
        Debug.Log(
            $"[Caja] Escala mundial ANTES: " +
            $"{cajaSostenida.transform.lossyScale}"
        );

        Transform cajaTransform = cajaSostenida.transform;

        // Guardar escala mundial antes de cambiar de padre.
        Vector3 escalaOriginal = cajaTransform.lossyScale;

        // Vincular la caja al punto de sujeción.
        cajaTransform.SetParent(puntoSujecion, false);

        // Colocar la caja delante de la cámara.
        cajaTransform.localPosition = Vector3.zero;
        cajaTransform.localRotation = Quaternion.identity;

        // Ajustar la escala local para aproximar la escala mundial original.
        Vector3 escalaPadre = puntoSujecion.lossyScale;

        if (Mathf.Abs(escalaPadre.x) > 0.0001f &&
            Mathf.Abs(escalaPadre.y) > 0.0001f &&
            Mathf.Abs(escalaPadre.z) > 0.0001f)
        {
            cajaTransform.localScale = new Vector3(
                escalaOriginal.x / escalaPadre.x,
                escalaOriginal.y / escalaPadre.y,
                escalaOriginal.z / escalaPadre.z
            );
        }

        Debug.Log($"[Caja] Escala mundial final: {cajaTransform.lossyScale}");

        foreach (Collider col in
                 cajaSostenida.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }

    private void SoltarCaja()
    {
        if (cajaSostenida == null)
            return;

        CajaInteractuable caja = cajaSostenida;

        caja.transform.SetParent(null);

        foreach (Collider col in caja.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }

        Rigidbody rb = caja.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        cajaSostenida = null;
    }

    private void OnDisable()
    {
        if (cajaDetectada != null)
        {
            cajaDetectada.SetResaltado(false);
            cajaDetectada = null;
        }
    }

    private void OnGUI()
    {
        if (!mostrarIndicador || Cursor.lockState != CursorLockMode.Locked) return;

        string textoPrompt = "";

        if (cajaSostenida != null)
        {
            if (estaLlenando)
            {
                textoPrompt = "Llenando...";
            }
            else if (objetoDetectado != null)
            {
                textoPrompt = "E · Llenar";
            }
            else
            {
                textoPrompt = "G · Soltar Caja";
            }
        }
        else if (cajaDetectada != null)
        {
            textoPrompt = "Click Derecho · Recoger Caja";
        }

        if (!string.IsNullOrEmpty(textoPrompt))
        {
            if (estiloGUI == null)
            {
                estiloGUI = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.90f, 0.4f) }
                };
            }

            GUI.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f + 25, 360, 30), textoPrompt, estiloGUI);
        }
    }
    private void DetectarObjetoAleatorio()
    {
        Ray rayo = new Ray(
            camara.transform.position,
            camara.transform.forward
        );

        bool impactoDetectado = Physics.Raycast(
            rayo,
            out RaycastHit impacto,
            distanciaInteraccion,
            capasDetectables,
            QueryTriggerInteraction.Ignore
        );

        ObjetoAleatorio nuevoObjeto = null;

        if (impactoDetectado)
        {
            nuevoObjeto = impacto.collider
                .GetComponentInParent<ObjetoAleatorio>();
        }

        // Solo permitir rellenar objetos que estén ocultos.
        if (nuevoObjeto != null && nuevoObjeto.EstaVisible)
        {
            nuevoObjeto = null;
        }

        objetoDetectado = nuevoObjeto;
    }


    private IEnumerator LlenarObjeto(ObjetoAleatorio objetivo)
    {
        if (objetivo == null || estaLlenando)
            yield break;

        estaLlenando = true;

        // Obtener el Animator de la caja.
        Animator animador =
            cajaSostenida.GetComponentInChildren<Animator>();


        int estadoHash = Animator.StringToHash(nombreEstadoAnimacion);

        if (animador == null)
        {
            Debug.LogError("[Caja] No se encontró ningún Animator.");
        }
        else if (!animador.HasState(0, estadoHash))
        {
            Debug.LogError(
                "[Caja] No se encontró el estado: " +
                nombreEstadoAnimacion
            );

            animador = null;
        }

        // Mostrar barra.
        if (barraLlenado != null)
        {
            barraLlenado.value = 0f;
            barraLlenado.gameObject.SetActive(true);
        }

        float tiempoTranscurrido = 0f;

        // Comenzar con la caja cerrada.
        if (animador != null)
        {
            animador.Play(estadoHash, 0, 1f);
            animador.Update(0f);
        }

        while (tiempoTranscurrido < duracionLlenado)
        {
            if (objetivo == null || cajaSostenida == null)
                break;

            tiempoTranscurrido += Time.deltaTime;

            // Calcular progreso.
            float progreso = Mathf.Clamp01(
                tiempoTranscurrido / duracionLlenado
            );

            // Actualizar barra.
            if (barraLlenado != null)
            {
                barraLlenado.value = progreso;
            }

            // Alternar apertura y cierre.
            if (animador != null)
            {
                float tiempoAnimacion =
                    tiempoTranscurrido * velocidadAnimacion;

                float posicion = Mathf.PingPong(
                    tiempoAnimacion, 1f
                );

                // La animación original va de abierta (0)
                // a cerrada (1).
                float tiempoNormalizado = 1f - posicion;

                animador.Play(
                    estadoHash,
                    0,
                    tiempoNormalizado
                );

                animador.Update(0f);
            }

            yield return null;
        }

        // Si el objeto sigue existiendo, hacerlo visible.
        if (objetivo != null && cajaSostenida != null)
        {
            objetivo.MostrarObjeto();

            Debug.Log("[Jugador] ¡Llenado completado!");
        }

        // Dejar la caja completamente cerrada.
        if (animador != null)
        {
            animador.Play(estadoHash, 0, 1f);
            animador.Update(0f);
        }

        // Ocultar barra.
        if (barraLlenado != null)
        {
            barraLlenado.value = 0f;
            barraLlenado.gameObject.SetActive(false);
        }

        objetoDetectado = null;
        estaLlenando = false;
    }
    private void MostrarEstadosAnimator(Animator animador)
    {
        if (animador == null)
        {
            Debug.LogError("[Caja] Animator es NULL");
            return;
        }

        AnimatorController controller =
            animador.runtimeAnimatorController as AnimatorController;

        if (controller == null)
        {
            Debug.LogError("[Caja] No se pudo obtener el AnimatorController.");
            return;
        }

        Debug.Log("========== ESTADOS DEL ANIMATOR ==========");

        foreach (var capa in controller.layers)
        {
            Debug.Log($"CAPA: {capa.name}");

            foreach (var estado in capa.stateMachine.states)
            {
                string ruta = capa.name + "." + estado.state.name;

                Debug.Log(
                    $"Estado: {ruta} | Hash: {Animator.StringToHash(ruta)}"
                );
            }
        }

        Debug.Log("==========================================");
    }
}
