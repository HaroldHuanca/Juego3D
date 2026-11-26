using UnityEngine;

namespace Mundo1
{
    /// <summary>
    /// Coloca este componente en un objeto vacío visible del Hierarchy para indicar
    /// fácilmente el nombre de la siguiente escena al terminar Mundo 1.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConfiguracionSiguienteMapa : MonoBehaviour
    {
        public static ConfiguracionSiguienteMapa Instance { get; private set; }

        [Header("COLOCAR EL NOMBRE DE LA SIGUIENTE ESCENA")]
        [Tooltip("Nombre exacto de la escena agregada en Build Settings (ej. NIvelHarold)")]
        [SerializeField] private string nombreSiguienteEscena = "NIvelHarold";

        public string NombreSiguienteEscena => nombreSiguienteEscena;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }
    }
}
