using System;
using System.Collections;
using UnityEngine;

namespace Mundo1
{
    /// <summary>
    /// Controla la atmósfera de tormenta: lluvia ambiental, destellos de relámpago,
    /// sonidos de truenos e intercambio fugaz de modelos (skin del gerente/cliente por el monstruo).
    /// </summary>
    [DisallowMultipleComponent]
    public class ControladorTormenta : MonoBehaviour
    {
        public static ControladorTormenta Instance { get; private set; }

        [Header("Luz del Relámpago")]
        [SerializeField] private Light luzRelampago;
        [SerializeField] private float intensidadRelampago = 3.5f;
        [SerializeField] private Color colorRelampago = new Color(0.92f, 0.95f, 1f);

        [Header("Sonido")]
        [SerializeField] private AudioSource audioSourceAmbiente;
        [SerializeField] private AudioSource audioSourceTruenos;
        [SerializeField] private AudioClip clipLluvia;
        [SerializeField] private AudioClip[] clipsTruenos;

        [Header("Partículas (Opcional)")]
        [SerializeField] private ParticleSystem particulasLluvia;

        private float intensidadLuzOriginal = 0f;
        private Coroutine rutinaRelampagoActiva;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            AsegurarAudioListener();
            AsegurarLucesYAudio();
            AutoCargarAudiosSiFaltan();
        }

        private void Start()
        {
            AsegurarAudioListener();
            IniciarLluvia(0.5f);
        }

        private void AsegurarAudioListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            foreach (var l in listeners)
            {
                if (!l.enabled) l.enabled = true;
            }

