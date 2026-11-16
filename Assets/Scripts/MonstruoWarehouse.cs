using UnityEngine;
using UnityEngine.AI;

/// <summary>Patrulla y persigue únicamente dentro del volumen del almacén.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MonstruoWarehouse : MonoBehaviour
{
    public BoxCollider zona;
    public Transform jugador;
    public Animator animador;
    public NavMeshData navegacion;
    public float radioDeteccion = 13f;
    public float radioAbandono = 19f;
    public float velocidadCaminar = 1.5f;
    public float velocidadPersecucion = 2.3f;
    [Header("Animación de caminata")]
    [Tooltip("Ritmo del ciclo a la velocidad de referencia. Menor valor = pasos más lentos.")]
    [Min(0.01f)] public float ritmoAnimacion = 0.65f;
    [Tooltip("Velocidad de desplazamiento en m/s que corresponde al ritmo indicado.")]
    [Min(0.01f)] public float velocidadReferenciaAnimacion = 1.5f;
    public enum Estado { Patrullando, Persiguiendo, Regresando }
    [SerializeField] private Estado estado;
    public Estado EstadoActual => estado;
    private NavMeshAgent agente;
    private NavMeshDataInstance instancia;
    private Vector3 hogar;
    private float siguienteDecision;
    private Vector3 ultimaPosicionSegura;
    private NavMeshPath ruta;
    private NavMeshQueryFilter Filtro => new NavMeshQueryFilter { agentTypeID = agente.agentTypeID, areaMask = agente.areaMask };

    private void Start()
    {
        ruta = new NavMeshPath();
        agente = GetComponent<NavMeshAgent>();
        if (zona == null || navegacion == null) { Debug.LogError("Falta configurar el almacén del monstruo.", this); enabled = false; return; }
        instancia = NavMesh.AddNavMeshData(navegacion);
        agente.enabled = true;
        hogar = transform.position;
        if (!NavMesh.SamplePosition(hogar, out var inicio, 4f, Filtro) || !Dentro(inicio.position, agente.radius))
        { Debug.LogError("El monstruo no tiene un punto inicial transitable dentro del almacén.", this); enabled = false; return; }
        agente.Warp(inicio.position);
        hogar = ultimaPosicionSegura = inicio.position;
        if (jugador == null)
        {
            var movimiento = FindAnyObjectByType<PlayerMovement>();
            if (movimiento != null) jugador = movimiento.transform;
        }
        if (animador != null)
        {
            animador.applyRootMotion = false;
            animador.speed = 0f;
        }
    }

    public bool Dentro(Vector3 punto, float margen = 0f)
    {
        if (zona == null) return false;
        Vector3 local = zona.transform.InverseTransformPoint(punto) - zona.center;
        Vector3 mitad = zona.size * .5f;
        Vector3 escala = zona.transform.lossyScale;
        return Mathf.Abs(local.x) <= mitad.x - margen / Mathf.Abs(escala.x)
            && Mathf.Abs(local.z) <= mitad.z - margen / Mathf.Abs(escala.z)
            && Mathf.Abs(local.y) <= mitad.y;
    }

    private bool IrA(Vector3 destino)
    {
        if (!Dentro(destino, agente.radius) || !NavMesh.SamplePosition(destino, out var hit, 2f, Filtro)
            || !Dentro(hit.position, agente.radius) || !agente.CalculatePath(hit.position, ruta)
            || ruta.status != NavMeshPathStatus.PathComplete) return false;
        // Un destino interior no basta: también se rechazan rutas que crucen el exterior.
        foreach (var esquina in ruta.corners) if (!Dentro(esquina, agente.radius * .5f)) return false;
        return agente.SetPath(ruta);
    }

    private void Update()
    {
        if (agente == null || !agente.isOnNavMesh) return;
        if (!Dentro(transform.position, agente.radius * .5f))
        {
            agente.Warp(ultimaPosicionSegura);
            agente.ResetPath();
            estado = Estado.Regresando;
        }
        else ultimaPosicionSegura = transform.position;
        bool jugadorDentro = jugador != null && Dentro(jugador.position);
        float distancia = jugadorDentro ? Vector3.Distance(transform.position, jugador.position) : float.PositiveInfinity;
        bool detectar = jugadorDentro && distancia <= (estado == Estado.Persiguiendo ? radioAbandono : radioDeteccion);
        if (estado == Estado.Persiguiendo && !detectar) { estado = Estado.Regresando; siguienteDecision = 0f; }
        if (detectar && estado != Estado.Persiguiendo) { estado = Estado.Persiguiendo; siguienteDecision = 0f; }
        agente.speed = estado == Estado.Persiguiendo ? velocidadPersecucion : velocidadCaminar;
        if (Time.time >= siguienteDecision)
        {
            siguienteDecision = Time.time + .35f;
            if (estado == Estado.Persiguiendo)
            {
                if (!IrA(jugador.position)) { estado = Estado.Regresando; IrA(hogar); }
            }
            else if (estado == Estado.Regresando)
            {
                IrA(hogar);
                if (Vector3.Distance(transform.position, hogar) < 1f) estado = Estado.Patrullando;
            }
            else if (!agente.hasPath || agente.remainingDistance <= agente.stoppingDistance + .3f)
            {
                var limites = new Bounds(zona.transform.TransformPoint(zona.center), Vector3.Scale(zona.size, zona.transform.lossyScale));
                for (int i = 0; i < 24; i++)
                {
                    var destino = new Vector3(Random.Range(limites.min.x, limites.max.x), hogar.y, Random.Range(limites.min.z, limites.max.z));
                    if (Vector3.Distance(destino, transform.position) > 3f && IrA(destino)) break;
                }
            }
        }
        // Ajustar los pasos a la velocidad horizontal real, también al acelerar y frenar.
        // La referencia es independiente de la velocidad elegida para la patrulla.
        if (animador != null)
        {
            Vector3 velocidad = agente.velocity;
            velocidad.y = 0f;
            animador.speed = velocidad.magnitude / Mathf.Max(0.01f, velocidadReferenciaAnimacion)
                * Mathf.Max(0.01f, ritmoAnimacion);
        }
    }

    private void OnDestroy() { if (instancia.valid) instancia.Remove(); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, radioDeteccion);
        if (zona != null) { Gizmos.color = Color.cyan; Gizmos.matrix = zona.transform.localToWorldMatrix; Gizmos.DrawWireCube(zona.center, zona.size); }
    }
}
