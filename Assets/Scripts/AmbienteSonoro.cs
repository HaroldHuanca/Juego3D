using UnityEngine;
using UnityEngine.Audio;

/// <summary>Dos bucles continuos con mezcla gradual según la posición del oyente.</summary>
[DisallowMultipleComponent]
public class AmbienteSonoro : MonoBehaviour
{
    [System.Serializable]
    public class Interior
    {
        public string nombre;
        public Vector3 centro;
        public Vector3 tamano;
    }

    public Transform oyente;
    public AudioClip vhsInterior;
    public AudioClip grillosExterior;
    public Interior[] interiores = new Interior[0];
    [Range(0f, 1f)] public float volumenInterior = 0.25f;
    [Range(0f, 1f)] public float volumenExterior = 0.45f;
    [Min(0.1f)] public float segundosTransicion = 1.5f;
    [Min(0.1f)] public float anchoTransicion = 1f;
    public AudioMixerGroup grupoSalida;
    private AudioSource interior, exterior;
    private float velocidadMezcla, entrada;
    public float MezclaInterior { get; private set; }

    private void Awake()
    {
        interior = CrearFuente(vhsInterior);
        exterior = CrearFuente(grillosExterior);
    }

    private AudioSource CrearFuente(AudioClip clip)
    {
        var fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        fuente.spatialBlend = 0f;
        fuente.dopplerLevel = 0f;
        fuente.volume = 0f;
        fuente.priority = 192;
        fuente.clip = clip;
        fuente.outputAudioMixerGroup = grupoSalida;
        return fuente;
    }

    private void OnEnable()
    {
        if (oyente == null && Camera.main != null) oyente = Camera.main.transform;
        MezclaInterior = oyente != null ? EvaluarInterior(oyente.position) : 0f;
        velocidadMezcla = 0f;
        entrada = 0f;
        interior.volume = exterior.volume = 0f;
        // Ambos avanzan continuamente: cruzar un umbral nunca reinicia un clip.
        double inicio = AudioSettings.dspTime + 0.2;
        if (interior.clip != null) interior.PlayScheduled(inicio);
        if (exterior.clip != null) exterior.PlayScheduled(inicio);
    }

    public float EvaluarInterior(Vector3 posicion)
    {
        float mezcla = 0f;
        foreach (var zona in interiores)
        {
            if (zona == null) continue;
            Vector3 d = posicion - zona.centro;
            Vector3 mitad = zona.tamano * 0.5f;
            float profundidad = Mathf.Min(mitad.x - Mathf.Abs(d.x),
                mitad.y - Mathf.Abs(d.y), mitad.z - Mathf.Abs(d.z));
            float peso = Mathf.Clamp01(0.5f + profundidad / Mathf.Max(0.1f, anchoTransicion));
            mezcla = Mathf.Max(mezcla, Mathf.SmoothStep(0f, 1f, peso));
        }
        return mezcla;
    }

    private void Update()
    {
        if (oyente == null) return;
        // Una pausa de carga no debe convertir el fundido en un salto de volumen.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        MezclaInterior = Mathf.SmoothDamp(MezclaInterior, EvaluarInterior(oyente.position),
            ref velocidadMezcla, Mathf.Max(0.1f, segundosTransicion) * 0.5f, Mathf.Infinity, dt);
        entrada = Mathf.MoveTowards(entrada, 1f, dt / Mathf.Max(0.1f, segundosTransicion));
        float fase = Mathf.Clamp01(MezclaInterior) * Mathf.PI * 0.5f;
        interior.volume = Mathf.Sin(fase) * volumenInterior * entrada;
        exterior.volume = Mathf.Cos(fase) * volumenExterior * entrada;
    }

    private void OnDisable()
    {
        if (interior != null) interior.Stop();
        if (exterior != null) exterior.Stop();
    }

    private void OnDestroy()
    {
        if (interior != null) Destroy(interior);
        if (exterior != null) Destroy(exterior);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var zona in interiores)
            if (zona != null) Gizmos.DrawWireCube(zona.centro, zona.tamano);
    }
}
