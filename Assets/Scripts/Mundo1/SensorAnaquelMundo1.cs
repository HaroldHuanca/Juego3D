using UnityEngine;

namespace Mundo1
{
    /// <summary>
    /// Se coloca en un anaquel o estante que contenga un ObjetoAleatorio.
    /// Detecta cuando el jugador rellena el anaquel con una caja y notifica al DirectorTurno.
    /// </summary>
    [DisallowMultipleComponent]
    public class SensorAnaquelMundo1 : MonoBehaviour
    {
        [Tooltip("Renderer del objeto del anaquel que se vuelve visible al rellenar")]
        [SerializeField] private Renderer objetoRenderer;

        private bool yaEstabaVisible = false;
        private bool contabilizado = false;

        private void Awake()
        {
            CachearRenderer();
        }

        private void Start()
        {
            CachearRenderer();
            if (objetoRenderer != null)
            {
                yaEstabaVisible = objetoRenderer.enabled;
            }
        }

        private void CachearRenderer()
        {
            if (objetoRenderer != null) return;
            var oa = GetComponent<ObjetoAleatorio>();
            if (oa != null) objetoRenderer = oa.GetComponent<Renderer>();
            if (objetoRenderer == null) objetoRenderer = GetComponentInChildren<Renderer>();
        }

        private void Update()
        {
            if (contabilizado || objetoRenderer == null) return;

            // Si antes estaba oculto/vacío y ahora está visible (rellenado con la caja)
            if (!yaEstabaVisible && objetoRenderer.enabled)
            {
                contabilizado = true;
                yaEstabaVisible = true;

                if (DirectorTurno.Instance != null)
                {
                    DirectorTurno.Instance.NotificarCajaRepuesta();
                }
            }
            else
            {
                yaEstabaVisible = objetoRenderer.enabled;
            }
        }

        /// <summary>
        /// Permite forzar el estado de vacío al inicio del nivel para que el jugador deba rellenarlo.
        /// </summary>
        public void ForzarVacio()
        {
            CachearRenderer();
            if (objetoRenderer != null)
            {
                objetoRenderer.enabled = false;
                yaEstabaVisible = false;
                contabilizado = false;
            }
        }
    }
}
