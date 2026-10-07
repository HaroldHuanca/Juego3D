
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float Velocidad = 5f;
    public float Gravedad = -9.81f;
    public float FuerzaSalto = 1.5f;

    private CharacterController controller;
    private Vector3 velocidadVertical;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Detectar si el personaje está en el suelo
        bool enSuelo = controller.isGrounded;

        if (enSuelo && velocidadVertical.y < 0)
        {
            velocidadVertical.y = -2f;
        }

        // Entrada del teclado usando New Input System
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontal = -1f;

            if (Keyboard.current.dKey.isPressed)
                horizontal = 1f;

            if (Keyboard.current.wKey.isPressed)
                vertical = 1f;

            if (Keyboard.current.sKey.isPressed)
                vertical = -1f;
        }

        // Movimiento relativo a la orientación del jugador
        Vector3 movimiento =
            transform.right * horizontal +
            transform.forward * vertical;

        // Evitar que el movimiento diagonal sea más rápido
        movimiento = Vector3.ClampMagnitude(movimiento, 1f);

        controller.Move(movimiento * Velocidad * Time.deltaTime);

        // Salto
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            enSuelo)
        {
            velocidadVertical.y = Mathf.Sqrt(
                FuerzaSalto * -2f * Gravedad
            );
        }

        // Gravedad
        velocidadVertical.y += Gravedad * Time.deltaTime;

        controller.Move(velocidadVertical * Time.deltaTime);
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Verificar si el objeto con el que chocamos tiene un Rigidbody
        Rigidbody body = hit.collider.attachedRigidbody;

        // Si no tiene Rigidbody, o está marcado como IsKinematic, no hacemos nada
        if (body == null || body.isKinematic)
        {
            return;
        }

        // Asegurarnos de que no estamos empujando algo que esté debajo de nosotros
        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        // Calcular la dirección del empuje (usando la dirección en la que camina el jugador)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Aplicar la fuerza al Rigidbody de la puerta
        // Puedes cambiar el "Velocidad" por un número fijo (ej. 5f) si quieres más fuerza
        body.AddForceAtPosition(pushDir * Velocidad, hit.point, ForceMode.Impulse);
    }
}