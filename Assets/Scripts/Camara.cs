
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class Camara : MonoBehaviour
{
    [Header("Referencias")]
    public Transform Player;

    [Header("Configuración")]
    public float Sensibilidad = 0.5f;
    public float LimiteVertical = 85f;

    [Header("Balanceo al caminar")]
    public bool BalanceoActivo = true;
    [Min(0f)] public float BalanceoVertical = 0.012f;
    [Min(0f)] public float BalanceoHorizontal = 0.01f;
    [Range(0f, 3f)] public float InclinacionPasos = 0.12f;
    [Range(0f, 2f)] public float CabeceoPasos = 0f;
    [Min(0.1f)] public float LongitudCicloPasos = 2.4f;
    [Min(0.1f)] public float SuavizadoBalanceo = 12f;
    [Header("Balanceo al esprintar")]
    [Min(0f)] public float BalanceoVerticalEsprint = 0.018f;
    [Min(0f)] public float BalanceoHorizontalEsprint = 0.013f;
    [Range(0f, 3f)] public float InclinacionEsprint = 0.2f;
    [Min(0.1f)] public float LongitudCicloEsprint = 3.2f;

    private float RotacionVertical = 0f;
    private PlayerMovement movimiento;
    private Vector3 posicionReposo;
    private Vector3 desplazamientoBalanceo;
    private Vector3 rotacionBalanceo;
    private float distanciaAnterior;
    public float FasePasos { get; private set; }
    public float IntensidadPasos { get; private set; }
    public float MezclaEsprint { get; private set; }

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
        posicionReposo = transform.localPosition;
        if (Player != null) movimiento = Player.GetComponent<PlayerMovement>();
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

    private void LateUpdate()
    {
        float suavizadoRitmo = 1f - Mathf.Exp(-8f * Time.deltaTime);
        float velocidad = movimiento != null && movimiento.isActiveAndEnabled ? movimiento.VelocidadHorizontalActual : 0f;
        float intensidadObjetivo = BalanceoActivo && movimiento != null && movimiento.EnSuelo
            ? Mathf.Clamp01(velocidad / Mathf.Max(0.1f, movimiento.Velocidad)) : 0f;
        float esprintObjetivo = movimiento == null ? 0f : Mathf.InverseLerp(movimiento.Velocidad,
            Mathf.Max(movimiento.Velocidad + 0.01f, movimiento.VelocidadEsprint), velocidad);
        IntensidadPasos = Mathf.Lerp(IntensidadPasos, intensidadObjetivo, suavizadoRitmo);
        MezclaEsprint = Mathf.Lerp(MezclaEsprint, esprintObjetivo, suavizadoRitmo);
        if (movimiento != null)
        {
            float recorrido = Mathf.Max(0f, movimiento.DistanciaCaminada - distanciaAnterior);
            distanciaAnterior = movimiento.DistanciaCaminada;
            float longitud = Mathf.Lerp(LongitudCicloPasos, LongitudCicloEsprint, MezclaEsprint);
            // Acumular la fase evita saltos al cambiar entre caminar y correr.
            FasePasos = Mathf.Repeat(FasePasos + recorrido / Mathf.Max(0.1f, longitud) * Mathf.PI * 2f, Mathf.PI * 2f);
        }
        float intensidad = IntensidadPasos;
        float fase = FasePasos;
        float horizontal = Mathf.Lerp(BalanceoHorizontal, BalanceoHorizontalEsprint, MezclaEsprint);
        float vertical = Mathf.Lerp(BalanceoVertical, BalanceoVerticalEsprint, MezclaEsprint);
        float inclinacion = Mathf.Lerp(InclinacionPasos, InclinacionEsprint, MezclaEsprint);
        // Ondas suaves, sin la esquina de Abs(Sin) ni golpes de cabeceo por apoyo.
        Vector3 objetivo = new Vector3(Mathf.Sin(fase) * horizontal,
            -Mathf.Cos(fase * 2f) * vertical, 0f) * intensidad;
        Vector3 giroObjetivo = new Vector3(Mathf.Cos(fase * 2f) * CabeceoPasos,
            0f, -Mathf.Sin(fase) * inclinacion) * intensidad;
        float suavizado = 1f - Mathf.Exp(-SuavizadoBalanceo * Time.deltaTime);
        desplazamientoBalanceo = Vector3.Lerp(desplazamientoBalanceo, objetivo,
            suavizado);
        rotacionBalanceo = Vector3.Lerp(rotacionBalanceo, giroObjetivo, suavizado);
        transform.localPosition = posicionReposo + desplazamientoBalanceo;
        transform.localRotation = Quaternion.Euler(RotacionVertical, 0f, 0f) * Quaternion.Euler(rotacionBalanceo);
    }

    private void OnDisable()
    {
        transform.localPosition -= desplazamientoBalanceo;
        desplazamientoBalanceo = Vector3.zero;
        transform.localRotation = Quaternion.Euler(RotacionVertical, 0f, 0f);
        rotacionBalanceo = Vector3.zero;
        IntensidadPasos = 0f;
        MezclaEsprint = 0f;
        if (movimiento != null) distanciaAnterior = movimiento.DistanciaCaminada;
    }
}
