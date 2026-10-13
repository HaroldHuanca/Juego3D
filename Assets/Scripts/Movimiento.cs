
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float Velocidad = 2.2f;
    [Min(0.1f)] public float Aceleracion = 5f;
    [Min(0.1f)] public float Desaceleracion = 8f;
    [Range(0.1f, 1f)] public float FactorRetroceso = 0.7f;
    [Range(0.1f, 1f)] public float FactorLateral = 0.8f;
    [Min(0f)] public float FuerzaEmpuje = 5f;
    [Header("Esprint (mantener Shift al avanzar)")]
    public bool EsprintActivado = true;
    [Min(0.1f)] public float VelocidadEsprint = 4.2f;
    [Min(0.1f)] public float AceleracionEsprint = 7f;
    public float Gravedad = -9.81f;
    public float FuerzaSalto = 1.5f;

    private CharacterController controller;
    private Vector3 velocidadVertical;
    private BrazosPrimeraPersona brazos;
    private Vector3 velocidadHorizontal;

    public float VelocidadHorizontalActual { get; private set; }
    public float DistanciaCaminada { get; private set; }
    public bool EnSuelo => controller != null && controller.isGrounded;
    public bool Esprintando { get; private set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // El CharacterController ya resuelve la gravedad y los contactos. Un
        // Rigidbody dinámico con otra cápsula corregía la posición en FixedUpdate.
        if (TryGetComponent<Rigidbody>(out var cuerpo))
        {
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }
        if (TryGetComponent<CapsuleCollider>(out var capsulaExtra)) capsulaExtra.enabled = false;
        var interaccion = GetComponentInChildren<InteraccionJugador>();
        brazos = interaccion != null ? interaccion.brazos : GetComponentInChildren<BrazosPrimeraPersona>();
    }

    void Update()
    {
        // Detectar si el personaje está en el suelo
        bool enSuelo = controller.isGrounded;

        if (enSuelo && velocidadVertical.y < 0)
        {
            velocidadVertical.y = -2f;
        }

        // Entrada del teclado usando New Input System
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontal = -1f;

            if (Keyboard.current.dKey.isPressed)
                horizontal = 1f;

            if (Keyboard.current.wKey.isPressed)
                vertical = 1f;

            if (Keyboard.current.sKey.isPressed)
                vertical = -1f;
        }

        // Movimiento relativo a la orientación del jugador
        Vector3 movimiento =
            transform.right * horizontal +
            transform.forward * vertical;

        // Evitar que el movimiento diagonal sea más rápido
        movimiento = Vector3.ClampMagnitude(movimiento, 1f);

        float factor = vertical < 0f ? FactorRetroceso : 1f;
        if (vertical == 0f && horizontal != 0f) factor = FactorLateral;
        bool shift = Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        Esprintando = EsprintActivado && shift && vertical > 0f;
        float rapidez = Esprintando ? Mathf.Max(Velocidad, VelocidadEsprint) : Velocidad;
        Vector3 objetivo = movimiento * rapidez * factor;
        bool frenando = objetivo.sqrMagnitude < velocidadHorizontal.sqrMagnitude;
        float cambio = movimiento.sqrMagnitude == 0f || frenando ? Desaceleracion :
            Esprintando ? AceleracionEsprint : Aceleracion;
        velocidadHorizontal = Vector3.MoveTowards(velocidadHorizontal, objetivo, cambio * Time.deltaTime);

        // Salto
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            enSuelo)
        {
            velocidadVertical.y = Mathf.Sqrt(
                FuerzaSalto * -2f * Gravedad
            );
        }

        // Gravedad
        velocidadVertical.y += Gravedad * Time.deltaTime;

        Vector3 antes = transform.position;
        controller.Move((velocidadHorizontal + velocidadVertical) * Time.deltaTime);
        Vector3 desplazamiento = transform.position - antes;
        desplazamiento.y = 0f;
        VelocidadHorizontalActual = Time.deltaTime > 0f ? desplazamiento.magnitude / Time.deltaTime : 0f;
        if (enSuelo && controller.isGrounded) DistanciaCaminada += desplazamiento.magnitude;
    }

    private void OnDisable()
    {
        velocidadHorizontal = Vector3.zero;
        VelocidadHorizontalActual = 0f;
        Esprintando = false;
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Verificar si el objeto con el que chocamos tiene un Rigidbody
        Rigidbody body = hit.collider.attachedRigidbody;

        // Si no tiene Rigidbody, o está marcado como IsKinematic, no hacemos nada
        if (body == null || body.isKinematic)
        {
            return;
        }

        // Asegurarnos de que no estamos empujando algo que esté debajo de nosotros
        if (hit.normal.y > 0.5f || velocidadHorizontal.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Calcular la dirección del empuje (usando la dirección en la que camina el jugador)
        Vector3 pushDir = velocidadHorizontal.normalized;

        // La fuerza de las puertas no depende de la velocidad de caminata.
        body.AddForceAtPosition(pushDir * FuerzaEmpuje, hit.point, ForceMode.Impulse);
        if (brazos != null && body.GetComponent<HingeJoint>() != null)
            brazos.Empujar();
    }
}
