using UnityEngine;
using UnityEngine.InputSystem;

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

    private CajaInteractuable cajaDetectada;
    private CajaInteractuable cajaSostenida;
    private GUIStyle estiloGUI;

    private void Update()
    {
        if (camara == null)
            return;

        // Si ya tenemos una caja cargada: se suelta con la tecla [G]
        if (cajaSostenida != null)
        {
            if (Keyboard.current != null &&
                Keyboard.current.gKey.wasPressedThisFrame)
            {
                SoltarCaja();
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
            textoPrompt = "G · Soltar Caja";
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
}
