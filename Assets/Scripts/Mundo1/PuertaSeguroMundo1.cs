using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mundo1.UI;

namespace Mundo1
{
    /// <summary>
    /// Coloca un seguro temporal en las puertas exteriores del local durante el prólogo
    /// y la atención de los 2 primeros clientes. Impide que el jugador salga antes de tiempo
    /// mostrando un aviso claro, pero permite el paso de los NPCs clientes y se desbloquea
    /// automáticamente al pasar a la fase de reponer anaqueles.
    /// </summary>
    [DisallowMultipleComponent]
    public class PuertaSeguroMundo1 : MonoBehaviour
    {
        private static readonly List<PuertaSeguroMundo1> TodasLasPuertas = new List<PuertaSeguroMundo1>();
        private static float ultimoAvisoGlobal = -10f;

        [Header("Estado del Seguro")]
        [SerializeField] private bool bloqueada = true;

        [TextArea(2, 3)]
        [SerializeField] private string mensajeBloqueo = "No es momento de salir del local aún. Termina de atender a los 2 clientes iniciales.";

        [SerializeField] private string textoIndicadorBloqueada = "🔒 Puerta cerrada (atiende a los 2 clientes)";

        private Rigidbody rb;
        private Collider colPuerta;
        private Quaternion rotacionInicialLocal;
        private Vector3 posicionInicialLocal;
        private Coroutine rutinaPasoNPC;
        private int npcsEnPaso = 0;

        public bool EstaBloqueada => bloqueada;
        public string TextoIndicador => textoIndicadorBloqueada;

        private void OnEnable()
        {
            if (!TodasLasPuertas.Contains(this))
            {
                TodasLasPuertas.Add(this);
            }
        }

        private void OnDisable()
        {
            TodasLasPuertas.Remove(this);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colPuerta = GetComponent<Collider>();
            rotacionInicialLocal = transform.localRotation;
            posicionInicialLocal = transform.localPosition;

            AplicarEstadoSeguro();
        }

        public void EstablecerBloqueo(bool bloquear)
        {
            bloqueada = bloquear;
            AplicarEstadoSeguro();
        }

        private void AplicarEstadoSeguro()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            if (bloqueada)
            {
                // Si no hay un NPC cruzando en este instante, fijar la puerta cerrada
                if (npcsEnPaso <= 0)
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    transform.localPosition = posicionInicialLocal;
                    transform.localRotation = rotacionInicialLocal;
                }
            }
            else
            {
                rb.isKinematic = false;
            }
        }

        /// <summary>
        /// Muestra el aviso al jugador cuando intenta abrir o salir por una puerta con seguro.
        /// </summary>
        public void NotificarIntentoSalida()
        {
            if (!bloqueada) return;

            if (Time.unscaledTime - ultimoAvisoGlobal < 2.5f) return;
            ultimoAvisoGlobal = Time.unscaledTime;

            if (HUDTareasMundo1.Instance != null)
            {
                HUDTareasMundo1.Instance.MostrarSubtitulo($"<color=#FFCC55>🔒 {mensajeBloqueo}</color>", 3.0f);
            }
            else
            {
                Debug.Log($"[PuertaSeguroMundo1] {mensajeBloqueo}");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!bloqueada) return;

            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<PlayerMovement>() != null)
            {
                NotificarIntentoSalida();
            }
        }

        /// <summary>
        /// Permite que un NPC guionado empuje y cruce la puerta aunque tenga seguro para el jugador.
        /// </summary>
        public void PermitirPasoNPC(Collider colliderNPC, Vector3 direccionPaso, float duracion = 1.8f)
        {
            if (!bloqueada) return;

            if (colPuerta != null && colliderNPC != null)
            {
                Physics.IgnoreCollision(colPuerta, colliderNPC, true);
            }

            if (rutinaPasoNPC != null) StopCoroutine(rutinaPasoNPC);
            rutinaPasoNPC = StartCoroutine(RutinaPasoNPC(colliderNPC, direccionPaso, duracion));
        }

        private IEnumerator RutinaPasoNPC(Collider colliderNPC, Vector3 direccionPaso, float duracion)
        {
            npcsEnPaso++;

            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 dir = direccionPaso;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    rb.AddForce(dir.normalized * 8f, ForceMode.Impulse);
                }
            }

            yield return new WaitForSeconds(duracion);

            if (colPuerta != null && colliderNPC != null)
            {
                Physics.IgnoreCollision(colPuerta, colliderNPC, false);
            }

            npcsEnPaso = Mathf.Max(0, npcsEnPaso - 1);
            AplicarEstadoSeguro();
            rutinaPasoNPC = null;
        }

        /// <summary>
        /// Bloquea todas las puertas del local (permite paso a NPCs guionados, impide salida del jugador).
        /// </summary>
        public static void BloquearTodas(string mensajePersonalizado = null, string indicadorPersonalizado = null)
        {
            for (int i = 0; i < TodasLasPuertas.Count; i++)
            {
                var puerta = TodasLasPuertas[i];
                if (puerta == null) continue;

                if (!string.IsNullOrEmpty(mensajePersonalizado)) puerta.mensajeBloqueo = mensajePersonalizado;
                if (!string.IsNullOrEmpty(indicadorPersonalizado)) puerta.textoIndicadorBloqueada = indicadorPersonalizado;
                puerta.EstablecerBloqueo(true);
            }
        }

        /// <summary>
        /// Desbloquea todas las puertas del local al completar la atención de los clientes iniciales.
        /// </summary>
        public static void DesbloquearTodas()
        {
            for (int i = 0; i < TodasLasPuertas.Count; i++)
            {
                if (TodasLasPuertas[i] != null)
                {
                    TodasLasPuertas[i].EstablecerBloqueo(false);
                }
            }
            Debug.Log("<color=green>[PuertaSeguroMundo1] Todas las puertas del local han sido desbloqueadas.</color>");
        }

        /// <summary>
        /// Avisa a todas las puertas bloqueadas cercanas cuando un NPC necesita cruzar.
        /// </summary>
        public static void PermitirPasoNPCCercanas(Vector3 posicionNPC, Collider colliderNPC, Vector3 direccionPaso, float radio = 2.4f)
        {
            float sqrRadio = radio * radio;
            for (int i = 0; i < TodasLasPuertas.Count; i++)
            {
                var puerta = TodasLasPuertas[i];
                if (puerta == null || !puerta.bloqueada) continue;

                if ((puerta.transform.position - posicionNPC).sqrMagnitude <= sqrRadio)
                {
                    puerta.PermitirPasoNPC(colliderNPC, direccionPaso);
                }
            }
        }
    }
}
