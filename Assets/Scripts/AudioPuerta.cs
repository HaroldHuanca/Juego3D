using UnityEngine;
using UnityEngine.Audio;

/// <summary>Audio de puertas físicas, sincronizado con el ángulo real de la bisagra.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HingeJoint))]
public class AudioPuerta : MonoBehaviour
{
    public AudioClip[] abrir = new AudioClip[0];
    public AudioClip[] cerrar = new AudioClip[0];
    [Range(0f, 1f)] public float volumen = 0.8f;
    public AudioMixerGroup grupoSalida;
    [Min(0.1f)] public float distanciaMinima = 1.5f;
    [Min(1f)] public float distanciaMaxima = 18f;
    [Header("Sincronización (grados desde la posición cerrada)")]
    [Min(0.1f)] public float umbralApertura = 3f;
    [Min(0f)] public float umbralCierre = 1f;

    private HingeJoint bisagra;
    private CierrePuerta control;
    private AudioSource fuente;
    private float anguloAnterior, tiempoReposo;
    private bool abierta, aperturaPermitida;
    private int ultimoAbrir = -1, ultimoCerrar = -1;
    public int AperturasReproducidas { get; private set; }
    public int CierresReproducidos { get; private set; }
    public AudioClip UltimoSonido { get; private set; }

    private float Angulo => control != null ? control.AnguloActual :
        Mathf.DeltaAngle(bisagra.useSpring ? bisagra.spring.targetPosition : 0f, bisagra.angle);

    private void Awake()
    {
        bisagra = GetComponent<HingeJoint>();
        control = GetComponent<CierrePuerta>();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 1f;
        fuente.dopplerLevel = 0f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
    }

    private void OnEnable()
    {
        anguloAnterior = Angulo;
        abierta = Mathf.Abs(anguloAnterior) > umbralApertura;
        aperturaPermitida = !abierta;
        tiempoReposo = 0f;
    }

    // El audio espera a que el movimiento alcance el umbral de apertura.
    public void NotificarApertura()
    {
        if (!abierta && Mathf.Abs(Angulo) <= umbralApertura) aperturaPermitida = true;
    }

    private void FixedUpdate()
    {
        float angulo = Angulo;
        float distancia = Mathf.Abs(angulo);
        float cierre = Mathf.Min(umbralCierre, umbralApertura * 0.5f);
        if (abierta)
        {
            // Detectar también el cruce entre ticks de física en un portazo rápido.
            if (distancia <= cierre || angulo * anguloAnterior < 0f)
            {
                abierta = false;
                aperturaPermitida = false;
                tiempoReposo = 0f;
                if (Reproducir(cerrar, ref ultimoCerrar)) CierresReproducidos++;
            }
        }
        else if (aperturaPermitida && distancia >= umbralApertura)
        {
            abierta = true;
            aperturaPermitida = false;
            if (Reproducir(abrir, ref ultimoAbrir)) AperturasReproducidas++;
        }
        else if (!aperturaPermitida)
        {
            // El rebote del muelle no es una nueva interacción.
            tiempoReposo = distancia <= cierre && Mathf.Abs(bisagra.velocity) < 5f
                ? tiempoReposo + Time.fixedDeltaTime : 0f;
            if (tiempoReposo >= 0.2f) aperturaPermitida = true;
        }
        anguloAnterior = angulo;
    }

    private bool Reproducir(AudioClip[] clips, ref int ultimo)
    {
        if (clips == null || clips.Length == 0) return false;
        int validos = 0;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && i != ultimo) validos++;
        int elegido = ultimo;
        if (validos > 0)
        {
            int sorteo = Random.Range(0, validos);
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && i != ultimo && sorteo-- == 0) { elegido = i; break; }
        }
        if (elegido < 0 || elegido >= clips.Length || clips[elegido] == null) return false;
        ultimo = elegido;
        fuente.outputAudioMixerGroup = grupoSalida;
        fuente.minDistance = distanciaMinima;
        fuente.maxDistance = Mathf.Max(distanciaMinima, distanciaMaxima);
        // El golpe de cierre sustituye cualquier crujido de apertura pendiente.
        fuente.Stop();
        fuente.clip = clips[elegido];
        fuente.volume = volumen;
        fuente.Play();
        UltimoSonido = clips[elegido];
        return true;
    }

    private void OnDisable() { if (fuente != null) fuente.Stop(); }
    private void OnDestroy() { if (fuente != null) Destroy(fuente); }
}
