using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Tienda.Clientes;
using Tienda.SistemaVentas;

namespace Mundo1
{
    /// <summary>
    /// Controlador ligero y fijo para los NPCs clientes de Mundo 1 (sin IA ni rutas aleatorias).
    /// Aparece justo afuera del local, entra directo a la caja registradora, genera su pedido,
    /// espera el cobro y sale por la puerta para desaparecer.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ClienteGuionadoMundo1 : MonoBehaviour
    {
        [Header("Puntos Fijos de Ruta")]
        [Tooltip("Punto justo afuera de la puerta donde aparece y al que regresa al irse")]
        [SerializeField] private Transform puntoExteriorPuerta;

        [Tooltip("Punto justo adentro de la puerta para cruzar sin desviarse")]
        [SerializeField] private Transform puntoInteriorPuerta;

        [Tooltip("Puntos interiores entre anaqueles que visita el cliente antes de ir al mostrador")]
        [SerializeField] private Transform[] puntosAnaqueles;

        [Tooltip("Segundos que se detiene el cliente mirando cada anaquel")]
        [SerializeField] private float tiempoEnCadaAnaquel = 2.0f;

        [Tooltip("Punto frente a la caja registradora donde espera ser atendido")]
        [SerializeField] private Transform puntoCaja;

        [Header("Apariencia y Animación")]
        [Range(0, 1)]
        [SerializeField] private int varianteAnimacion = 0;
        [SerializeField] private float velocidadCaminar = 2.4f;
        [SerializeField] private bool mirarAlJugadorEnCaja = true;

        [Header("Eventos Guionados del Tramo Final")]
        public bool activarEventoEntreAnaqueles = false;
        public int indiceEstanteEvento = 1;
        public bool esClienteClimaxFinal = false;

        public Func<ClienteGuionadoMundo1, IEnumerator> RutinaEventoEntreAnaqueles;
        public Action<ClienteGuionadoMundo1> AlAcercarseACaja;
        public Func<ClienteGuionadoMundo1, IEnumerator> RutinaPrePedidoEnCaja;

        private NavMeshAgent agente;
        private Animator animator;
        private Collider colNPC;
        private IconoFlotanteCliente iconoFlotante;
        private Transform transformJugador;
        private Camera camaraJugador;
        private GUIStyle estiloPromptAtender;
        private Coroutine rutinaFlujo;
        private bool esperandoPago = false;
        public bool EstaEsperandoPago => esperandoPago;
        private bool pasoPuertaNotificadoEntrada = false;
        private bool pasoPuertaNotificadoSalida = false;
        private bool mirandoParaAtender = false;
        private bool avisoCercaCajaEnviado = false;
        private bool movimientoPausado = false;

        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamVariante = Animator.StringToHash("Variante");
        private static readonly int ParamHablando = Animator.StringToHash("Hablando");

        private void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            colNPC = GetComponent<Collider>();

            iconoFlotante = GetComponent<IconoFlotanteCliente>();
            if (iconoFlotante == null)
            {
                iconoFlotante = gameObject.AddComponent<IconoFlotanteCliente>();
            }

            // Desactivar ClienteIA antiguo si estuviera en el mismo objeto
            var iaAntigua = GetComponent<ClienteIA>();
            if (iaAntigua != null) iaAntigua.enabled = false;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) transformJugador = playerObj.transform;
            else if (Camera.main != null) transformJugador = Camera.main.transform;
        }

        private void OnEnable()
        {
            esperandoPago = false;
            pasoPuertaNotificadoEntrada = false;
            pasoPuertaNotificadoSalida = false;
            mirandoParaAtender = false;
            avisoCercaCajaEnviado = false;
            movimientoPausado = false;

            AutoBuscarPuntosSiFaltan();

            if (animator != null)
            {
                animator.SetFloat(ParamVariante, varianteAnimacion);
                animator.SetBool(ParamHablando, false);
                animator.SetFloat(ParamSpeed, 0f);
            }

            if (rutinaFlujo != null) StopCoroutine(rutinaFlujo);
            rutinaFlujo = StartCoroutine(RutinaClienteFijo());
        }

        private void OnDisable()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlCompletarCobro;
            }
            if (iconoFlotante != null) iconoFlotante.Ocultar();
        }

        private void Update()
        {
            if (animator != null && agente != null)
            {
                animator.SetFloat(ParamSpeed, agente.velocity.magnitude);
            }

            if (esperandoPago && mirarAlJugadorEnCaja && transformJugador != null)
            {
                Vector3 dir = transformJugador.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
                }
            }

            ActualizarAtencionDirectaConClick();
        }

        private void ActualizarAtencionDirectaConClick()
        {
            mirandoParaAtender = false;

            if (!esperandoPago || Cursor.lockState != CursorLockMode.Locked) return;
            if (CinematicaGerente.Instance != null && CinematicaGerente.Instance.EstaEnCinematica) return;
            if (CajaRegistradoraUI.Instance != null && CajaRegistradoraUI.Instance.EstaAbierta) return;

            if (camaraJugador == null || !camaraJugador.isActiveAndEnabled)
            {
                camaraJugador = Camera.main;
                if (camaraJugador == null)
                {
                    var camScript = FindAnyObjectByType<Camara>();
                    if (camScript != null) camaraJugador = camScript.GetComponent<Camera>();
                }
            }
            if (camaraJugador == null) return;

            Vector3 objetivo = transform.position + Vector3.up * 1.45f;
            Vector3 diff = objetivo - camaraJugador.transform.position;
            float dist = diff.magnitude;
            if (dist > 4.2f) return;

            float dot = Vector3.Dot(camaraJugador.transform.forward, diff / Mathf.Max(0.001f, dist));
            if (dot < 0.78f) return;

            mirandoParaAtender = true;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (esClienteClimaxFinal && DirectorTurno.Instance != null && DirectorTurno.Instance.DebeDispararClimaxAlCobrar())
                {
                    DirectorTurno.Instance.DispararSecuenciaFinalUltimoCliente();
                }
                else
                {
                    CajaRegistradoraUI.Instance?.AbrirCajaDirectoACobro();
                }
            }
        }

        private void OnGUI()
        {
            if (!mirandoParaAtender) return;

            if (estiloPromptAtender == null)
            {
                estiloPromptAtender = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.45f, 1f, 0.70f, 1f) }
                };
            }

            GUI.Label(new Rect(Screen.width / 2f - 220, Screen.height / 2f - 18, 440, 36), "Click · Atender Cliente", estiloPromptAtender);
        }

        public void PausarMovimiento(bool pausar)
        {
            movimientoPausado = pausar;
            if (agente != null && agente.isOnNavMesh)
            {
                agente.isStopped = pausar;
            }
        }

        private void AutoBuscarPuntosSiFaltan()
        {
            if (puntoExteriorPuerta == null)
            {
                var p = BuscarTransformPorNombre("Punto_Puerta_Exterior");
                if (p == null) p = BuscarTransformPorNombre("Punto_SalidaPuerta");
                if (p != null) puntoExteriorPuerta = p;
            }
            if (puntoInteriorPuerta == null)
            {
                var p = BuscarTransformPorNombre("Punto_Puerta_Interior");
                if (p != null) puntoInteriorPuerta = p;
            }
            if (puntoCaja == null)
            {
                var p = GestorRutas.Instance != null ? GestorRutas.Instance.PuntoCaja : null;
                if (p == null) p = BuscarTransformPorNombre("Punto_CajaRegistradora");
                if (p == null) p = BuscarTransformPorNombre("Punto_Caja");
                if (p != null) puntoCaja = p;
            }

            if (!TieneAlgunPuntoValido(puntosAnaqueles))
            {
                Transform[] baseEstantes = GestorRutas.Instance != null ? GestorRutas.Instance.PuntosEstantes : null;
                if (baseEstantes != null && baseEstantes.Length > 0)
                {
                    if (varianteAnimacion == 0 && baseEstantes.Length >= 3)
                    {
                        puntosAnaqueles = new Transform[] { baseEstantes[0], baseEstantes[1], baseEstantes[2] };
                    }
                    else if (varianteAnimacion == 1 && baseEstantes.Length >= 4)
                    {
                        puntosAnaqueles = new Transform[] { baseEstantes[2], baseEstantes[3], baseEstantes[0] };
                    }
                    else
                    {
                        puntosAnaqueles = baseEstantes;
                    }
                }
                else
                {
                    var e1 = BuscarTransformPorNombre("Estante_1");
                    var e2 = BuscarTransformPorNombre("Estante_2");
                    var e3 = BuscarTransformPorNombre("Estante_3");
                    var e4 = BuscarTransformPorNombre("Estante_4");
                    if (varianteAnimacion == 0)
                    {
                        puntosAnaqueles = FiltrarNoNulos(e1, e2, e3);
                    }
                    else
                    {
                        puntosAnaqueles = FiltrarNoNulos(e3, e4, e1);
                    }
                }
            }
        }

        private static bool TieneAlgunPuntoValido(Transform[] puntos)
        {
            if (puntos == null || puntos.Length == 0) return false;
            for (int i = 0; i < puntos.Length; i++)
            {
                if (puntos[i] != null) return true;
            }
            return false;
        }

        private static Transform[] FiltrarNoNulos(params Transform[] candidatos)
        {
            int count = 0;
            for (int i = 0; i < candidatos.Length; i++)
            {
                if (candidatos[i] != null) count++;
            }
            Transform[] resultado = new Transform[count];
            int idx = 0;
            for (int i = 0; i < candidatos.Length; i++)
            {
                if (candidatos[i] != null) resultado[idx++] = candidatos[i];
            }
            return resultado;
        }

        private static Transform BuscarTransformPorNombre(string nombre)
        {
            var todos = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < todos.Length; i++)
            {
                if (todos[i].name == nombre) return todos[i];
            }
            return null;
        }

        private IEnumerator RutinaClienteFijo()
        {
            // 1. Aparecer justo afuera del local (sobre NavMesh válido)
            if (agente != null)
            {
                agente.enabled = true;
                agente.speed = velocidadCaminar;
                agente.updateRotation = true;

                if (puntoExteriorPuerta != null)
                {
                    Vector3 posSpawn = puntoExteriorPuerta.position;
                    if (NavMesh.SamplePosition(posSpawn, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                    {
                        agente.Warp(hit.position);
                    }
                    else
                    {
                        transform.position = posSpawn;
                    }
                }
            }

            yield return null;

            // 2. Entrar por la puerta hacia el local
            if (puntoInteriorPuerta != null)
            {
                yield return StartCoroutine(MoverHacia(puntoInteriorPuerta.position, esEntrada: true));
            }

            // 3. Recorrer los puntos entre anaqueles dentro de la tienda antes de ir a caja
            if (puntosAnaqueles != null && puntosAnaqueles.Length > 0)
            {
                for (int i = 0; i < puntosAnaqueles.Length; i++)
                {
                    Transform estante = puntosAnaqueles[i];
                    if (estante == null) continue;

                    yield return StartCoroutine(MoverHacia(estante.position, esEntrada: true, tolerancia: 0.45f));
                    yield return StartCoroutine(GirarSuave(estante.rotation, 0.35f));

                    if (activarEventoEntreAnaqueles && i == indiceEstanteEvento && RutinaEventoEntreAnaqueles != null)
                    {
                        yield return StartCoroutine(RutinaEventoEntreAnaqueles(this));
                    }

                    if (tiempoEnCadaAnaquel > 0f)
                    {
                        yield return new WaitForSeconds(tiempoEnCadaAnaquel);
                    }

                    if (agente != null) agente.updateRotation = true;
                }
            }

            // 4. Ir finalmente al mostrador de la caja registradora
            if (puntoCaja != null)
            {
                yield return StartCoroutine(MoverHacia(puntoCaja.position, esEntrada: true, notificarCercaCaja: true));

                // Alinear al cliente mirando al mostrador/jugador
                Quaternion rotObjetivo = puntoCaja.rotation;
                if (mirarAlJugadorEnCaja && transformJugador != null)
                {
                    Vector3 dir = transformJugador.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) rotObjetivo = Quaternion.LookRotation(dir);
                }
                yield return StartCoroutine(GirarSuave(rotObjetivo, 0.3f));
            }

            if (RutinaPrePedidoEnCaja != null)
            {
                yield return StartCoroutine(RutinaPrePedidoEnCaja(this));
            }

            // 4. Esperar a ser cobrado en la terminal
            esperandoPago = true;
            if (animator != null) animator.SetBool(ParamHablando, true);
            if (iconoFlotante != null) iconoFlotante.MostrarIconoPago();

            if (TiendaManager.Instance != null)
            {
                if (!TiendaManager.Instance.HayPedidoPendiente)
                {
                    TiendaManager.Instance.GenerarPedidoParaCliente();
                }
                TiendaManager.Instance.OnVentaFinalizadaConExito += AlCompletarCobro;
            }

            while (esperandoPago)
            {
                yield return null;
            }

            // 5. Cobro completado: agradecer y marcharse
            if (animator != null) animator.SetBool(ParamHablando, false);
            if (iconoFlotante != null) iconoFlotante.MostrarIconoExito();

            yield return new WaitForSeconds(1.1f);
            if (iconoFlotante != null) iconoFlotante.Ocultar();

            // 6. Salir del local por la puerta y desaparecer afuera
            if (puntoInteriorPuerta != null)
            {
                yield return StartCoroutine(MoverHacia(puntoInteriorPuerta.position, esEntrada: false));
            }

            if (puntoExteriorPuerta != null)
            {
                yield return StartCoroutine(MoverHacia(puntoExteriorPuerta.position, esEntrada: false));
            }

            gameObject.SetActive(false);
        }

        private void AlCompletarCobro()
        {
            if (TiendaManager.Instance != null)
            {
                TiendaManager.Instance.OnVentaFinalizadaConExito -= AlCompletarCobro;
            }
            esperandoPago = false;
        }

        private IEnumerator MoverHacia(Vector3 destino, bool esEntrada, float tolerancia = 0.35f, bool notificarCercaCaja = false)
        {
            if (agente == null || !agente.isOnNavMesh)
            {
                yield break;
            }

            Vector3 destinoValido = destino;
            if (NavMesh.SamplePosition(destino, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                destinoValido = hit.position;
            }

            agente.isStopped = false;
            agente.updateRotation = true;
            agente.SetDestination(destinoValido);

            while (agente.pathPending)
            {
                yield return null;
            }

            float tiempoMax = 15f;
            float transcurrido = 0f;

            while (transcurrido < tiempoMax)
            {
                if (!movimientoPausado)
                {
                    transcurrido += Time.deltaTime;
                }

                // Permitir el paso a través de puertas con seguro sin dejar salir al jugador
                if (esEntrada && !pasoPuertaNotificadoEntrada)
                {
                    PuertaSeguroMundo1.PermitirPasoNPCCercanas(transform.position, colNPC, transform.forward, 2.2f);
                }
                else if (!esEntrada && !pasoPuertaNotificadoSalida)
                {
                    PuertaSeguroMundo1.PermitirPasoNPCCercanas(transform.position, colNPC, transform.forward, 2.2f);
                }

                float dist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(destinoValido.x, destinoValido.z)
                );

                if (notificarCercaCaja && !avisoCercaCajaEnviado && dist <= 3.1f)
                {
                    avisoCercaCajaEnviado = true;
                    AlAcercarseACaja?.Invoke(this);
                }

                if (dist <= Mathf.Max(agente.stoppingDistance, tolerancia))
                {
                    break;
                }

                yield return null;
            }

            if (esEntrada && puntoInteriorPuerta != null && Vector3.Distance(destino, puntoInteriorPuerta.position) < 0.5f)
            {
                pasoPuertaNotificadoEntrada = true;
            }
            if (!esEntrada && puntoExteriorPuerta != null && Vector3.Distance(destino, puntoExteriorPuerta.position) < 0.5f)
            {
                pasoPuertaNotificadoSalida = true;
            }

            agente.isStopped = true;
        }

        private IEnumerator GirarSuave(Quaternion rotacionFinal, float duracion)
        {
            if (agente != null) agente.updateRotation = false;
            Quaternion inicio = transform.rotation;
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(inicio, rotacionFinal, Mathf.Clamp01(t / duracion));
                yield return null;
            }
            transform.rotation = rotacionFinal;
        }
    }
}
