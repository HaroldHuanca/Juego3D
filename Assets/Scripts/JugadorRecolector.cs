
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

    private CajaInteractuable cajaDetectada;
    private CajaInteractuable cajaSostenida;

    private void Update()
    {
        if (camara == null)
            return;

        if (cajaSostenida != null)
        {
            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                SoltarCaja();
            }

            return;
        }

        DetectarCaja();

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame &&
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

        if (!impactoDetectado)
        {
            cajaDetectada = null;
            return;
        }

        cajaDetectada =
            impacto.collider.GetComponentInParent<CajaInteractuable>();
    }

    private void RecogerCaja()
    {
        if (cajaDetectada == null || puntoSujecion == null)
            return;

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
        // Escalas después de cambiar la jerarquía.
        Debug.Log(
            $"[Caja] Escala local DESPUÉS: " +
            $"{cajaSostenida.transform.localScale}"
        );

        Debug.Log(
            $"[Caja] Escala mundial DESPUÉS: " +
            $"{cajaSostenida.transform.lossyScale}"
        );

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
}
