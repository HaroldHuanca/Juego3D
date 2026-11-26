
using UnityEngine;
using UnityEngine.InputSystem;

public class ControladorAcciones : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ControladorEquipamiento equipamiento;

    void Awake()
    {
        if (equipamiento == null)
            equipamiento = GetComponent<ControladorEquipamiento>();

        if (equipamiento == null)
            Debug.LogError(
                "No se encontró ControladorEquipamiento."
            );
    }

    void Update()
    {
        if (Keyboard.current == null || equipamiento == null)
            return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
            EjecutarAccion();
    }

    void EjecutarAccion()
    {
        GameObject objetoVisual = equipamiento.ObjetoVisual;

        if (objetoVisual == null)
            return;

        AccionHerramienta accion =
            objetoVisual.GetComponentInChildren<AccionHerramienta>(true);

        if (accion == null)
        {
            Debug.Log("El objeto equipado no tiene una acción.");
            return;
        }

        accion.Ejecutar();
    }
}
