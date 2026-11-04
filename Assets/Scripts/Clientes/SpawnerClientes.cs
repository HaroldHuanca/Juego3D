using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tienda.Clientes
{
    [DefaultExecutionOrder(-50)]
    public class SpawnerClientes : MonoBehaviour
    {
        public static SpawnerClientes Instance { get; private set; }

        [Header("Prefabs de Clientes")]
        [Tooltip("Arrastra aquí los 5 prefabs de clientes (Remy, Megan, Adan, Sophie, Josh)")]
        [SerializeField] private GameObject[] prefabsClientes;

        [Header("Frecuencia de Llegada (Segundos)")]
        [Tooltip("Tiempo mínimo de espera entre clientes")]
        [SerializeField] private float tiempoMinimoEspera = 15f;

        [Tooltip("Tiempo máximo de espera entre clientes")]
        [SerializeField] private float tiempoMaximoEspera = 30f;

        [Header("Capacidad de la Tienda")]
        [Tooltip("Máximo número de clientes simultáneos permitidos en el mapa al mismo tiempo")]
        [SerializeField] private int maxClientesSimultaneos = 3;

        [Header("Opciones de Inicio")]
        [Tooltip("Generar el primer cliente automáticamente a los 2 segundos de iniciar")]
        [SerializeField] private bool generarPrimerClienteAlIniciar = true;

        [Header("Clientes Activos en el Mapa (Lectura)")]
        [SerializeField] private List<ClienteIA> clientesActivos = new List<ClienteIA>();

        private Coroutine rutinaSpawn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            rutinaSpawn = StartCoroutine(RutinaCicloSpawner());
        }

        private IEnumerator RutinaCicloSpawner()
        {
            // Comprobar si hay personajes manuales activos en la Hierarchy
            var iniciales = FindObjectsOfType<ClienteIA>();
            if (iniciales.Length >= maxClientesSimultaneos)
            {
                Debug.LogWarning($"[SpawnerClientes] Ya hay {iniciales.Length} clientes activos en la escena. Desactiva los NPCs de la Hierarchy para que solo el Spawner los controle.");
            }

            // Esperar 6 segundos antes del primer cliente para iniciar con calma
            if (generarPrimerClienteAlIniciar)
            {
                yield return new WaitForSeconds(6.0f);
                if (ContarClientesEnEscena() < maxClientesSimultaneos)
                {
                    GenerarClienteAleatorio();
                }
            }

            while (true)
            {
                float tiempoEspera = Random.Range(tiempoMinimoEspera, tiempoMaximoEspera);
                yield return new WaitForSeconds(tiempoEspera);

                // Control estricto: solo genera si el número total en la escena es menor al límite
                if (ContarClientesEnEscena() < maxClientesSimultaneos)
                {
                    GenerarClienteAleatorio();
                }
            }
        }

        public int ContarClientesEnEscena()
        {
            clientesActivos.RemoveAll(c => c == null);
            var todos = FindObjectsOfType<ClienteIA>();
            return todos != null ? todos.Length : 0;
        }

        /// <summary>
        /// Elige un personaje al azar de la lista y lo hace aparecer en una de las rutas exteriores.
        /// </summary>
        [ContextMenu("Generar Cliente Ahora")]
        public void GenerarClienteAleatorio()
        {
            if (prefabsClientes == null || prefabsClientes.Length == 0)
            {
                Debug.LogWarning("[SpawnerClientes] No hay prefabs asignados en la lista de clientes.");
                return;
            }

            // Elegir personaje al azar
            GameObject prefabElegido = prefabsClientes[Random.Range(0, prefabsClientes.Length)];
            if (prefabElegido == null) return;

            // Instanciar
            GameObject nuevoClienteObj = Instantiate(prefabElegido);
            nuevoClienteObj.name = prefabElegido.name + "_" + Random.Range(100, 999);

            ClienteIA clienteIA = nuevoClienteObj.GetComponent<ClienteIA>();
            if (clienteIA != null)
            {
                clientesActivos.Add(clienteIA);
            }
        }

        /// <summary>
        /// Llamado por ClienteIA al destruirse para liberar cupo en la tienda.
        /// </summary>
        public void RegistrarSalidaCliente(ClienteIA cliente)
        {
            if (clientesActivos.Contains(cliente))
            {
                clientesActivos.Remove(cliente);
            }
        }
    }
}
