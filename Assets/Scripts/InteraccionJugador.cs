using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class InteraccionJugador : MonoBehaviour
{
    public BrazosPrimeraPersona brazos;
    public Transform jugador;
    public LayerMask capasInteractuables = ~0;
    [Min(0.1f)] public float distancia = 1.8f;
    [Min(0f)] public float fuerzaPuerta = 3f;
    public bool mostrarIndicador = true;
    private ObjetoRecogible objeto;
    private Rigidbody puerta;
    private RaycastHit contacto;
    private Coroutine recogidaPendiente;
    private GUIStyle estilo;

    private void Update()
    {
        BuscarObjetivo();
        if (Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame) IntentarInteractuar();
    }

    private void BuscarObjetivo()
    {
        objeto = null;
        puerta = null;
        // El primer impacto bloquea la interacción: no permite recoger a través de paredes.
        if (!Physics.Raycast(transform.position, transform.forward, out contacto,
            distancia, capasInteractuables, QueryTriggerInteraction.Ignore)) return;
        if (jugador != null && contacto.transform.IsChildOf(jugador)) return;
        objeto = contacto.collider.GetComponentInParent<ObjetoRecogible>();
        Rigidbody body = contacto.rigidbody;
        if (body != null && !body.isKinematic && body.GetComponent<HingeJoint>() != null) puerta = body;
    }

    public bool IntentarInteractuar()
    {
        BuscarObjetivo();
        if (brazos == null || brazos.Ocupado || recogidaPendiente != null) return false;
        if (objeto != null && objeto.isActiveAndEnabled && !objeto.Recogido)
        {
            if (!brazos.Recoger()) return false;
            recogidaPendiente = StartCoroutine(RecogerAlContacto(objeto));
            return true;
        }
        if (puerta == null || !brazos.Empujar()) return false;
        puerta.AddForceAtPosition(transform.forward * fuerzaPuerta, contacto.point, ForceMode.Impulse);
        return true;
    }

    private IEnumerator RecogerAlContacto(ObjetoRecogible objetivo)
    {
        yield return new WaitForSeconds(brazos.TiempoContacto);
        BuscarObjetivo();
        if (objetivo != null && objeto == objetivo && objetivo.isActiveAndEnabled && !objetivo.Recogido)
            objetivo.Recoger();
        recogidaPendiente = null;
    }

    private void OnDisable()
    {
        if (recogidaPendiente != null) StopCoroutine(recogidaPendiente);
        recogidaPendiente = null;
        objeto = null;
        puerta = null;
    }

    private void OnGUI()
    {
        if (!mostrarIndicador || Cursor.lockState != CursorLockMode.Locked) return;
        if (estilo == null) estilo = new GUIStyle(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        GUI.Label(new Rect(Screen.width / 2f - 8, Screen.height / 2f - 8, 16, 16), "·", estilo);
        if (brazos == null || brazos.Ocupado) return;
        string texto = objeto != null ? "E · Recoger " + objeto.nombreObjeto : puerta != null ? "E · Empujar" : "";
        GUI.Label(new Rect(Screen.width / 2f - 180, Screen.height / 2f + 25, 360, 30), texto, estilo);
    }
}
