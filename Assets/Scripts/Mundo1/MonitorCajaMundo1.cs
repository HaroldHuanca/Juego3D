using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Tienda.SistemaVentas;

namespace Mundo1
{
    /// <summary>
    /// Escucha los intentos de cobro en la caja registradora.
    /// Si el jugador ingresa un vuelto incorrecto, reproduce un sonido aleatorio de terror/monstruo detrás del jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonitorCajaMundo1 : MonoBehaviour
    {
        [Header("Sonidos Aleatorios del Monstruo al Fallar el Vuelto")]
        [Tooltip("Clips que pueden sonar detrás de ti al equivocarte en la caja (si está vacío carga respiración detrás por defecto)")]
        [SerializeField] private AudioClip[] sonidosFalloCaja;
        [Range(0f, 1f)]
        [SerializeField] private float volumenSusto = 0.85f;

        private Button botonConfirmarCobro;
        private int ventasPrevias = 0;
        private AudioSource audioSourceSusto;

        private static readonly WaitForSeconds EsperaBusqueda = new WaitForSeconds(0.5f);
        private static readonly WaitForEndOfFrame EsperaFinFrame = new WaitForEndOfFrame();

        private void Awake()
        {
            AutoCargarSonidosSiFaltan();
        }

        private void Start()
        {
            if (TiendaManager.Instance != null)
            {
                ventasPrevias = TiendaManager.Instance.VentasExitosas;
            }

            StartCoroutine(RutinaVincularBotonCaja());
        }

        private void AutoCargarSonidosSiFaltan()
        {
#if UNITY_EDITOR
            if (sonidosFalloCaja == null || sonidosFalloCaja.Length == 0 || System.Array.TrueForAll(sonidosFalloCaja, c => c == null))
            {
                var resp = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Terror/sonidoRespiracionCerca.mp3");
                var glitch = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Camara/ErrorGlitch.mp3");
                var lista = new List<AudioClip>();
                if (resp != null) lista.Add(resp);
                if (glitch != null) lista.Add(glitch);
                sonidosFalloCaja = lista.ToArray();
            }
#endif
        }

        private IEnumerator RutinaVincularBotonCaja()
        {
            while (botonConfirmarCobro == null)
            {
                var ui = CajaRegistradoraUI.Instance;
                if (ui == null) ui = FindAnyObjectByType<CajaRegistradoraUI>(FindObjectsInactive.Include);
                if (ui != null)
                {
                    if (ui.BotonConfirmarCobro != null)
                    {
                        botonConfirmarCobro = ui.BotonConfirmarCobro;
                        botonConfirmarCobro.onClick.AddListener(AlHacerClicEnConfirmar);
                        yield break;
                    }

                    var botones = ui.GetComponentsInChildren<Button>(true);
                    foreach (var b in botones)
                    {
                        if (b.gameObject.name == "Btn_Confirmar")
                        {
                            botonConfirmarCobro = b;
                            botonConfirmarCobro.onClick.AddListener(AlHacerClicEnConfirmar);
                            yield break;
                        }
                    }
                }
                yield return EsperaBusqueda;
            }
        }

        private void AlHacerClicEnConfirmar()
        {
            StartCoroutine(RutinaVerificarResultadoCobro());
        }

        private IEnumerator RutinaVerificarResultadoCobro()
        {
            yield return EsperaFinFrame;

            if (TiendaManager.Instance == null) yield break;

            if (TiendaManager.Instance.VentasExitosas > ventasPrevias)
            {
                ventasPrevias = TiendaManager.Instance.VentasExitosas;
            }
            else if (TiendaManager.Instance.HayPedidoPendiente)
            {
                ReproducirSonidoAleatorioDetras();
                if (RetroCameraOverlay.Instance != null)
                {
                    RetroCameraOverlay.Instance.DispararGlitch(0.25f);
                }
            }
        }

        private void ReproducirSonidoAleatorioDetras()
        {
            if (sonidosFalloCaja == null || sonidosFalloCaja.Length == 0) return;

            var validos = new List<AudioClip>();
            for (int i = 0; i < sonidosFalloCaja.Length; i++)
            {
                if (sonidosFalloCaja[i] != null) validos.Add(sonidosFalloCaja[i]);
            }
            if (validos.Count == 0) return;

            AudioClip clipElegido = validos[Random.Range(0, validos.Count)];

            if (audioSourceSusto == null)
            {
                var go = new GameObject("Audio_SustoCajaDetras");
                go.transform.SetParent(transform, false);
                audioSourceSusto = go.AddComponent<AudioSource>();
                audioSourceSusto.playOnAwake = false;
                audioSourceSusto.spatialBlend = 0.75f;
                audioSourceSusto.minDistance = 0.5f;
                audioSourceSusto.maxDistance = 12f;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                audioSourceSusto.transform.position = cam.transform.position - cam.transform.forward * 1.4f;
            }

            audioSourceSusto.PlayOneShot(clipElegido, volumenSusto);
        }
    }
}
