using UnityEngine;
using UnityEngine.Audio;

/// <summary>Un clip por apoyo; repite la lista siguiendo el ciclo real de caminata.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camara))]
public class PasosJugador : MonoBehaviour
{
    [Tooltip("Se reproducen en orden y vuelven al primero al terminar la lista.")]
    public AudioClip[] pasos = new AudioClip[0];
    [Range(0f, 1f)] public float volumen = 0.65f;
    [Tooltip("Opcional: grupo de un AudioMixer para controlar el volumen del jugador.")]
    public AudioMixerGroup grupoSalida;

    private Camara camara;
    private AudioSource fuente;
    private int siguientePaso;

    public int PasosReproducidos { get; private set; }
    public AudioClip UltimoPaso { get; private set; }

    private void Awake()
    {
        camara = GetComponent<Camara>();
        // Fuente exclusiva: no interrumpe otros efectos del jugador.
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false; // El ciclo lo marca cada apoyo, no la duración del archivo.
        fuente.spatialBlend = 0f;
        fuente.dopplerLevel = 0f;
    }

    private void OnEnable()
    {
        camara.PasoRealizado += ReproducirPaso;
    }

    private void OnDisable()
    {
        if (camara != null) camara.PasoRealizado -= ReproducirPaso;
        if (fuente != null) fuente.Stop();
    }

    private void OnDestroy()
    {
        if (fuente != null) Destroy(fuente);
    }

    private void ReproducirPaso()
    {
        if (pasos == null || pasos.Length == 0) return;
        // Permite dejar huecos en el Inspector sin perder el resto de sonidos.
        for (int intento = 0; intento < pasos.Length; intento++)
        {
            siguientePaso %= pasos.Length;
            AudioClip clip = pasos[siguientePaso];
            siguientePaso = (siguientePaso + 1) % pasos.Length;
            if (clip == null) continue;
            fuente.outputAudioMixerGroup = grupoSalida;
            fuente.PlayOneShot(clip, volumen);
            UltimoPaso = clip;
            PasosReproducidos++;
            return;
        }
    }
}
