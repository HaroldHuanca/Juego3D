
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

    [Header("Temblor en Persecución")]
    [SerializeField] private float amplitudTemblorPos = 0.032f;
    [SerializeField] private float amplitudTemblorRot = 1.65f;
    [SerializeField] private float frecuenciaTemblor = 22f;

    private float RotacionVertical = 0f;
    private PlayerMovement movimiento;
    private Vector3 posicionReposo;
    private Vector3 desplazamientoBalanceo;
    private Vector3 rotacionBalanceo;
    private float distanciaAnterior;
    private float intensidadTemblorObjetivo = 0f;
    private float intensidadTemblorActual = 0f;
    private float multiplicadorSensibilidad = 1f;
    private bool controlManualPausado = false;
    private Camera camaraComponente;
    private Coroutine rutinaFov;
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
        camaraComponente = GetComponent<Camera>();
    }

    void Update()
    {
        if (controlManualPausado || Mouse.current == null || Player == null)
            return;

        float sens = Sensibilidad * multiplicadorSensibilidad;
        float mouseX = Mouse.current.delta.x.ReadValue();
        float mouseY = Mouse.current.delta.y.ReadValue();

        // Rotación vertical de la cámara
        RotacionVertical -= mouseY * sens;
        RotacionVertical = Mathf.Clamp(
            RotacionVertical,
            -LimiteVertical,
            LimiteVertical
        );

        transform.localRotation =
            Quaternion.Euler(RotacionVertical, 0f, 0f);

        // Rotación horizontal del jugador
        Player.Rotate(
            Vector3.up * mouseX * sens
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

        intensidadTemblorActual = Mathf.MoveTowards(intensidadTemblorActual, intensidadTemblorObjetivo, Time.deltaTime * 3.0f);
        Vector3 offsetTemblorPos = Vector3.zero;
        Vector3 offsetTemblorRot = Vector3.zero;

        if (intensidadTemblorActual > 0.005f)
        {
            float t = Time.unscaledTime * frecuenciaTemblor;
            float nx = (Mathf.PerlinNoise(t, 1.37f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(4.91f, t) - 0.5f) * 2f;
            float rx = (Mathf.PerlinNoise(t * 1.15f, 9.23f) - 0.5f) * 2f;
            float ry = (Mathf.PerlinNoise(13.7f, t * 1.1f) - 0.5f) * 2f;
            float rz = (Mathf.PerlinNoise(t * 0.95f, 21.4f) - 0.5f) * 2f;

            offsetTemblorPos = new Vector3(nx, ny, 0f) * (amplitudTemblorPos * intensidadTemblorActual);
            offsetTemblorRot = new Vector3(rx, ry, rz) * (amplitudTemblorRot * intensidadTemblorActual);
        }

        transform.localPosition = posicionReposo + desplazamientoBalanceo + offsetTemblorPos;
        transform.localRotation = Quaternion.Euler(RotacionVertical, 0f, 0f) * Quaternion.Euler(rotacionBalanceo + offsetTemblorRot);
    }

    public void EstablecerTemblorPersecucion(float intensidad01)
    {
        intensidadTemblorObjetivo = Mathf.Clamp01(intensidad01);
    }

    public void EstablecerModificadorSensibilidad(float mult)
    {
        multiplicadorSensibilidad = Mathf.Max(0.02f, mult);
    }

    public void EstablecerControlManualPausado(bool pausado)
    {
        controlManualPausado = pausado;
    }

    public void AplicarReflejoDolorHaciaAbajo(float gradosAbajo = 22f, float duracion = 0.25f)
    {
        StartCoroutine(RutinaBajarVistaDolor(gradosAbajo, duracion));
    }

    private System.Collections.IEnumerator RutinaBajarVistaDolor(float gradosAbajo, float duracion)
    {
        float inicio = RotacionVertical;
        float fin = Mathf.Clamp(RotacionVertical + gradosAbajo, -LimiteVertical, LimiteVertical);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            RotacionVertical = Mathf.Lerp(inicio, fin, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, duracion)));
            yield return null;
        }
        RotacionVertical = fin;
    }

    public System.Collections.IEnumerator RutinaZoomHaciaObjetivo(Transform objetivo, float fovZoom = 28f, float duracion = 0.50f)
    {
        if (camaraComponente == null) camaraComponente = GetComponent<Camera>();
        controlManualPausado = true;

        float fovIni = camaraComponente != null ? camaraComponente.fieldOfView : 50f;
        Quaternion rotPlayerIni = Player != null ? Player.rotation : Quaternion.identity;
        float pitchIni = RotacionVertical;

        Quaternion rotPlayerFin = rotPlayerIni;
        float pitchFin = pitchIni;
        if (objetivo != null && Player != null)
        {
            Vector3 targetPos = objetivo.position + Vector3.up * 1.45f;
            Vector3 dirPlano = targetPos - Player.position;
            dirPlano.y = 0f;
            if (dirPlano.sqrMagnitude > 0.01f)
            {
                rotPlayerFin = Quaternion.LookRotation(dirPlano);
            }

            Vector3 dirCam = targetPos - transform.position;
            float distPlana = new Vector2(dirCam.x, dirCam.z).magnitude;
            if (distPlana > 0.01f)
            {
                pitchFin = Mathf.Clamp(-Mathf.Atan2(dirCam.y, distPlana) * Mathf.Rad2Deg, -LimiteVertical, LimiteVertical);
            }
        }

        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duracion)));
            if (Player != null) Player.rotation = Quaternion.Slerp(rotPlayerIni, rotPlayerFin, p);
            RotacionVertical = Mathf.Lerp(pitchIni, pitchFin, p);
            if (camaraComponente != null) camaraComponente.fieldOfView = Mathf.Lerp(fovIni, fovZoom, p);
            yield return null;
        }

        controlManualPausado = false;
    }

    public void RestaurarFOV(float fovDestino = 50f, float duracion = 0.40f)
    {
        if (camaraComponente == null) camaraComponente = GetComponent<Camera>();
        if (camaraComponente == null) return;
        if (rutinaFov != null) StopCoroutine(rutinaFov);
        rutinaFov = StartCoroutine(RutinaCambiarFOV(fovDestino, duracion));
    }

    private System.Collections.IEnumerator RutinaCambiarFOV(float destino, float duracion)
    {
        float ini = camaraComponente.fieldOfView;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            camaraComponente.fieldOfView = Mathf.Lerp(ini, destino, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duracion))));
            yield return null;
        }
        camaraComponente.fieldOfView = destino;
        rutinaFov = null;
    }

    public System.Collections.IEnumerator RutinaGirarRelativoSuave(float gradosHorizontal, float duracion)
    {
        if (Player == null) yield break;
        controlManualPausado = true;
        Quaternion ini = Player.rotation;
        Quaternion fin = ini * Quaternion.Euler(0f, gradosHorizontal, 0f);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duracion)));
            Player.rotation = Quaternion.Slerp(ini, fin, p);
            yield return null;
        }
        Player.rotation = fin;
    }

    public void SincronizarRotacionVertical(float nuevaVertical = 0f)
    {
        RotacionVertical = Mathf.Clamp(nuevaVertical, -LimiteVertical, LimiteVertical);
    }

    private void OnDisable()
    {
        intensidadTemblorObjetivo = 0f;
        intensidadTemblorActual = 0f;
        transform.localPosition -= desplazamientoBalanceo;
        desplazamientoBalanceo = Vector3.zero;
        transform.localRotation = Quaternion.Euler(RotacionVertical, 0f, 0f);
        rotacionBalanceo = Vector3.zero;
        IntensidadPasos = 0f;
        MezclaEsprint = 0f;
        if (movimiento != null) distanciaAnterior = movimiento.DistanciaCaminada;
    }
}
