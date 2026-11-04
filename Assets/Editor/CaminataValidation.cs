using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class CaminataValidation
{
    private const string Key = "CaminataValidation.Stage";
    private static PlayerMovement player;
    private static Camara camara;
    private static Keyboard teclado;
    private static Vector3 reposo;
    private static float distanciaBloqueada;
    private static double siguiente;
    private static double limite;
    private static GameObject pared;
    private static Rigidbody puerta;
    private static float maximoBalanceo;
    private static float maximoGiro;
    private static float faseAnterior;
    private static float mayorCambioFase;
    private static float mayorCambioPosicion;
    private static Vector3 posicionAnterior;
    private static int frameAnterior;

    static CaminataValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }

    public static void RunAislado()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var jugador = new GameObject("Jugador de prueba", typeof(CharacterController), typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerMovement));
        var vista = new GameObject("Vista de prueba", typeof(Camera), typeof(Camara));
        vista.tag = "MainCamera";
        vista.transform.SetParent(jugador.transform, false);
        vista.transform.localPosition = Vector3.up * 0.8f;
        vista.GetComponent<Camara>().Player = jugador.transform;
        var brazos = new GameObject("Brazos de prueba", typeof(BrazosPrimeraPersona));
        brazos.transform.SetParent(vista.transform, false);
        brazos.GetComponent<BrazosPrimeraPersona>().puntoVista = vista.transform;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/CaminataTest.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
        Debug.Log("CAMINATA_CHECK_OK: " + mensaje);
    }

    private static void Siguiente(int stage, double segundos)
    {
        siguiente = Time.time + segundos;
        SessionState.SetInt(Key, stage);
    }

    private static void Entrada(params Key[] keys)
    {
        InputSystem.QueueStateEvent(teclado, new KeyboardState(keys));
    }

    private static void Tick()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0) return;
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if ((stage == 4 || stage >= 20 && stage <= 24) && camara != null)
        {
            maximoBalanceo = Mathf.Max(maximoBalanceo, Vector3.Distance(camara.transform.localPosition, reposo));
            maximoGiro = Mathf.Max(maximoGiro, Quaternion.Angle(Quaternion.identity, camara.transform.localRotation));
            if (stage >= 20 && Time.frameCount != frameAnterior)
            {
                mayorCambioFase = Mathf.Max(mayorCambioFase,
                    Mathf.Abs(Mathf.DeltaAngle(faseAnterior * Mathf.Rad2Deg, camara.FasePasos * Mathf.Rad2Deg)) * Mathf.Deg2Rad);
                mayorCambioPosicion = Mathf.Max(mayorCambioPosicion,
                    Vector3.Distance(posicionAnterior, camara.transform.localPosition));
                faseAnterior = camara.FasePasos;
                posicionAnterior = camara.transform.localPosition;
                frameAnterior = Time.frameCount;
            }
        }
        if (stage > 1 && stage != 11 && Time.time < siguiente) return;
        try
        {
            if (stage == 1 && EditorApplication.isPlaying)
            {
                camara = UnityEngine.Object.FindAnyObjectByType<Camara>();
                if (camara == null) return;
                player = camara.Player.GetComponent<PlayerMovement>();
                Time.captureDeltaTime = 1f / 60f;
                player.GetComponent<CharacterController>().minMoveDistance = 0f;
                limite = EditorApplication.timeSinceStartup + 40;
                Check(Mathf.Approximately(player.Velocidad, 2.2f), "Escena usa velocidad de terror 2.2 m/s");
                teclado = InputSystem.AddDevice<Keyboard>();
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Check(player.GetComponent<Rigidbody>().isKinematic && !player.GetComponent<Rigidbody>().useGravity &&
                    !player.GetComponent<CapsuleCollider>().enabled, "Controlador evita conflicto con la física de la cápsula");
                Physics.autoSyncTransforms = true;
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = new Vector3(0, 101, 0);
                player.GetComponent<Rigidbody>().position = player.transform.position;
                player.transform.rotation = Quaternion.identity;
                player.GetComponent<CharacterController>().enabled = true;
                var suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                suelo.transform.position = new Vector3(0, 99.5f, 0);
                suelo.transform.localScale = new Vector3(50, 1, 50);
                Physics.SyncTransforms();
                reposo = camara.transform.localPosition;
                Entrada();
                Siguiente(2, 1);
            }
            else if (stage == 2 && EditorApplication.isPlaying)
            {
                Check(player.EnSuelo, "Jugador apoyado sobre suelo de prueba");
                Entrada(UnityEngine.InputSystem.Key.W);
                Siguiente(3, 0.15);
            }
            else if (stage == 3 && EditorApplication.isPlaying)
            {
                Check(player.VelocidadHorizontalActual > 0.1f && player.VelocidadHorizontalActual < 1.8f,
                    "Acelera progresivamente al comenzar");
                Siguiente(4, 0.8);
            }
            else if (stage == 4 && EditorApplication.isPlaying)
            {
                Check(Mathf.Abs(player.VelocidadHorizontalActual - 2.2f) < 0.12f, "Alcanza velocidad configurada");
                Check(player.DistanciaCaminada > 0.5f, "Pasos avanzan con desplazamiento real");
                Vector3 offset = camara.transform.localPosition - reposo;
                Check(Mathf.Abs(offset.x) <= camara.BalanceoHorizontal + 0.001f &&
                    Mathf.Abs(offset.y) <= camara.BalanceoVertical + 0.001f, "Balanceo dentro de amplitud configurada");
                Check(maximoBalanceo > 0.002f && maximoBalanceo < 0.017f && maximoGiro < 0.13f,
                    "Balanceo de caminata suave, con inclinación mínima");
                var brazos = UnityEngine.Object.FindAnyObjectByType<BrazosPrimeraPersona>();
                Check(Vector3.Distance(brazos.transform.position, camara.transform.position) < 0.001f,
                    "Brazos siguen la cámara después del balanceo");
                maximoBalanceo = 0f;
                faseAnterior = camara.FasePasos;
                posicionAnterior = camara.transform.localPosition;
                frameAnterior = Time.frameCount;
                Entrada(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift);
                Siguiente(20, 0.7);
            }
            else if (stage == 20 && EditorApplication.isPlaying)
            {
                Check(player.Esprintando && Mathf.Abs(player.VelocidadHorizontalActual - 4.2f) < 0.12f,
                    "Shift izquierdo alcanza velocidad de esprint");
                Check(camara.MezclaEsprint > 0.8f, "Balanceo acompaña velocidad de carrera");
                Check(maximoBalanceo < 0.024f && maximoGiro < 0.21f, "Balanceo de carrera permanece moderado");
                Entrada(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.RightShift);
                Siguiente(21, 0.3);
            }
            else if (stage == 21 && EditorApplication.isPlaying)
            {
                Check(player.Esprintando && Mathf.Abs(player.VelocidadHorizontalActual - 4.2f) < 0.12f,
                    "Shift derecho también permite esprintar");
                Entrada(UnityEngine.InputSystem.Key.W);
                Siguiente(22, 0.15);
            }
            else if (stage == 22 && EditorApplication.isPlaying)
            {
                Check(!player.Esprintando && player.VelocidadHorizontalActual > 2.2f && player.VelocidadHorizontalActual < 4.1f,
                    "Soltar Shift frena gradualmente hasta caminar");
                Siguiente(23, 0.7);
            }
            else if (stage == 23 && EditorApplication.isPlaying)
            {
                Check(Mathf.Abs(player.VelocidadHorizontalActual - 2.2f) < 0.12f && camara.MezclaEsprint < 0.1f,
                    "Recupera velocidad y balanceo de caminata");
                Check(mayorCambioFase < 0.25f && mayorCambioPosicion < 0.006f,
                    "Transiciones de caminar y correr sin saltos de fase ni sacudidas");
                Entrada(UnityEngine.InputSystem.Key.LeftShift);
                Siguiente(24, 0.8);
            }
            else if (stage == 24 && EditorApplication.isPlaying)
            {
                Check(!player.Esprintando && player.VelocidadHorizontalActual < 0.02f,
                    "Shift solo no mueve al jugador");
                Entrada();
                Siguiente(5, 0.5);
            }
            else if (stage == 5 && EditorApplication.isPlaying)
            {
                Check(player.VelocidadHorizontalActual < 0.02f, "Se detiene al soltar la tecla");
                Check(Vector3.Distance(camara.transform.localPosition, reposo) < 0.001f, "Cámara vuelve a reposo sin deriva");
                Check(Quaternion.Angle(Quaternion.identity, camara.transform.localRotation) < 0.01f, "Inclinación vuelve a reposo");
                Entrada(UnityEngine.InputSystem.Key.S, UnityEngine.InputSystem.Key.LeftShift);
                Siguiente(6, 0.7);
            }
            else if (stage == 6 && EditorApplication.isPlaying)
            {
                Check(Mathf.Abs(player.VelocidadHorizontalActual - 1.54f) < 0.12f, "Retroceso más lento");
                Check(!player.Esprintando, "Retroceder con Shift conserva caminata");
                Entrada();
                Siguiente(7, 0.5);
            }
            else if (stage == 7 && EditorApplication.isPlaying)
            {
                pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pared.transform.position = player.transform.position + Vector3.forward * 0.55f;
                pared.transform.localScale = new Vector3(5, 5, 0.2f);
                Entrada(UnityEngine.InputSystem.Key.W);
                Siguiente(8, 1);
            }
            else if (stage == 8 && EditorApplication.isPlaying)
            {
                Check(player.VelocidadHorizontalActual < 0.02f, "Pared bloquea movimiento real");
                distanciaBloqueada = player.DistanciaCaminada;
                Siguiente(9, 0.5);
            }
            else if (stage == 9 && EditorApplication.isPlaying)
            {
                Check(Mathf.Abs(player.DistanciaCaminada - distanciaBloqueada) < 0.002f, "No genera pasos contra una pared");
                Check(Vector3.Distance(camara.transform.localPosition, reposo) < 0.001f, "Balanceo cesa al estar bloqueado");
                pared.SetActive(false);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = player.transform.position + Vector3.forward * 0.8f;
                go.transform.localScale = new Vector3(1f, 2f, 0.1f);
                puerta = go.AddComponent<Rigidbody>();
                puerta.useGravity = false;
                var hinge = go.AddComponent<HingeJoint>();
                hinge.axis = Vector3.up;
                hinge.anchor = new Vector3(-0.5f, 0, 0);
                Physics.SyncTransforms();
                Siguiente(10, 0.35);
            }
            else if (stage == 10 && EditorApplication.isPlaying)
            {
                Check(puerta.angularVelocity.sqrMagnitude > 0.0001f, "Caminata lenta conserva el empuje físico de puertas");
                Entrada();
                InputSystem.RemoveDevice(teclado);
                Time.captureDeltaTime = 0f;
                Debug.Log("CAMINATA_VALIDATION_OK");
                SessionState.SetInt(Key, 11);
                EditorApplication.ExitPlaymode();
            }
            else if (stage == 11 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            if (limite > 0 && EditorApplication.timeSinceStartup > limite) throw new Exception("Timeout de caminata");
        }
        catch (Exception error)
        {
            SessionState.SetInt(Key, 0);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
