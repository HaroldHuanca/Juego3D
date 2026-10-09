using UnityEngine;

/// <summary>Reproduce los clips del rig sin desplazar al jugador.</summary>
[DefaultExecutionOrder(200)]
public class BrazosPrimeraPersona : MonoBehaviour
{
    public Animator animador;
    public AnimationClip reposo;
    public AnimationClip recoger;
    public AnimationClip empujar;
    public Transform puntoVista;
    [Header("Balanceo de brazos al caminar")]
    [Min(0f)] public float BalanceoCaminata = 0.012f;
    [Min(0f)] public float BalanceoEsprint = 0.018f;
    [Range(0f, 1f)] public float momentoContacto = 0.55f;
    private float finAccion;
    private Camara mirada;
    private Vector3 posicionRig;
    private Vector3 balanceoRig;

    public bool Ocupado => Time.time < finAccion;
    public float TiempoContacto => recoger == null ? 0f : recoger.length * momentoContacto;

    public bool Recoger() => Reproducir("Recoger", recoger);
    public bool Empujar() => Reproducir("Empujar", empujar);

    private void Awake()
    {
        if (animador != null) posicionRig = animador.transform.localPosition;
        if (puntoVista != null) mirada = puntoVista.GetComponent<Camara>();
        // La cápsula original está escalada de forma no uniforme. Separar el rig
        // evita que los brazos se deformen al mirar arriba o abajo.
        if (puntoVista == null) return;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (puntoVista != null)
            transform.SetPositionAndRotation(puntoVista.position, puntoVista.rotation);
        if (animador == null || mirada == null) return;
        float intensidad = Ocupado || !mirada.isActiveAndEnabled ? 0f : mirada.IntensidadPasos;
        float amplitud = Mathf.Lerp(BalanceoCaminata, BalanceoEsprint, mirada.MezclaEsprint);
        Vector3 objetivo = new Vector3(-Mathf.Sin(mirada.FasePasos),
            Mathf.Cos(mirada.FasePasos * 2f) * 0.5f, 0f) * (amplitud * intensidad);
        balanceoRig = Vector3.Lerp(balanceoRig, objetivo, 1f - Mathf.Exp(-10f * Time.deltaTime));
        animador.transform.localPosition = posicionRig + balanceoRig;
    }

    private bool Reproducir(string estado, AnimationClip clip)
    {
        if (Ocupado || animador == null || clip == null || !isActiveAndEnabled) return false;
        animador.CrossFadeInFixedTime(estado, 0.08f, 0, 0f);
        finAccion = Time.time + clip.length;
        return true;
    }

    private void Update()
    {
        if (finAccion > 0f && !Ocupado)
        {
            finAccion = 0f;
            animador.CrossFadeInFixedTime("Reposo", 0.15f);
        }
    }

    private void OnDisable() { finAccion = 0f; }
}
