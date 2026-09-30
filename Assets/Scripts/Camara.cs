
using UnityEngine;
using UnityEngine.InputSystem;

public class Camara : MonoBehaviour
{
    [Header("Referencias")]
    public Transform Player;

    [Header("Configuración")]
    public float Sensibilidad = 0.5f;
    public float LimiteVertical = 85f;

    private float RotacionVertical = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Player == null)
        {
            Player = transform.parent;
        }

        if (Player == null)
        {
            Debug.LogError("No se encontró el Player.");
        }
    }

    void Update()
    {
        if (Mouse.current == null || Player == null)
            return;

        float mouseX = Mouse.current.delta.x.ReadValue();
        float mouseY = Mouse.current.delta.y.ReadValue();

        // Rotación vertical de la cámara
        RotacionVertical -= mouseY * Sensibilidad;
        RotacionVertical = Mathf.Clamp(
            RotacionVertical,
            -LimiteVertical,
            LimiteVertical
        );

        transform.localRotation =
            Quaternion.Euler(RotacionVertical, 0f, 0f);

        // Rotación horizontal del jugador
        Player.Rotate(
            Vector3.up * mouseX * Sensibilidad
        );
    }
}