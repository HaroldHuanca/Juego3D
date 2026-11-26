
using System;
using UnityEngine;
using UnityEngine.InputSystem;

// La mirada se actualiza en 100; mover después evita usar la orientación del frame anterior.
[DefaultExecutionOrder(110)]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float Velocidad = 2.2f;
    [Min(0.1f)] public float Aceleracion = 16f;
    [Min(0.1f)] public float Desaceleracion = 30f;
    [Range(0.1f, 1f)] public float FactorRetroceso = 0.7f;
    [Range(0.1f, 1f)] public float FactorLateral = 0.8f;
    [Min(0f)] public float FuerzaEmpuje = 5f;

    [Header("Esprint y Estamina")]
    public bool EsprintActivado = true;
    [Min(0.1f)] public float VelocidadEsprint = 4.2f;
    [Min(0.1f)] public float AceleracionEsprint = 20f;
    [SerializeField] private float estaminaMaxima = 100f;
    [Tooltip("Consumo por segundo al esprintar (~18.2 = ~5.5s de carrera continua)")]
    [SerializeField] private float consumoEstaminaPorSegundo = 18.2f;
    [Tooltip("Regeneración por segundo al no esprintar (~12.5 = ~2s para recuperar el 25%)")]
    [SerializeField] private float regeneracionEstaminaPorSegundo = 12.5f;
    [Tooltip("Porcentaje mínimo requerido tras agotarse antes de poder volver a esprintar")]
    [SerializeField] private float umbralRecuperacionSprint = 25f;

    [Header("Audio de Jadeo / Cansancio")]
    [SerializeField] private AudioClip clipJadeoCansancio;
    [Range(0f, 1f)] [SerializeField] private float volumenJadeo = 0.65f;

    public float Gravedad = -9.81f;
    public float FuerzaSalto = 1.5f;

    private CharacterController controller;
    private Vector3 velocidadVertical;
    private BrazosPrimeraPersona brazos;
    private Vector3 velocidadHorizontal;
    private float estaminaActual = 100f;
    private bool estaminaAgotada = false;
    private AudioSource audioSourceJadeo;
    private float multiplicadorVelocidadTemporal = 1f;
    private bool bloquearSprintTemporal = false;
    private bool limiteZonaCajaActivo = false;
    private Vector3 centroZonaCaja;
    private float mitadZonaCajaX = 1.65f;
    private float mitadZonaCajaZ = 2.2f;

    public event Action<float, float> OnEstaminaCambiada;

    public float EstaminaActual => estaminaActual;
    public float EstaminaMaxima => estaminaMaxima;
    public float PorcentajeEstamina => estaminaMaxima > 0f ? Mathf.Clamp01(estaminaActual / estaminaMaxima) : 0f;
    public bool EstaminaAgotada => estaminaAgotada;
    public float VelocidadHorizontalActual { get; private set; }
    public float DistanciaCaminada { get; private set; }
    public bool EnSuelo => controller != null && controller.isGrounded;
    public bool Esprintando { get; private set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        estaminaActual = estaminaMaxima;
        estaminaAgotada = false;

        audioSourceJadeo = gameObject.AddComponent<AudioSource>();
        audioSourceJadeo.playOnAwake = false;
        audioSourceJadeo.loop = true;
        audioSourceJadeo.spatialBlend = 0f;
        audioSourceJadeo.volume = volumenJadeo;

#if UNITY_EDITOR
        if (clipJadeoCansancio == null)
        {
            clipJadeoCansancio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/clipLatido.mp3");
        }
#endif
        OnEstaminaCambiada?.Invoke(estaminaActual, estaminaMaxima);
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
        bool intentaEsprintar = EsprintActivado && shift && vertical > 0f;
        Esprintando = intentaEsprintar && !estaminaAgotada && !bloquearSprintTemporal && estaminaActual > 0f;

        ActualizarEstamina();

        float rapidez = (Esprintando ? Mathf.Max(Velocidad, VelocidadEsprint) : Velocidad) * multiplicadorVelocidadTemporal;
        // Suavizar solamente la rapidez: la dirección siempre sigue la mirada y las teclas.
        // Interpolar un vector mundial conserva impulso lateral al girar la cámara.
        float rapidezObjetivo = rapidez * factor * movimiento.magnitude;
        float rapidezActual = velocidadHorizontal.magnitude;
        float cambio = rapidezObjetivo < rapidezActual ? Desaceleracion :
            Esprintando ? AceleracionEsprint : Aceleracion;
        float nuevaRapidez = Mathf.MoveTowards(rapidezActual, rapidezObjetivo, cambio * Time.deltaTime);
        velocidadHorizontal = movimiento.sqrMagnitude > 0f
            ? movimiento.normalized * nuevaRapidez
            : Vector3.zero;

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

        if (limiteZonaCajaActivo)
        {
            Vector3 pos = transform.position;
            float minX = centroZonaCaja.x - mitadZonaCajaX;
            float maxX = centroZonaCaja.x + mitadZonaCajaX;
            float minZ = centroZonaCaja.z - mitadZonaCajaZ;
            float maxZ = centroZonaCaja.z + mitadZonaCajaZ;
            float clampedX = Mathf.Clamp(pos.x, minX, maxX);
            float clampedZ = Mathf.Clamp(pos.z, minZ, maxZ);
            if (!Mathf.Approximately(pos.x, clampedX) || !Mathf.Approximately(pos.z, clampedZ))
            {
                bool estabaActivo = controller.enabled;
                controller.enabled = false;
                transform.position = new Vector3(clampedX, pos.y, clampedZ);
                controller.enabled = estabaActivo;
            }
        }

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
        if (audioSourceJadeo != null && audioSourceJadeo.isPlaying)
        {
            audioSourceJadeo.Stop();
        }
    }

    private void ActualizarEstamina()
    {
        float anterior = estaminaActual;

        if (Esprintando)
        {
            estaminaActual = Mathf.Max(0f, estaminaActual - consumoEstaminaPorSegundo * Time.deltaTime);
            if (estaminaActual <= 0.01f)
            {
                estaminaActual = 0f;
                estaminaAgotada = true;
            }
        }
        else
        {
            estaminaActual = Mathf.Min(estaminaMaxima, estaminaActual + regeneracionEstaminaPorSegundo * Time.deltaTime);
            if (estaminaAgotada && estaminaActual >= umbralRecuperacionSprint)
            {
                estaminaAgotada = false;
            }
        }

        ActualizarAudioJadeo();

        if (!Mathf.Approximately(anterior, estaminaActual))
        {
            OnEstaminaCambiada?.Invoke(estaminaActual, estaminaMaxima);
        }
    }

    private void ActualizarAudioJadeo()
    {
        if (audioSourceJadeo == null || clipJadeoCansancio == null) return;

        bool debeSonar = estaminaAgotada || estaminaActual <= estaminaMaxima * 0.20f;
        if (debeSonar)
        {
            if (audioSourceJadeo.clip != clipJadeoCansancio)
            {
                audioSourceJadeo.clip = clipJadeoCansancio;
            }
            if (!audioSourceJadeo.isPlaying)
            {
                audioSourceJadeo.Play();
            }
            audioSourceJadeo.volume = estaminaAgotada ? volumenJadeo : volumenJadeo * 0.55f;
        }
        else if (audioSourceJadeo.isPlaying)
        {
            audioSourceJadeo.Stop();
        }
    }

    public void RestaurarEstaminaCompleta()
    {
        estaminaActual = estaminaMaxima;
        estaminaAgotada = false;
        ActualizarAudioJadeo();
        OnEstaminaCambiada?.Invoke(estaminaActual, estaminaMaxima);
    }

    public void EstablecerPenalizacionDolor(bool activo, float multVelocidad = 0.28f)
    {
        multiplicadorVelocidadTemporal = activo ? Mathf.Clamp(multVelocidad, 0.05f, 1f) : 1f;
        bloquearSprintTemporal = activo;
    }

    public void EstablecerLimiteZonaCaja(bool activo, Vector3 centro, float halfSizeX = 1.65f, float halfSizeZ = 2.2f)
    {
        limiteZonaCajaActivo = activo;
        centroZonaCaja = centro;
        mitadZonaCajaX = Mathf.Max(0.5f, halfSizeX);
        mitadZonaCajaZ = Mathf.Max(0.5f, halfSizeZ);
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
