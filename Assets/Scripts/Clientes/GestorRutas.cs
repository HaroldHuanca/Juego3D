using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tienda.Clientes
{
    [Serializable]
    public class RutaCamino
    {
        public string nombreRuta = "Camino";
        [Tooltip("Puntos ordenados que forman este camino")]
        public Transform[] puntos;
    }

    [DefaultExecutionOrder(-100)]
    public class GestorRutas : MonoBehaviour
    {
        public static GestorRutas Instance { get; private set; }

        [Header("--- RUTAS DE LLEGADA EXTERIOR (Intersección en T) ---")]
        [Tooltip("Diferentes caminos desde donde pueden venir los clientes (ej. Desde la Izquierda, Desde la Derecha, Desde la Calle Central)")]
        [SerializeField] private List<RutaCamino> rutasLlegada = new List<RutaCamino>();

        [Header("--- PUNTOS INTERIORES DE LA TIENDA ---")]
        [Tooltip("Estantes o góndolas donde los clientes se detienen a mirar productos")]
        [SerializeField] private Transform[] puntosEstantes;

        [Header("--- CAJA REGISTRADORA Y FILA DE ESPERA ---")]
        [Tooltip("Punto exacto frente al mostrador donde se atiende al primer cliente")]
        [SerializeField] private Transform puntoCaja;

        [Tooltip("Distancia en metros entre las personas que esperan en la fila detrás de la caja")]
        [SerializeField] private float distanciaEntrePersonasEnFila = 1.3f;

        [Tooltip("Opcional: Si tus pasillos doblan, puedes asignar puntos manuales para la cola aquí")]
        [SerializeField] private Transform[] puntosFilaManuales;

        [Header("--- RUTAS DE SALIDA EXTERIOR (Hacia dónde se van) ---")]
        [Tooltip("Caminos posibles hacia donde pueden irse al salir de la tienda")]
        [SerializeField] private List<RutaCamino> rutasSalida = new List<RutaCamino>();

        // Cola de clientes esperando a pagar
        private List<ClienteIA> filaCaja = new List<ClienteIA>();

        public Transform[] PuntosEstantes => puntosEstantes;
        public Transform PuntoCaja => puntoCaja;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #region Sistema de Fila / Cola para la Caja
        /// <summary>
        /// Registra a un cliente en la cola de la caja. Retorna su posición en la fila (0 = le toca ser atendido).
        /// </summary>
        public int UnirseAColaCaja(ClienteIA cliente)
        {
            filaCaja.RemoveAll(c => c == null);

            if (!filaCaja.Contains(cliente))
            {
                filaCaja.Add(cliente);
            }

            return filaCaja.IndexOf(cliente);
        }

        /// <summary>
        /// Calcula la posición en el mundo para un cliente según su turno en la fila.
        /// </summary>
        public Vector3 ObtenerPosicionEnFila(int turnoEnFila)
        {
            if (puntoCaja == null) return Vector3.zero;

            // Si es el turno 0, es justo frente a la caja
            if (turnoEnFila == 0)
            {
                return puntoCaja.position;
            }

            // Si hay puntos de cola manuales configurados
            int indexManual = turnoEnFila - 1;
            if (puntosFilaManuales != null && indexManual < puntosFilaManuales.Length && puntosFilaManuales[indexManual] != null)
            {
                return puntosFilaManuales[indexManual].position;
            }

            // Cálculo automático: formar una fila india recta detrás de la caja
            return puntoCaja.position - (puntoCaja.forward * (turnoEnFila * distanciaEntrePersonasEnFila));
        }

        /// <summary>
        /// Llamado cuando un cliente termina de pagar y se marcha, para hacer avanzar a toda la fila.
        /// </summary>
        public void AvanzarFilaCaja(ClienteIA clienteAtendido)
        {
            if (filaCaja.Contains(clienteAtendido))
            {
                filaCaja.Remove(clienteAtendido);
            }

            filaCaja.RemoveAll(c => c == null);

            // Notificar a todos los que siguen en la fila de su nuevo turno
            for (int i = 0; i < filaCaja.Count; i++)
            {
                if (filaCaja[i] != null)
                {
                    filaCaja[i].ActualizarTurnoEnFila(i);
                }
            }
        }
        #endregion

        #region Rutas Aleatorias
        public Transform[] ObtenerRutaLlegadaAleatoria()
        {
            if (rutasLlegada == null || rutasLlegada.Count == 0) return null;
            var elegida = rutasLlegada[UnityEngine.Random.Range(0, rutasLlegada.Count)];
            return elegida != null ? elegida.puntos : null;
        }

        public Transform[] ObtenerRutaSalidaAleatoria()
        {
            if (rutasSalida == null || rutasSalida.Count == 0) return null;
            var elegida = rutasSalida[UnityEngine.Random.Range(0, rutasSalida.Count)];
            return elegida != null ? elegida.puntos : null;
        }
        #endregion

        #region Gizmos en la Vista Scene
        private void OnDrawGizmos()
        {
            // Rutas de llegada (Verde)
            if (rutasLlegada != null)
            {
                Gizmos.color = Color.green;
                foreach (var ruta in rutasLlegada)
                {
                    DibujarCamino(ruta.puntos);
                }
            }

            // Estantes (Cian con línea de mirada)
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

            // Caja y Fila de espera (Amarillo para caja, Naranja para la cola)
            if (puntoCaja != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(puntoCaja.position, 0.5f);
                Gizmos.DrawRay(puntoCaja.position, puntoCaja.forward * 1.2f);

                // Dibujar posiciones de fila estimada (3 personas detrás)
                Gizmos.color = new Color(1f, 0.7f, 0.2f); // Ámbar / Mostaza
                for (int i = 1; i <= 3; i++)
                {
                    Vector3 posCola = ObtenerPosicionEnFila(i);
                    Gizmos.DrawWireSphere(posCola, 0.35f);
                    Gizmos.DrawLine(ObtenerPosicionEnFila(i - 1), posCola);
                }
            }

            // Rutas de salida (Rojo)
            if (rutasSalida != null)
            {
                Gizmos.color = new Color(1f, 0.3f, 0f);
                foreach (var ruta in rutasSalida)
                {
                    DibujarCamino(ruta.puntos);
                }
            }
        }

        private void DibujarCamino(Transform[] puntos)
        {
            if (puntos == null || puntos.Length == 0) return;
            for (int i = 0; i < puntos.Length; i++)
            {
                if (puntos[i] == null) continue;
                Gizmos.DrawWireSphere(puntos[i].position, 0.35f);
                if (i > 0 && puntos[i - 1] != null)
                {
                    Gizmos.DrawLine(puntos[i - 1].position, puntos[i].position);
                }
            }
        }
        #endregion
    }
}
