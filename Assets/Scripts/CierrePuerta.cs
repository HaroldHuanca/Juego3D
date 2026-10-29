using UnityEngine;

/// <summary>Abre completamente por interacción y cierra tras una espera.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
[DefaultExecutionOrder(50)]
public class CierrePuerta : MonoBehaviour
{
    [Range(10f, 160f)] public float anguloApertura = 90f;
    [Min(0.1f)] public float duracionMovimiento = 0.9f;
    [Min(0f)] public float segundosAbierta = 3f;
    [Min(0.05f)] public float margenSeguridad = 0.25f;
    private HingeJoint bisagra;
    private Rigidbody cuerpo;
    private Vector3 posicionCerrada, pivote, eje;
    private Quaternion rotacionCerrada;
    private float inicio, destino, transcurrido, espera;
    private bool enMovimiento;
    private float anguloAbierto;
    private ZonaHoja[] zonas;
    private readonly Collider[] contactos = new Collider[32];
    private struct ZonaHoja
    {
        public Vector3 centro, mitad;
        public Quaternion rotacion;
    }
    public float AnguloActual { get; private set; }
    public bool Cerrada => !enMovimiento && !Abierta;
    public bool Abierta { get; private set; }
    public bool PuedeAbrir => isActiveAndEnabled && Cerrada;
    public bool JugadorEnPaso { get; private set; }

    private void Awake()
    {
        bisagra = GetComponent<HingeJoint>();
        cuerpo = GetComponent<Rigidbody>();
        // La escena define la pose cerrada. Girar alrededor del anclaje funciona
        // también con los ejes locales Z de las puertas importadas.
        posicionCerrada = cuerpo.position;
        rotacionCerrada = cuerpo.rotation;
        pivote = transform.TransformPoint(bisagra.anchor);
        eje = transform.TransformDirection(bisagra.axis).normalized;
        bisagra.useSpring = false;
        bisagra.useMotor = false;
        if (!cuerpo.isKinematic)
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
        }
        cuerpo.isKinematic = true;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        var hojas = GetComponentsInChildren<Collider>();
        var zonasHoja = new System.Collections.Generic.List<ZonaHoja>();
        foreach (var hoja in hojas)
        {
            if (!hoja.enabled || hoja.isTrigger || hoja.attachedRigidbody != cuerpo) continue;
            if (hoja is BoxCollider caja)
            {
                Vector3 escala = caja.transform.lossyScale;
                escala = new Vector3(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z));
                zonasHoja.Add(new ZonaHoja { centro = caja.transform.TransformPoint(caja.center),
                    mitad = Vector3.Scale(caja.size * 0.5f, escala), rotacion = caja.transform.rotation });
            }
            else zonasHoja.Add(new ZonaHoja { centro = hoja.bounds.center,
                mitad = hoja.bounds.extents, rotacion = Quaternion.identity });
        }
        zonas = zonasHoja.ToArray();
    }

    public bool Abrir(Vector3 posicionJugador, Vector3 puntoContacto)
    {
        if (!PuedeAbrir) return false;
        Vector3 radial = puntoContacto - pivote;
        if (Vector3.Cross(eje, radial).sqrMagnitude < 0.001f)
        {
            Collider hoja = GetComponent<Collider>();
            if (hoja != null) radial = hoja.bounds.center - pivote;
        }
        float torque = Vector3.Dot(Vector3.Cross(radial, puntoContacto - posicionJugador), eje);
        float signo = torque < 0f ? -1f : 1f;
        float amplitud = Mathf.Clamp(anguloApertura, 10f, 160f);
        if (bisagra.useLimits)
        {
            float disponible = signo > 0f ? bisagra.limits.max : -bisagra.limits.min;
            if (disponible < 10f)
            {
                signo = -signo;
                disponible = signo > 0f ? bisagra.limits.max : -bisagra.limits.min;
            }
            amplitud = Mathf.Min(amplitud, Mathf.Max(0f, disponible));
        }
        if (amplitud < 1f) return false;
        if (TryGetComponent<AudioPuerta>(out var audio)) audio.NotificarApertura();
        anguloAbierto = signo * amplitud;
        ComenzarMovimiento(anguloAbierto);
        return true;
    }

    private void ComenzarMovimiento(float objetivo)
    {
        inicio = AnguloActual;
        destino = objetivo;
        transcurrido = 0f;
        enMovimiento = true;
    }

    private void FixedUpdate()
    {
        JugadorEnPaso = !Cerrada && HayJugadorEnRecorrido();
        if (JugadorEnPaso)
        {
            espera = segundosAbierta;
            // Si alguien entra cuando ya está cerrándose, recuperar el paso libre.
            if (enMovimiento && Mathf.Approximately(destino, 0f))
                ComenzarMovimiento(anguloAbierto);
        }
        if (!enMovimiento)
        {
            if (Abierta)
            {
                if (JugadorEnPaso) return;
                espera -= Time.fixedDeltaTime;
                if (espera <= 0f) ComenzarMovimiento(0f);
            }
            return;
        }
        transcurrido += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(transcurrido / Mathf.Max(0.1f, duracionMovimiento));
        AnguloActual = Mathf.Lerp(inicio, destino, Mathf.SmoothStep(0f, 1f, t));
        Quaternion giro = Quaternion.AngleAxis(AnguloActual, eje);
        cuerpo.MovePosition(pivote + giro * (posicionCerrada - pivote));
        cuerpo.MoveRotation(giro * rotacionCerrada);
        if (t >= 1f)
        {
            enMovimiento = false;
            Abierta = Mathf.Abs(destino) > 0.01f;
            espera = segundosAbierta;
        }
    }

    private bool HayJugadorEnRecorrido()
    {
        // La zona permanece en el hueco original y cubre todo el barrido de la
        // hoja, incluso cuando está abierta. No depende de qué lado cruza el jugador.
        int muestras = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(anguloAbierto) / 15f));
        for (int i = 0; i <= muestras; i++)
        {
            Quaternion giro = Quaternion.AngleAxis(anguloAbierto * i / muestras, eje);
            foreach (var zona in zonas)
            {
                Vector3 centro = pivote + giro * (zona.centro - pivote);
                Vector3 mitad = zona.mitad + Vector3.one * margenSeguridad;
                Quaternion rotacion = giro * zona.rotacion;
                int cantidad = Physics.OverlapBoxNonAlloc(centro, mitad, contactos, rotacion,
                    ~0, QueryTriggerInteraction.Ignore);
                // No perder al jugador si el escenario llena el búfer habitual.
                Collider[] encontrados = cantidad == contactos.Length
                    ? Physics.OverlapBox(centro, mitad, rotacion, ~0, QueryTriggerInteraction.Ignore) : contactos;
                if (encontrados != contactos) cantidad = encontrados.Length;
                for (int j = 0; j < cantidad; j++)
                    if (encontrados[j] != null && encontrados[j].GetComponentInParent<PlayerMovement>() != null)
                        return true;
            }
        }
        return false;
    }
}
