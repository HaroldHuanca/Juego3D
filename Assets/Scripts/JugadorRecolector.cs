using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

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
    private string mensajeBloqueoCaja = "";

    public bool EstaSosteniendoCaja => cajaSostenida != null;

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

                    if (Keyboard.current.eKey.isPressed &&
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
            mensajeBloqueoCaja = "";

            if (cajaDetectada != null)
            {
                bool permitida = Mundo1.SistemaPaquetesMundo1.Instance == null ||
                                 Mundo1.SistemaPaquetesMundo1.Instance.PuedeRecogerCaja(cajaDetectada.gameObject, out mensajeBloqueoCaja);
                if (permitida)
                {
                    cajaDetectada.SetResaltado(true);
                }
            }
        }
        else if (cajaDetectada != null && Mundo1.SistemaPaquetesMundo1.Instance != null)
        {
            Mundo1.SistemaPaquetesMundo1.Instance.PuedeRecogerCaja(cajaDetectada.gameObject, out mensajeBloqueoCaja);
        }
    }

    private void RecogerCaja()
    {
        if (cajaDetectada == null || puntoSujecion == null)
            return;

        if (Mundo1.SistemaPaquetesMundo1.Instance != null &&
            !Mundo1.SistemaPaquetesMundo1.Instance.PuedeRecogerCaja(cajaDetectada.gameObject, out mensajeBloqueoCaja))
        {
            return;
        }

        // Apagar el brillo de interacción antes de cargarla
        cajaDetectada.SetResaltado(false);

        cajaSostenida = cajaDetectada;
        Animator animador = cajaSostenida.GetComponentInChildren<Animator>();

#if UNITY_EDITOR
        MostrarEstadosAnimator(animador);
#endif
        if (animador != null)
        {
            int estadoHash = Animator.StringToHash(nombreEstadoAnimacion);
            if (animador.HasState(0, estadoHash))
            {
                animador.speed = 0f;
                animador.Play(estadoHash, 0, 1f); // Mantener la caja cerrada al recogerla
                animador.Update(0f);
            }
        }
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

        Mundo1.SistemaPaquetesMundo1.Instance?.NotificarCajaRecogida(cajaSostenida.gameObject);
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
        Mundo1.SistemaPaquetesMundo1.Instance?.NotificarCajaSoltada();
    }

    /// <summary>
    /// Si el jugador sostiene una caja cuando es atrapado por el monstruo, la devuelve a su punto inicial en el almacén.
    /// </summary>
    public void DevolverCajaSostenidaAOrigen()
    {
        StopAllCoroutines();
        estaLlenando = false;
        objetoDetectado = null;

        if (barraLlenado != null)
        {
            barraLlenado.value = 0f;
            barraLlenado.gameObject.SetActive(false);
        }

        if (cajaSostenida == null) return;

        var caja = cajaSostenida;
        cajaSostenida = null;

        var paquete = caja.GetComponent<Mundo1.PaqueteMundo1>();
        if (paquete != null)
        {
            paquete.RestaurarEnOrigen();
        }
        else
        {
            caja.transform.SetParent(null);
            foreach (Collider col in caja.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
            }
            var rb = caja.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        Mundo1.SistemaPaquetesMundo1.Instance?.NotificarCajaSoltada();
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
            textoPrompt = !string.IsNullOrEmpty(mensajeBloqueoCaja)
                ? mensajeBloqueoCaja
                : "Click Derecho · Recoger Caja";
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
        float duracionTransicionCaja = Mathf.Clamp(1f / Mathf.Max(0.1f, velocidadAnimacion), 0.15f, 0.8f);
        float tiempoNormalizadoActual = 1f;

        // Comenzar con la caja cerrada (1f) y abrirla una sola vez hacia abierta (0f).
        if (animador != null)
        {
            animador.speed = 0f;
            animador.Play(estadoHash, 0, 1f);
            animador.Update(0f);
        }

        bool completado = false;
        while (tiempoTranscurrido < duracionLlenado)
        {
            bool mantieneE = Keyboard.current != null && Keyboard.current.eKey.isPressed;
            if (!mantieneE || objetivo == null || cajaSostenida == null)
            {
                break;
            }

            DetectarObjetoAleatorio();
            if (objetoDetectado != objetivo)
            {
                break;
            }

            tiempoTranscurrido += Time.deltaTime;

            float progreso = Mathf.Clamp01(tiempoTranscurrido / duracionLlenado);
            if (barraLlenado != null)
            {
                barraLlenado.value = progreso;
            }

            // Abrir suavemente de 1 (cerrada) a 0 (abierta) y mantenerse abierta mientras llena.
            if (animador != null)
            {
                float progresoApertura = Mathf.Clamp01(tiempoTranscurrido / duracionTransicionCaja);
                tiempoNormalizadoActual = Mathf.Lerp(1f, 0f, progresoApertura);
                animador.Play(estadoHash, 0, tiempoNormalizadoActual);
                animador.Update(0f);
            }

            if (tiempoTranscurrido >= duracionLlenado)
            {
                completado = true;
                break;
            }

            yield return null;
        }

        // Si completó el llenado, mostrar el producto en el anaquel y consumir la caja usada.
        if (completado && objetivo != null && cajaSostenida != null)
        {
            objetivo.MostrarObjeto();
            Debug.Log("[Jugador] ¡Llenado completado!");

            GameObject cajaConsumida = cajaSostenida.gameObject;
            cajaSostenida.transform.SetParent(null);
            cajaSostenida = null;
            cajaConsumida.SetActive(false);

            Mundo1.SistemaPaquetesMundo1.Instance?.NotificarPaqueteEntregado(cajaConsumida);
        }

        // Al soltar [E] o terminar el llenado, cerrar la caja suavemente hasta 1 (cerrada).
        if (animador != null && tiempoNormalizadoActual < 0.99f)
        {
            float tiempoCierre = 0f;
            float duracionCierre = Mathf.Min(duracionTransicionCaja, 0.35f) * Mathf.Clamp01(1f - tiempoNormalizadoActual);
            while (tiempoCierre < duracionCierre)
            {
                tiempoCierre += Time.deltaTime;
                float t = duracionCierre > 0.001f ? Mathf.Clamp01(tiempoCierre / duracionCierre) : 1f;
                animador.Play(estadoHash, 0, Mathf.Lerp(tiempoNormalizadoActual, 1f, t));
                animador.Update(0f);
                yield return null;
            }
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
#if UNITY_EDITOR
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
#endif
}