            if (listeners == null || listeners.Length == 0)
            {
                if (Camera.main != null && Camera.main.GetComponent<AudioListener>() == null)
                {
                    Camera.main.gameObject.AddComponent<AudioListener>();
                }
            }
        }

        private void AutoCargarAudiosSiFaltan()
        {
#if UNITY_EDITOR
            if (clipLluvia == null)
            {
                clipLluvia = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambiente/clipLluvia.mp3");
            }

            if (clipsTruenos == null || clipsTruenos.Length == 0 || System.Array.TrueForAll(clipsTruenos, c => c == null))
            {
                var t1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambiente/trueno.mp3");
                var t2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambiente/trueno2.mp3");
                clipsTruenos = new AudioClip[] { t1, t2 };
            }
#endif
        }

        private void AsegurarLucesYAudio()
        {
            // Si no se asignó luz, creamos o buscamos una direccional secundaria para los rayos
            if (luzRelampago == null)
            {
                var goLuz = new GameObject("Luz_Relampago", typeof(Light));
                goLuz.transform.SetParent(transform, false);
                luzRelampago = goLuz.GetComponent<Light>();
                luzRelampago.type = LightType.Directional;
                luzRelampago.color = colorRelampago;
                luzRelampago.intensity = 0f;
                luzRelampago.enabled = false;
            }
            else
            {
                intensidadLuzOriginal = luzRelampago.intensity;
                luzRelampago.enabled = false;
            }

            if (audioSourceAmbiente == null)
            {
                audioSourceAmbiente = gameObject.AddComponent<AudioSource>();
            }
            audioSourceAmbiente.loop = true;
            audioSourceAmbiente.playOnAwake = false;
            audioSourceAmbiente.spatialBlend = 0f; // 2D universal
            audioSourceAmbiente.volume = 0.5f;

            if (audioSourceTruenos == null)
            {
                audioSourceTruenos = gameObject.AddComponent<AudioSource>();
            }
            audioSourceTruenos.loop = false;
            audioSourceTruenos.playOnAwake = false;
            audioSourceTruenos.spatialBlend = 0f; // 2D universal
            audioSourceTruenos.volume = 1f;
        }

        /// <summary>
        /// Comienza la reproducción de la lluvia ambiental con volumen suave.
        /// </summary>
        public void IniciarLluvia(float volumen = 0.5f)
        {
            if (audioSourceAmbiente != null && clipLluvia != null)
            {
                audioSourceAmbiente.clip = clipLluvia;
                audioSourceAmbiente.volume = volumen;
                if (!audioSourceAmbiente.isPlaying) audioSourceAmbiente.Play();
            }

            if (particulasLluvia != null && !particulasLluvia.isPlaying)
            {
                particulasLluvia.Play();
            }
        }

        /// <summary>
        /// Aumenta la intensidad de la tormenta (para la recta final del nivel).
        /// </summary>
        public void IntensificarTormenta(float volumen = 0.95f, float duracion = 3f)
        {
            if (audioSourceAmbiente != null)
            {
                StartCoroutine(RutinaFadeVolumen(audioSourceAmbiente, volumen, Mathf.Max(0.1f, duracion)));
            }
            if (particulasLluvia != null && !particulasLluvia.isPlaying)
            {
                particulasLluvia.Play();
            }
        }

        /// <summary>
        /// Dispara un relámpago con doble destello realista, reproduce el trueno y,
        /// opcionalmente, intercambia durante 0.18s la skin normal por la del monstruo.
        /// </summary>
        public void DispararRelampago(GameObject skinNormal = null, GameObject skinMonstruo = null, float duracion = 0.18f, Action onFinalizado = null)
        {
            if (rutinaRelampagoActiva != null) StopCoroutine(rutinaRelampagoActiva);
            rutinaRelampagoActiva = StartCoroutine(RutinaRelampago(skinNormal, skinMonstruo, duracion, onFinalizado));
        }

        private IEnumerator RutinaRelampago(GameObject skinNormal, GameObject skinMonstruo, float duracion, Action onFinalizado)
        {
            // 1. Sonido de trueno (filtrar clips válidos para garantizar 100% que SIEMPRE suene)
            if (audioSourceTruenos != null)
            {
                audioSourceTruenos.spatialBlend = 0f; // Asegurar 2D
                AudioClip clipElegido = null;

                if (clipsTruenos != null && clipsTruenos.Length > 0)
                {
                    var clipsValidos = new System.Collections.Generic.List<AudioClip>();
                    foreach (var c in clipsTruenos)
                    {
                        if (c != null) clipsValidos.Add(c);
                    }
                    if (clipsValidos.Count > 0)
                    {
                        clipElegido = clipsValidos[UnityEngine.Random.Range(0, clipsValidos.Count)];
                    }
                }

#if UNITY_EDITOR
                if (clipElegido == null)
                {
                    clipElegido = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambiente/trueno.mp3");
                }
#endif

                if (clipElegido != null)
                {
                    audioSourceTruenos.PlayOneShot(clipElegido, 1f);
                }
            }

            // 2. Intercambio fugaz de skins si están asignadas
            if (skinNormal != null && skinMonstruo != null)
            {
                skinMonstruo.transform.position = skinNormal.transform.position;
                skinMonstruo.transform.rotation = skinNormal.transform.rotation;
            }

            // Ocultar renderers del modelo normal sin desactivar su GameObject raíz
            // (evita cancelar corrutinas activas en dicho GameObject como la cinemática)
            Renderer[] renderersNormal = null;
            if (skinNormal != null)
            {
                renderersNormal = skinNormal.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderersNormal) r.enabled = false;
            }

            if (skinMonstruo != null) skinMonstruo.SetActive(true);

            // 3. Primer destello fuerte (Relámpago principal)
            if (luzRelampago != null)
            {
                luzRelampago.enabled = true;
                luzRelampago.intensity = intensidadRelampago;
            }

            yield return new WaitForSeconds(duracion * 0.55f);

            // 4. Parpadeo intermedio (efecto de rayo que titila)
            if (luzRelampago != null) luzRelampago.intensity = intensidadRelampago * 0.25f;
            yield return new WaitForSeconds(0.04f);

            // Segundo destello breve
            if (luzRelampago != null) luzRelampago.intensity = intensidadRelampago * 0.85f;
            yield return new WaitForSeconds(duracion * 0.45f);

            // 5. Apagar relámpago y restaurar skin original
            if (luzRelampago != null)
            {
                luzRelampago.intensity = intensidadLuzOriginal;
                luzRelampago.enabled = false;
            }

            if (skinMonstruo != null) skinMonstruo.SetActive(false);

            if (renderersNormal != null)
            {
                foreach (var r in renderersNormal) r.enabled = true;
            }
            else if (skinNormal != null)
            {
                skinNormal.SetActive(true);
            }

            rutinaRelampagoActiva = null;
            onFinalizado?.Invoke();
        }

        private IEnumerator RutinaFadeVolumen(AudioSource source, float objetivo, float tiempo)
        {
            float inicio = source.volume;
            float transcurrido = 0f;
            while (transcurrido < tiempo)
            {
                transcurrido += Time.deltaTime;
                source.volume = Mathf.Lerp(inicio, objetivo, transcurrido / tiempo);
                yield return null;
            }
            source.volume = objetivo;
        }
    }
}
