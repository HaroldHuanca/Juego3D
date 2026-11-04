using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Tienda.SistemaVentas;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Tienda.Clientes
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ClienteIA : MonoBehaviour
    {
        public enum EstadoCliente
        {
            LlegandoExterior,  // Camina por la acera y cruza hacia la tienda
            Comprando,         // Visita estantes en el interior de la tienda
            YendoACaja,        // Camina hacia la zona de la caja
            EsperandoEnFila,   // Espera en la cola detrás de otros clientes (en Idle)
            EnCaja,            // Está frente al cajero pagando/hablando
            SaliendoExterior   // Sale por la puerta y se aleja por la acera
        }

        public enum ModoMirarEnCaja
        {
            HaciaRotacionPuntoCaja, // Gira hacia donde apunte la flecha azul (Forward) de Punto_Caja
            HaciaElJugador,         // Gira automáticamente para mirar al jugador/cajero a los ojos
            HaciaObjetivoManual     // Mira hacia un objeto específico
        }

        [Header("Estado del Cliente (Solo Lectura)")]
        [SerializeField] private EstadoCliente estadoActual = EstadoCliente.LlegandoExterior;
        [SerializeField] private int turnoEnFila = -1;

        [Header("Rutas Asignadas (Si están vacías, las toma de GestorRutas)")]
        [Tooltip("Si está marcado, coloca al cliente en el primer punto de la ruta exterior al iniciar")]
        [SerializeField] private bool aparecerEnPuntoInicial = true;

        [SerializeField] private Transform[] rutaLlegadaExterior;
        [SerializeField] private Transform[] puntosEstantes;
        [SerializeField] private Transform puntoCaja;
        [SerializeField] private Transform[] rutaSalidaExterior;

        [Header("Orientación y Hacia Dónde Mirar")]
        [Tooltip("Cómo decide a dónde mirar al llegar al mostrador de la caja")]
        [SerializeField] private ModoMirarEnCaja modoMiradaEnCaja = ModoMirarEnCaja.HaciaRotacionPuntoCaja;

        [Tooltip("Si eliges 'HaciaObjetivoManual', arrastra aquí el objeto al que debe mirar")]
        [SerializeField] private Transform objetivoManualMirada;

        [Tooltip("Velocidad con la que gira el cuerpo suavemente para encarar el objetivo")]
        [SerializeField] private float velocidadGiro = 6.0f;

        [Header("Configuración de Comportamiento")]
        [Tooltip("Cuántos estantes visitará antes de ir a pagar (0 para ir directo a la caja)")]
        [Range(0, 5)] [SerializeField] private int maxEstantesAVisitar = 2;

        [Tooltip("Segundos que pasa mirando un estante")]
        [SerializeField] private float tiempoEnEstante = 3.0f;

        [Header("Variación de Animación")]
        [Tooltip("0 = set de animaciones 1 (idle/walk/talk 1). 1 = set de animaciones 2")]
        [Range(0, 1)] public int varianteAnimacion = 0;

        [Header("Física y Puertas Batientes")]
        [Tooltip("Fuerza con la que empuja las puertas de cristal al acercarse")]
        [SerializeField] private float fuerzaEmpujePuerta = 6f;

        [Tooltip("Distancia hacia adelante a la que detecta la puerta para abrirla antes de chocar")]
        [SerializeField] private float distanciaSensorPuerta = 1.3f;

        // Componentes y control
        private NavMeshAgent agente;
        private Animator animator;
        private IconoFlotanteCliente iconoFlotante;
        private Transform transformJugador;
        private int indiceLlegada = 0;
        private int indiceSalida = 0;
        private int estantesVisitados = 0;
        private Coroutine rutinaActual;
        private Coroutine rutinaGiro;
        private float tiempoUltimoEmpujePuerta = 0f;

        // Parámetros del Animator
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamVariante = Animator.StringToHash("Variante");
        private static readonly int ParamHablando = Animator.StringToHash("Hablando");

        public EstadoCliente Estado => estadoActual;

        private void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();

            iconoFlotante = GetComponent<IconoFlotanteCliente>();
            if (iconoFlotante == null)
            {
                iconoFlotante = gameObject.AddComponent<IconoFlotanteCliente>();
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                transformJugador = playerObj.transform;
            }
            else if (Camera.main != null)
            {
                transformJugador = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (animator != null)
            {
                animator.SetFloat(ParamVariante, (float)varianteAnimacion);
            }

            ConectarConGestorRutas();
            IniciarRuta();
        }

        private void ConectarConGestorRutas()
        {
            if (GestorRutas.Instance == null) return;

            if (rutaLlegadaExterior == null || rutaLlegadaExterior.Length == 0)
            {
                rutaLlegadaExterior = GestorRutas.Instance.ObtenerRutaLlegadaAleatoria();
            }

            if (puntosEstantes == null || puntosEstantes.Length == 0)
            {
                puntosEstantes = GestorRutas.Instance.PuntosEstantes;
            }

            if (puntoCaja == null)
            {
                puntoCaja = GestorRutas.Instance.PuntoCaja;
            }
        }

        private void Update()
        {
            if (animator != null)
            {
                float velocidadActual = agente.velocity.magnitude;
                animator.SetFloat(ParamSpeed, velocidadActual);
            }

            DetectarYEmpujarPuertaDelante();

            // Mirar al jugador mientras está siendo atendido en caja
            if (estadoActual == EstadoCliente.EnCaja && modoMiradaEnCaja == ModoMirarEnCaja.HaciaElJugador && transformJugador != null)
            {
                GirarHaciaPosicion(transformJugador.position, Time.deltaTime * 3f);
            }

            // Tecla 'K' para pruebas: solo despacha si está siendo atendido en la caja (no si está en la cola)
            if (estadoActual == EstadoCliente.EnCaja)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
                {
                    DespacharCliente();
                }
#else
                if (Input.GetKeyDown(KeyCode.K))
                {
                    DespacharCliente();
                }
#endif
            }
        }

        private void OnDestroy()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlCompletarVentaEnCaja;
            }

            if (GestorRutas.Instance != null)
            {
                GestorRutas.Instance.AvanzarFilaCaja(this);
            }

            if (SpawnerClientes.Instance != null)
            {
                SpawnerClientes.Instance.RegistrarSalidaCliente(this);
            }
        }

        #region Orientación y Mirada
        public void GirarHaciaPosicion(Vector3 posicionObjetivo, float t)
        {
            Vector3 direccion = posicionObjetivo - transform.position;
            direccion.y = 0f;

            if (direccion.sqrMagnitude > 0.001f)
            {
                Quaternion rotacionDeseada = Quaternion.LookRotation(direccion);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacionDeseada, t);
            }
        }

        private IEnumerator RutinaGiroSuave(Quaternion rotacionFinal, float duracion = 0.4f)
        {
            agente.updateRotation = false;
            float transcurrido = 0f;
            Quaternion rotInicial = transform.rotation;

            while (transcurrido < duracion)
            {
                transcurrido += Time.deltaTime;
                float progreso = Mathf.Clamp01(transcurrido / duracion);
                transform.rotation = Quaternion.Slerp(rotInicial, rotacionFinal, progreso);
                yield return null;
            }

            transform.rotation = rotacionFinal;
        }
        #endregion

        #region Empuje de Puertas
        private void DetectarYEmpujarPuertaDelante()
        {
            if (agente == null || agente.isStopped) return;
            if (Time.time - tiempoUltimoEmpujePuerta < 0.25f) return;

            Vector3 origen = transform.position + Vector3.up * 1.0f;
            Vector3 direccion = transform.forward;

            if (Physics.Raycast(origen, direccion, out RaycastHit hit, distanciaSensorPuerta))
            {
                Rigidbody rb = hit.rigidbody;
                if (rb != null && !rb.isKinematic && hit.collider.GetComponentInParent<HingeJoint>() != null)
                {
                    rb.AddForceAtPosition(direccion * fuerzaEmpujePuerta, hit.point, ForceMode.Impulse);
                    tiempoUltimoEmpujePuerta = Time.time;
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.rigidbody != null && !collision.rigidbody.isKinematic)
            {
                if (collision.gameObject.GetComponentInParent<HingeJoint>() != null)
                {
                    Vector3 dir = (agente != null && agente.velocity.sqrMagnitude > 0.05f)
                        ? agente.velocity.normalized
                        : transform.forward;

                    Vector3 puntoContacto = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                    collision.rigidbody.AddForceAtPosition(dir * (fuerzaEmpujePuerta * 1.5f), puntoContacto, ForceMode.Force);
                }
            }
        }
        #endregion

        public void IniciarRuta()
        {
            agente.updateRotation = true;

            if (rutaLlegadaExterior != null && rutaLlegadaExterior.Length > 0)
            {
                if (aparecerEnPuntoInicial && rutaLlegadaExterior[0] != null)
                {
                    agente.Warp(rutaLlegadaExterior[0].position);
                }

                estadoActual = EstadoCliente.LlegandoExterior;
                indiceLlegada = 0;
                NavegarSiguientePuntoLlegada();
            }
            else
            {
                IniciarComprasInteriores();
            }
        }

        #region Fase 1: Llegada Exterior
        private void NavegarSiguientePuntoLlegada()
        {
            if (indiceLlegada < rutaLlegadaExterior.Length)
            {
                Transform siguientePunto = rutaLlegadaExterior[indiceLlegada];
                indiceLlegada++;

                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(siguientePunto.position, 0f, NavegarSiguientePuntoLlegada));
            }
            else
            {
                IniciarComprasInteriores();
            }
        }
        #endregion

        #region Fase 2: Compras Interiores
        private void IniciarComprasInteriores()
        {
            estantesVisitados = 0;

            if (puntosEstantes != null && puntosEstantes.Length > 0 && maxEstantesAVisitar > 0)
            {
                VisitarSiguienteEstante();
            }
            else
            {
                IrACaja();
            }
        }

        private void VisitarSiguienteEstante()
        {
            if (estantesVisitados >= maxEstantesAVisitar || puntosEstantes == null || puntosEstantes.Length == 0)
            {
                IrACaja();
                return;
            }

            estadoActual = EstadoCliente.Comprando;
            Transform estanteElegido = puntosEstantes[Random.Range(0, puntosEstantes.Length)];

            if (rutinaActual != null) StopCoroutine(rutinaActual);
            rutinaActual = StartCoroutine(RutinaMoverseHacia(estanteElegido.position, tiempoEnEstante, () =>
            {
                estantesVisitados++;
                VisitarSiguienteEstante();
            }, estanteElegido.rotation));
        }
        #endregion

        #region Fase 3: Ir a Caja y Sistema de Fila
        public void IrACaja()
        {
            if (GestorRutas.Instance == null)
            {
                DespacharCliente();
                return;
            }

            // Pedir turno en la fila
            turnoEnFila = GestorRutas.Instance.UnirseAColaCaja(this);
            Vector3 destinoFila = GestorRutas.Instance.ObtenerPosicionEnFila(turnoEnFila);

            if (turnoEnFila == 0)
            {
                // Es el primero: va directo al mostrador
                estadoActual = EstadoCliente.YendoACaja;
                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(destinoFila, 0f, LlegarACaja));
            }
            else
            {
                // Hay gente delante: se coloca en la cola y espera en Idle
                estadoActual = EstadoCliente.EsperandoEnFila;
                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(destinoFila, 0f, EsperarEnFila, puntoCaja != null ? puntoCaja.rotation : null));
            }
        }

        private void EsperarEnFila()
        {
            estadoActual = EstadoCliente.EsperandoEnFila;
            if (animator != null) animator.SetBool(ParamHablando, false); // En la cola está quieto/esperando en Idle
            if (iconoFlotante != null) iconoFlotante.MostrarIconoEspera();
            if (puntoCaja != null) StartCoroutine(RutinaGiroSuave(puntoCaja.rotation, 0.3f));
        }

        /// <summary>
        /// Se llama cuando el cliente que estaba adelante se va y toda la fila avanza un paso.
        /// </summary>
        public void ActualizarTurnoEnFila(int nuevoTurno)
        {
            if (estadoActual == EstadoCliente.SaliendoExterior) return;

            turnoEnFila = nuevoTurno;
            Vector3 nuevaPosicion = GestorRutas.Instance.ObtenerPosicionEnFila(turnoEnFila);

            if (turnoEnFila == 0)
            {
                // Le toca el turno en el mostrador
                estadoActual = EstadoCliente.YendoACaja;
                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(nuevaPosicion, 0f, LlegarACaja));
            }
            else
            {
                // Avanza un paso en la fila
                estadoActual = EstadoCliente.EsperandoEnFila;
                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(nuevaPosicion, 0f, EsperarEnFila, puntoCaja != null ? puntoCaja.rotation : null));
            }
        }

        private void LlegarACaja()
        {
            estadoActual = EstadoCliente.EnCaja;

            // Conectar con el sistema de ventas: generar el pedido real en la caja registradora
            if (TiendaManager.Instance != null)
            {
                if (!TiendaManager.Instance.HayPedidoPendiente)
                {
                    TiendaManager.Instance.GenerarPedidoParaCliente();
                }
                TiendaManager.Instance.OnVentaFinalizadaConExito += AlCompletarVentaEnCaja;
            }

            if (iconoFlotante != null)
            {
                iconoFlotante.MostrarIconoPago();
            }

            Quaternion rotacionObjetivo = transform.rotation;

            switch (modoMiradaEnCaja)
            {
                case ModoMirarEnCaja.HaciaRotacionPuntoCaja:
                    if (puntoCaja != null) rotacionObjetivo = puntoCaja.rotation;
                    break;

                case ModoMirarEnCaja.HaciaElJugador:
                    if (transformJugador != null)
                    {
                        Vector3 dir = transformJugador.position - transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f) rotacionObjetivo = Quaternion.LookRotation(dir);
                    }
                    break;

                case ModoMirarEnCaja.HaciaObjetivoManual:
                    if (objetivoManualMirada != null)
                    {
                        Vector3 dir = objetivoManualMirada.position - transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f) rotacionObjetivo = Quaternion.LookRotation(dir);
                    }
                    break;
            }

            if (rutinaGiro != null) StopCoroutine(rutinaGiro);
            rutinaGiro = StartCoroutine(RutinaGiroSuave(rotacionObjetivo, 0.4f));

            if (animator != null)
            {
                animator.SetBool(ParamHablando, true);
            }

            Debug.Log($"[ClienteIA] '{name}' está siendo atendido en caja. Cobra en la terminal para despacharlo (o tecla 'K').");
        }

        private void AlCompletarVentaEnCaja()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlCompletarVentaEnCaja;
            }

            if (iconoFlotante != null)
            {
                iconoFlotante.MostrarIconoExito();
            }

            StartCoroutine(RutinaDespachoConRetardo(1.2f));
        }

        private IEnumerator RutinaDespachoConRetardo(float delay)
        {
            yield return new WaitForSeconds(delay);
            DespacharCliente();
        }

        public void DespacharCliente()
        {
            if (estadoActual != EstadoCliente.EnCaja) return;

            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlCompletarVentaEnCaja;
            }

            if (iconoFlotante != null)
            {
                iconoFlotante.Ocultar();
            }

            agente.updateRotation = true;

            if (animator != null)
            {
                animator.SetBool(ParamHablando, false);
            }

            // Avisar a la cola que avance
            if (GestorRutas.Instance != null)
            {
                GestorRutas.Instance.AvanzarFilaCaja(this);
                rutaSalidaExterior = GestorRutas.Instance.ObtenerRutaSalidaAleatoria();
            }

            estadoActual = EstadoCliente.SaliendoExterior;
            indiceSalida = 0;

            if (rutaSalidaExterior != null && rutaSalidaExterior.Length > 0)
            {
                NavegarSiguientePuntoSalida();
            }
            else
            {
                Destroy(gameObject, 1f);
            }
        }
        #endregion

        #region Fase 5: Salida Exterior
        private void NavegarSiguientePuntoSalida()
        {
            agente.updateRotation = true;

            if (indiceSalida < rutaSalidaExterior.Length)
            {
                Transform siguientePunto = rutaSalidaExterior[indiceSalida];
                indiceSalida++;

                if (rutinaActual != null) StopCoroutine(rutinaActual);
                rutinaActual = StartCoroutine(RutinaMoverseHacia(siguientePunto.position, 0f, NavegarSiguientePuntoSalida));
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Rutina de Movimiento con NavMesh
        private IEnumerator RutinaMoverseHacia(Vector3 destino, float esperaAlLlegar, System.Action alTerminar, Quaternion? rotacionAlLlegar = null)
        {
            agente.isStopped = false;
            agente.updateRotation = true;
            agente.SetDestination(destino);

            yield return new WaitUntil(() => !agente.pathPending);

            while (agente.remainingDistance > agente.stoppingDistance + 0.2f)
            {
                yield return null;
            }

            agente.isStopped = true;

            if (rotacionAlLlegar.HasValue)
            {
                yield return StartCoroutine(RutinaGiroSuave(rotacionAlLlegar.Value, 0.35f));
            }

            if (esperaAlLlegar > 0f)
            {
                yield return new WaitForSeconds(esperaAlLlegar);
            }

            alTerminar?.Invoke();
        }
        #endregion

        #region Gizmos Visuales en la Vista Scene
        private void OnDrawGizmosSelected()
        {
            if (rutaLlegadaExterior != null && rutaLlegadaExterior.Length > 0)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < rutaLlegadaExterior.Length; i++)
                {
                    if (rutaLlegadaExterior[i] == null) continue;
                    Gizmos.DrawWireSphere(rutaLlegadaExterior[i].position, 0.45f);
                    if (i > 0 && rutaLlegadaExterior[i - 1] != null)
                    {
                        Gizmos.DrawLine(rutaLlegadaExterior[i - 1].position, rutaLlegadaExterior[i].position);
                    }
                }
            }

            if (puntosEstantes != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var p in puntosEstantes)
                {
                    if (p != null)
                    {
                        Gizmos.DrawWireSphere(p.position, 0.4f);
                        Gizmos.DrawRay(p.position, p.forward * 0.8f);
                    }
                }
            }

            if (puntoCaja != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(puntoCaja.position, 0.5f);
                Gizmos.DrawRay(puntoCaja.position, puntoCaja.forward * 1.2f);
            }

            if (rutaSalidaExterior != null && rutaSalidaExterior.Length > 0)
            {
                Gizmos.color = new Color(1f, 0.4f, 0f);
                for (int i = 0; i < rutaSalidaExterior.Length; i++)
                {
                    if (rutaSalidaExterior[i] == null) continue;
                    Gizmos.DrawWireSphere(rutaSalidaExterior[i].position, 0.45f);
                    if (i > 0 && rutaSalidaExterior[i - 1] != null)
                    {
                        Gizmos.DrawLine(rutaSalidaExterior[i - 1].position, rutaSalidaExterior[i].position);
                    }
                }
            }
        }
        #endregion
    }
}
