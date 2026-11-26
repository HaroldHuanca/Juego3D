using UnityEngine;

namespace Mundo1
{
    /// <summary>
    /// Identifica una caja física como uno de los 4 paquetes secuenciales de Mundo 1
    /// y recuerda su transform inicial para poder devolverla a su origen si es necesario.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaqueteMundo1 : MonoBehaviour
    {
        [Header("Datos del Paquete")]
        [Range(1, 4)]
        [SerializeField] private int numeroPaquete = 1;
        [SerializeField] private string nombrePaquete = "Paquete #1";
        [SerializeField] private string ubicacionDescripcion = "Almacén interior de la tienda";

        private Vector3 posicionInicial;
        private Quaternion rotacionInicial;
        private Vector3 escalaInicial;
        private bool origenGuardado = false;

        public int NumeroPaquete => numeroPaquete;
        public string NombrePaquete => string.IsNullOrEmpty(nombrePaquete) ? $"Paquete #{numeroPaquete}" : nombrePaquete;
        public string UbicacionDescripcion => ubicacionDescripcion;

        private void Awake()
        {
            GuardarOrigenSiFalta();
        }

        public void GuardarOrigenSiFalta()
        {
            if (origenGuardado) return;
            posicionInicial = transform.position;
            rotacionInicial = transform.rotation;
            escalaInicial = transform.localScale;
            origenGuardado = true;
        }

        public void RestaurarEnOrigen()
        {
            GuardarOrigenSiFalta();
            transform.SetParent(null);
            transform.position = posicionInicial;
            transform.rotation = rotacionInicial;
            transform.localScale = escalaInicial;

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void Configurar(int numero, string nombre, string ubicacion)
        {
            numeroPaquete = Mathf.Clamp(numero, 1, 4);
            nombrePaquete = nombre;
            ubicacionDescripcion = ubicacion;
        }
    }
}
