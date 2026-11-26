using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Mundo1;
using Mundo1.UI;

/// <summary>Patrulla y persigue únicamente dentro del volumen del almacén.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MonstruoWarehouse : MonoBehaviour
{
    public BoxCollider zona;
    public Transform jugador;
    public Animator animador;
    public NavMeshData navegacion;
    public float radioDeteccion = 13f;
    public float radioAbandono = 19f;
    public float velocidadCaminar = 1.5f;
    public float velocidadPersecucion = 2.3f;

    [Header("Detección Justa (Vista y Pasos)")]
    [Tooltip("Distancia a la que escucha al jugador si está esprintando aunque haya anaqueles de por medio")]
    public float radioEscuchaSprint = 15f;
    [Tooltip("Distancia mínima de proximidad a la que siempre siente al jugador aunque camine lento")]
    public float radioDeteccionCercana = 2.6f;
    [Tooltip("Segundos que mantiene el rastro al perder de vista detrás de un estante")]
    public float memoriaPersecucion = 2.2f;
    [Tooltip("Distancia a la que atrapa al jugador y dispara el susto")]
    public float distanciaCaptura = 1.65f;

    [Header("Susto y Retorno a Tienda (Sin Game Over)")]
    [Tooltip("Si se deja vacío busca automáticamente 'Punto_JugadorFrenteGerente'")]
    public Transform puntoRetornoTienda;
    [SerializeField] private AudioClip sonidoJumpscare;
    [SerializeField] private AudioClip sonidoDeteccion;
    [SerializeField] private float volumenSusto = 1f;

    [Header("Animación de caminata")]
    [Tooltip("Ritmo del ciclo a la velocidad de referencia. Menor valor = pasos más lentos.")]
    [Min(0.01f)] public float ritmoAnimacion = 0.65f;
    [Tooltip("Velocidad de desplazamiento en m/s que corresponde al ritmo indicado.")]
    [Min(0.01f)] public float velocidadReferenciaAnimacion = 1.5f;

    public enum Estado { Patrullando, Persiguiendo, Regresando }
    [SerializeField] private Estado estado;
    public Estado EstadoActual => estado;

    private NavMeshAgent agente;
    private NavMeshDataInstance instancia;
    private Vector3 hogar;
    private float siguienteDecision;
    private Vector3 ultimaPosicionSegura;
    private NavMeshPath ruta;

    private PlayerMovement playerMovement;
    private JugadorRecolector jugadorRecolector;
    private Camara camaraScript;
    private Camera camaraPrincipal;
    private float tiempoUltimaDeteccion = -10f;
    private bool enSecuenciaCaptura = false;
    private AudioSource audioSourceLocal;
    private CanvasGroup overlayOscuroCaptura;
    private Light luzSustoRostro;

    private NavMeshQueryFilter Filtro => new NavMeshQueryFilter { agentTypeID = agente.agentTypeID, areaMask = agente.areaMask };

    private void Start()
    {
        ruta = new NavMeshPath();
        agente = GetComponent<NavMeshAgent>();
        if (zona == null || navegacion == null) { Debug.LogError("Falta configurar el almacén del monstruo.", this); enabled = false; return; }
        instancia = NavMesh.AddNavMeshData(navegacion);
        agente.enabled = true;
        hogar = transform.position;
        if (!NavMesh.SamplePosition(hogar, out var inicio, 4f, Filtro) || !Dentro(inicio.position, agente.radius))
        { Debug.LogError("El monstruo no tiene un punto inicial transitable dentro del almacén.", this); enabled = false; return; }
        agente.Warp(inicio.position);
        hogar = ultimaPosicionSegura = inicio.position;

        if (jugador == null)
        {
            var movimiento = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (movimiento != null) jugador = movimiento.transform;
        }
        if (jugador != null)
        {
            playerMovement = jugador.GetComponent<PlayerMovement>();
            jugadorRecolector = jugador.GetComponent<JugadorRecolector>();
            camaraScript = jugador.GetComponentInChildren<Camara>(true);
            camaraPrincipal = jugador.GetComponentInChildren<Camera>(true);
        }
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;

        if (puntoRetornoTienda == null)
        {
            var goRet = GameObject.Find("Punto_JugadorFrenteGerente");
            if (goRet != null) puntoRetornoTienda = goRet.transform;
        }

        audioSourceLocal = GetComponent<AudioSource>();
        if (audioSourceLocal == null) audioSourceLocal = gameObject.AddComponent<AudioSource>();
        audioSourceLocal.playOnAwake = false;
        audioSourceLocal.spatialBlend = 0f;

#if UNITY_EDITOR
        if (sonidoJumpscare == null)
            sonidoJumpscare = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoJumpscare.mp3");
        if (sonidoDeteccion == null)
            sonidoDeteccion = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoRespiracionCerca.mp3");
#endif

        if (animador != null)
        {
            animador.applyRootMotion = false;
            animador.speed = 0f;
        }
    }

    public bool Dentro(Vector3 punto, float margen = 0f)
    {
        if (zona == null) return false;
        Vector3 local = zona.transform.InverseTransformPoint(punto) - zona.center;
        Vector3 mitad = zona.size * .5f;
        Vector3 escala = zona.transform.lossyScale;
        return Mathf.Abs(local.x) <= mitad.x - margen / Mathf.Abs(escala.x)
            && Mathf.Abs(local.z) <= mitad.z - margen / Mathf.Abs(escala.z)
            && Mathf.Abs(local.y) <= mitad.y;
    }

    private bool IrA(Vector3 destino)
    {
        if (!Dentro(destino, agente.radius) || !NavMesh.SamplePosition(destino, out var hit, 2f, Filtro)
            || !Dentro(hit.position, agente.radius) || !agente.CalculatePath(hit.position, ruta)
            || ruta.status != NavMeshPathStatus.PathComplete) return false;
        // Un destino interior no basta: también se rechazan rutas que crucen el exterior.
        foreach (var esquina in ruta.corners) if (!Dentro(esquina, agente.radius * .5f)) return false;
        return agente.SetPath(ruta);
    }

    private bool TieneLineaDeVision()
    {
        if (jugador == null) return false;
        Vector3 origen = transform.position + Vector3.up * 1.6f;
        Vector3 destino = jugador.position + Vector3.up * 1.35f;
        Vector3 dir = destino - origen;
        float dist = dir.magnitude;
        if (dist > radioDeteccion) return false;

        if (Physics.Raycast(origen, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == jugador || hit.transform.IsChildOf(jugador) || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return true;
            }
            return false;
        }

        return true;
    }

    private bool PuedeDetectarJugador(float distancia)
    {
        if (TieneLineaDeVision()) return true;
        if (distancia <= radioDeteccionCercana) return true;
        if (playerMovement != null && playerMovement.Esprintando && distancia <= radioEscuchaSprint) return true;
        return false;
    }

    private void Update()
    {
        if (enSecuenciaCaptura) return;
        if (agente == null || !agente.isOnNavMesh) return;

        if (!Dentro(transform.position, agente.radius * .5f))
        {
            agente.Warp(ultimaPosicionSegura);
            agente.ResetPath();
            estado = Estado.Regresando;
        }
        else ultimaPosicionSegura = transform.position;

        bool jugadorDentro = jugador != null && Dentro(jugador.position);
        float distancia = jugadorDentro ? Vector3.Distance(transform.position, jugador.position) : float.PositiveInfinity;

        if (jugadorDentro && distancia <= distanciaCaptura)
        {
            StartCoroutine(RutinaCapturaYSusto());
            return;
        }

        bool detectadoAhora = jugadorDentro && PuedeDetectarJugador(distancia);
        if (detectadoAhora)
        {
            tiempoUltimaDeteccion = Time.time;
        }

        bool mantenerPersecucion = jugadorDentro &&
                                   distancia <= radioAbandono &&
                                   (detectadoAhora || (Time.time - tiempoUltimaDeteccion) <= memoriaPersecucion);

        if (estado == Estado.Persiguiendo && !mantenerPersecucion)
        {
            estado = Estado.Regresando;
            siguienteDecision = 0f;
        }
        else if (detectadoAhora && estado != Estado.Persiguiendo)
        {
            estado = Estado.Persiguiendo;
            siguienteDecision = 0f;
            if (sonidoDeteccion != null && audioSourceLocal != null)
            {
                audioSourceLocal.PlayOneShot(sonidoDeteccion, 0.75f);
            }
        }

        // Interferencia progresiva de cámara corporal y temblor SOLO durante persecución
        float intensidadPersecucion = 0f;
        if (estado == Estado.Persiguiendo && jugadorDentro)
        {
            float rango = Mathf.Max(1f, radioAbandono - distanciaCaptura);
            float cercania = 1f - Mathf.Clamp01((distancia - distanciaCaptura) / rango);
            intensidadPersecucion = Mathf.Lerp(0.32f, 1f, cercania);
        }

        if (RetroCameraOverlay.Instance != null)
        {
            RetroCameraOverlay.Instance.EstablecerInterferenciaPersecucion(intensidadPersecucion);
        }
        if (camaraScript != null)
        {
            camaraScript.EstablecerTemblorPersecucion(intensidadPersecucion);
        }

        agente.speed = estado == Estado.Persiguiendo ? velocidadPersecucion : velocidadCaminar;
        if (Time.time >= siguienteDecision)
        {
            siguienteDecision = Time.time + .35f;
            if (estado == Estado.Persiguiendo)
            {
                if (!IrA(jugador.position)) { estado = Estado.Regresando; IrA(hogar); }
            }
            else if (estado == Estado.Regresando)
            {
                IrA(hogar);
                if (Vector3.Distance(transform.position, hogar) < 1f) estado = Estado.Patrullando;
            }
            else if (!agente.hasPath || agente.remainingDistance <= agente.stoppingDistance + .3f)
            {
                var limites = new Bounds(zona.transform.TransformPoint(zona.center), Vector3.Scale(zona.size, zona.transform.lossyScale));
                for (int i = 0; i < 24; i++)
                {
                    var destino = new Vector3(Random.Range(limites.min.x, limites.max.x), hogar.y, Random.Range(limites.min.z, limites.max.z));
                    if (Vector3.Distance(destino, transform.position) > 3f && IrA(destino)) break;
                }
            }
        }

        // Ajustar los pasos a la velocidad horizontal real, también al acelerar y frenar.
        if (animador != null)
        {
            Vector3 velocidad = agente.velocity;
            velocidad.y = 0f;
            animador.speed = velocidad.magnitude / Mathf.Max(0.01f, velocidadReferenciaAnimacion)
                * Mathf.Max(0.01f, ritmoAnimacion);
        }
    }

    private IEnumerator RutinaCapturaYSusto()
    {
        enSecuenciaCaptura = true;

        if (RetroCameraOverlay.Instance != null)
        {
            RetroCameraOverlay.Instance.EstablecerInterferenciaPersecucion(0f);
            RetroCameraOverlay.Instance.DispararGlitch(0.75f);
        }
        if (camaraScript != null)
        {
            camaraScript.EstablecerTemblorPersecucion(0f);
        }

        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = true;
            agente.ResetPath();
        }
        agente.enabled = false;

        // Bloquear controles del jugador mientras ocurre el susto
        if (playerMovement != null) playerMovement.enabled = false;
        if (camaraScript != null) camaraScript.enabled = false;
        if (jugadorRecolector != null)
        {
            jugadorRecolector.DevolverCajaSostenidaAOrigen();
            jugadorRecolector.enabled = false;
        }

        AsegurarOverlayOscuro();

        // 1. Oscurecer el entorno dejando al monstruo iluminado frente a la cámara
        yield return StartCoroutine(FundirOverlayOscuro(0f, 0.80f, 0.12f));

        Vector3 posCam = camaraPrincipal != null ? camaraPrincipal.transform.position : jugador.position + Vector3.up * 1.6f;
        Vector3 forwardPlano = camaraPrincipal != null ? camaraPrincipal.transform.forward : jugador.forward;
        forwardPlano.y = 0f;
        if (forwardPlano.sqrMagnitude < 0.01f) forwardPlano = Vector3.forward;
        forwardPlano.Normalize();

        Vector3 posInicioSusto = posCam + forwardPlano * 2.65f;
        posInicioSusto.y = jugador.position.y;
        Vector3 posFinSusto = posCam + forwardPlano * 0.90f;
        posFinSusto.y = jugador.position.y;

        transform.position = posInicioSusto;
        Vector3 dirAMirar = posCam - transform.position;
        dirAMirar.y = 0f;
        if (dirAMirar.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dirAMirar);

        if (luzSustoRostro != null)
        {
            luzSustoRostro.transform.position = posCam + forwardPlano * 0.6f + Vector3.up * 0.15f;
            luzSustoRostro.enabled = true;
        }

        if (sonidoJumpscare != null && audioSourceLocal != null)
        {
            audioSourceLocal.PlayOneShot(sonidoJumpscare, volumenSusto);
        }

        if (animador != null)
        {
            animador.speed = 3.2f;
        }

        // 2. El monstruo se acerca rápido a la cámara durante el momento oscuro
        Quaternion rotCamInicio = camaraPrincipal != null ? camaraPrincipal.transform.rotation : Quaternion.identity;
        float duracionAvance = 0.55f;
        float t = 0f;
        while (t < duracionAvance)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duracionAvance);
            float curva = p * p;

            transform.position = Vector3.Lerp(posInicioSusto, posFinSusto, curva);

            if (camaraPrincipal != null)
            {
                Vector3 rostroObjetivo = transform.position + Vector3.up * 1.78f;
                Quaternion rotObjetivo = Quaternion.LookRotation(rostroObjetivo - camaraPrincipal.transform.position);
                camaraPrincipal.transform.rotation = Quaternion.Slerp(rotCamInicio, rotObjetivo, Mathf.Clamp01(p * 3.5f));
            }
            yield return null;
        }

        // 3. Fundido total a negro
        yield return StartCoroutine(FundirOverlayOscuro(0.80f, 1f, 0.22f));
        if (luzSustoRostro != null) luzSustoRostro.enabled = false;
        if (animador != null) animador.speed = 0f;

        // 4. Devolver jugador a la tienda (Punto_JugadorFrenteGerente) y monstruo a su hogar
        if (puntoRetornoTienda != null && jugador != null)
        {
            var cc = jugador.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            jugador.position = puntoRetornoTienda.position;
            jugador.rotation = puntoRetornoTienda.rotation;
            if (cc != null) cc.enabled = true;
        }

        if (camaraPrincipal != null)
        {
            camaraPrincipal.transform.localRotation = Quaternion.identity;
        }

        if (playerMovement != null)
        {
            playerMovement.RestaurarEstaminaCompleta();
        }

        agente.enabled = true;
        agente.Warp(hogar);
        ultimaPosicionSegura = hogar;
        estado = Estado.Patrullando;
        tiempoUltimaDeteccion = -10f;

        yield return new WaitForSecondsRealtime(0.45f);

        // 5. Aclarar vista y devolver control en la tienda
        yield return StartCoroutine(FundirOverlayOscuro(1f, 0f, 0.55f));

        if (playerMovement != null) playerMovement.enabled = true;
        if (camaraScript != null) camaraScript.enabled = true;
        if (jugadorRecolector != null) jugadorRecolector.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (HUDTareasMundo1.Instance != null)
        {
            HUDTareasMundo1.Instance.MostrarSubtitulo("¡Escapaste por poco! Vuelve al almacén por el paquete que te toca.", 4.5f);
        }

        enSecuenciaCaptura = false;
    }

    private void AsegurarOverlayOscuro()
    {
        if (overlayOscuroCaptura == null)
        {
            var canvasObj = new GameObject("Canvas_SustoMonstruo", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 960;

            var panel = new GameObject("Panel_Oscuro", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasObj.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = panel.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            overlayOscuroCaptura = panel.GetComponent<CanvasGroup>();
            overlayOscuroCaptura.alpha = 0f;
            overlayOscuroCaptura.blocksRaycasts = false;
            overlayOscuroCaptura.interactable = false;
        }

        if (luzSustoRostro == null)
        {
            var goLuz = new GameObject("Luz_SustoRostro", typeof(Light));
            luzSustoRostro = goLuz.GetComponent<Light>();
            luzSustoRostro.type = LightType.Point;
            luzSustoRostro.color = new Color(1f, 0.35f, 0.30f);
            luzSustoRostro.range = 6f;
            luzSustoRostro.intensity = 5.5f;
            luzSustoRostro.enabled = false;
        }
    }

    private IEnumerator FundirOverlayOscuro(float desde, float hasta, float duracion)
    {
        if (overlayOscuroCaptura == null) yield break;
        float t = 0f;
        overlayOscuroCaptura.alpha = desde;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            overlayOscuroCaptura.alpha = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t / duracion));
            yield return null;
        }
        overlayOscuroCaptura.alpha = hasta;
    }

    private void OnDestroy()
    {
        if (instancia.valid) instancia.Remove();
        if (overlayOscuroCaptura != null) Destroy(overlayOscuroCaptura.transform.root.gameObject);
        if (luzSustoRostro != null) Destroy(luzSustoRostro.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, radioDeteccion);
        Gizmos.color = new Color(1f, 0.4f, 0f); Gizmos.DrawWireSphere(transform.position, radioEscuchaSprint);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, distanciaCaptura);
        if (zona != null) { Gizmos.color = Color.cyan; Gizmos.matrix = zona.transform.localToWorldMatrix; Gizmos.DrawWireCube(zona.center, zona.size); }
    }
}
