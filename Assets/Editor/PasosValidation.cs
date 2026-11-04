using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class PasosValidation
{
    private const string Key = "PasosValidation.Stage";
    private static PlayerMovement jugador;
    private static Camara camara;
    private static PasosJugador audio;
    private static Keyboard teclado;
    private static double siguiente, limite;
    private static int cuenta, eventos, frame;
    private static bool enAire;
    private static string errorApoyo;

    static PasosValidation() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Audio/Validar pasos (escena aislada)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetString(Key + ".Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        suelo.transform.position = new Vector3(0, -0.5f, 0);
        suelo.transform.localScale = new Vector3(200, 1, 200);
        var player = new GameObject("Jugador de prueba", typeof(CharacterController), typeof(PlayerMovement));
        player.transform.position = Vector3.up;
        var vista = new GameObject("Vista de prueba", typeof(Camera), typeof(AudioListener), typeof(Camara), typeof(PasosJugador));
        vista.transform.SetParent(player.transform, false);
        vista.GetComponent<Camara>().Player = player.transform;
        var pasos = vista.GetComponent<PasosJugador>();
        pasos.pasos = new AudioClip[10];
        for (int i = 0; i < 10; i++)
        {
            pasos.pasos[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/Audio/Jugador/Pasos/KenneyRPG/footstep{i:00}.ogg");
            Check(pasos.pasos[i] != null && pasos.pasos[i].length > 0, "Clip importado " + i);
        }
        System.IO.Directory.CreateDirectory("Assets/AudioValidationTemp");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/AudioValidationTemp/Test.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool ok, string mensaje)
    {
        if (!ok) throw new Exception("PASOS: " + mensaje);
        Debug.Log("PASOS_CHECK_OK: " + mensaje);
    }
    private static void Entrada(params UnityEngine.InputSystem.Key[] keys) =>
        InputSystem.QueueStateEvent(teclado, new KeyboardState(keys));
    private static void Next(int stage, double seconds)
    {
        siguiente = (stage == 10 ? EditorApplication.timeSinceStartup : Time.time) + seconds;
        SessionState.SetInt(Key, stage);
    }
    private static void Apoyo()
    {
        eventos++;
        if (audio.PasosReproducidos != eventos || audio.UltimoPaso != audio.pasos[(eventos - 1) % 10])
            errorApoyo = "Audio fuera de secuencia o desincronizado del apoyo";
    }

    private static void Tick()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0) return;
        try
        {
            if (stage == 99 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                string anterior = SessionState.GetString(Key + ".Scene", "");
                if (!string.IsNullOrEmpty(anterior)) EditorSceneManager.OpenScene(anterior);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset("Assets/AudioValidationTemp");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            if (!EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (errorApoyo != null) throw new Exception(errorApoyo);
            if (stage > 1 && EditorApplication.timeSinceStartup > limite) throw new Exception("Timeout de pasos");
            if (stage == 1)
            {
                jugador = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                audio = UnityEngine.Object.FindAnyObjectByType<PasosJugador>();
                camara = audio.GetComponent<Camara>();
                Time.captureDeltaTime = 1f / 60f;
                jugador.GetComponent<CharacterController>().minMoveDistance = 0f;
                teclado = InputSystem.AddDevice<Keyboard>();
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                eventos = 0;
                camara.PasoRealizado += Apoyo;
                limite = EditorApplication.timeSinceStartup + 120;
                Entrada();
                Next(2, 0.5);
                return;
            }
            if (stage == 7 && frame != Time.frameCount)
            {
                frame = Time.frameCount;
                if (!jugador.EnSuelo)
                {
                    if (enAire) Check(audio.PasosReproducidos == cuenta, "Sin pasos durante salto");
                    cuenta = audio.PasosReproducidos;
                    enAire = true;
                }
            }
            if ((stage == 10 ? EditorApplication.timeSinceStartup : Time.time) < siguiente) return;
            switch (stage)
            {
                case 2:
                    Check(audio.PasosReproducidos == 0, "Silencio en reposo");
                    Entrada(UnityEngine.InputSystem.Key.W);
                    Next(3, 7);
                    break;
                case 3:
                    Check(eventos >= 11, $"Diez clips y retorno a footstep00: {eventos} apoyos, {jugador.DistanciaCaminada:F2} m");
                    Entrada();
                    Next(4, 0.8);
                    break;
                case 4:
                    cuenta = audio.PasosReproducidos;
                    Next(5, 0.7);
                    break;
                case 5:
                    Check(audio.PasosReproducidos == cuenta, "Sin pasos después de detenerse");
                    Entrada(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift);
                    Next(6, 2);
                    break;
                case 6:
                    Check(audio.PasosReproducidos - cuenta >= 4, "La carrera acelera los apoyos");
                    Entrada(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.Space);
                    enAire = false;
                    Next(7, 0.1);
                    break;
                case 7:
                    if (!jugador.EnSuelo) { siguiente = Time.time + 0.05; break; }
                    Check(enAire, "Salto comprobado");
                    Entrada(UnityEngine.InputSystem.Key.W);
                    var pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pared.transform.position = jugador.transform.position + Vector3.forward * 0.8f;
                    pared.transform.localScale = new Vector3(10, 10, 0.2f);
                    Physics.SyncTransforms();
                    Next(8, 1);
                    break;
                case 8:
                    cuenta = audio.PasosReproducidos;
                    Next(9, 0.8);
                    break;
                case 9:
                    Check(audio.PasosReproducidos == cuenta, "Sin pasos al avanzar contra una pared");
                    Entrada(UnityEngine.InputSystem.Key.S);
                    Time.timeScale = 0f;
                    Next(10, 0.5);
                    break;
                case 10:
                    Check(audio.PasosReproducidos == cuenta, "Sin pasos durante pausa");
                    Time.timeScale = 1f;
                    camara.BalanceoActivo = false;
                    Next(11, 2);
                    break;
                case 11:
                    Check(audio.PasosReproducidos > cuenta, "Audio funciona con balanceo visual apagado");
                    Check(audio.GetComponent<AudioSource>() != null && !audio.GetComponent<AudioSource>().loop,
                        "Fuente de audio creada; repetición gobernada por los apoyos");
                    Entrada();
                    camara.PasoRealizado -= Apoyo;
                    InputSystem.RemoveDevice(teclado);
                    Time.captureDeltaTime = 0f;
                    Debug.Log("PASOS_VALIDATION_OK");
                    SessionState.SetInt(Key, 99);
                    EditorApplication.ExitPlaymode();
                    break;
            }
        }
        catch (Exception error)
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            SessionState.SetInt(Key, 0);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
