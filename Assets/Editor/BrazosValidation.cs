using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Pruebas de integración reales en Play Mode; no guarda los objetos de prueba.</summary>
[InitializeOnLoad]
public static class BrazosValidation
{
    private const string Key = "BrazosValidation.Stage";
    private static double siguiente;
    private static double limite;
    private static InteraccionJugador interaction;
    private static ObjetoRecogible objeto;
    private static int eventos;
    private static GameObject pared;
    private static Rigidbody puerta;

    static BrazosValidation() { EditorApplication.update += Tick; }

    public static void SetupAndRun()
    {
        BrazosSetup.Run();
        Run();
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool ok, string mensaje)
    {
        if (!ok) throw new Exception(mensaje);
        Debug.Log("BRAZOS_CHECK_OK: " + mensaje);
    }

    private static void Tick()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0 || EditorApplication.timeSinceStartup < siguiente) return;
        try
        {
            if (stage == 1 && EditorApplication.isPlaying)
            {
                interaction = UnityEngine.Object.FindAnyObjectByType<InteraccionJugador>();
                if (interaction == null) return;
                limite = EditorApplication.timeSinceStartup + 50;
                Check(interaction.brazos != null && interaction.brazos.animador != null, "Rig conectado");
                var cam = interaction.GetComponent<Camera>();
                var stack = cam.GetUniversalAdditionalCameraData().cameraStack;
                Check(stack.Count == 1 && stack[0] != null, "Una cámara Overlay en URP");
                Check((cam.cullingMask & stack[0].cullingMask) == 0, "Capas de brazos separadas del escenario");
                Check(!interaction.brazos.animador.applyRootMotion, "Animación sin mover al jugador");
                foreach (var r in interaction.brazos.GetComponentsInChildren<Renderer>())
                    Debug.Log("BRAZOS_BOUNDS: " + r.bounds + " camera=" + cam.transform.position);
                interaction.jugador.GetComponent<PlayerMovement>().enabled = false;
                interaction.GetComponent<Camara>().enabled = false;
                var body = interaction.jugador.GetComponent<Rigidbody>();
                if (body != null) body.isKinematic = true;
                siguiente = EditorApplication.timeSinceStartup + 1;
                SessionState.SetInt(Key, 2);
            }
            else if (stage == 2 && EditorApplication.isPlaying)
            {
                Capturar("Reposo");
                interaction.transform.position = new Vector3(0, 100, 0);
                interaction.transform.rotation = Quaternion.identity;
                objeto = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<ObjetoRecogible>();
                objeto.transform.position = interaction.transform.position + Vector3.forward;
                objeto.transform.localScale = Vector3.one * 0.2f;
                objeto.alRecoger.AddListener(() => eventos++);
                pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pared.transform.position = interaction.transform.position + Vector3.forward * 0.5f;
                pared.transform.localScale = Vector3.one * 0.2f;
                Physics.SyncTransforms();
                Check(!interaction.IntentarInteractuar(), "Pared bloquea recogida");
                pared.SetActive(false);
                objeto.transform.position = interaction.transform.position + Vector3.forward * 3f;
                Physics.SyncTransforms();
                Check(!interaction.IntentarInteractuar(), "Objeto lejano rechazado");
                objeto.transform.position = interaction.transform.position + Vector3.forward;
                Physics.SyncTransforms();
                Check(interaction.IntentarInteractuar(), "Recogida inicia clip");
                Check(!interaction.IntentarInteractuar(), "Acciones simultáneas bloqueadas");
                siguiente = EditorApplication.timeSinceStartup + interaction.brazos.recoger.length + 0.2f;
                SessionState.SetInt(Key, 3);
            }
            else if (stage == 3 && EditorApplication.isPlaying)
            {
                Check(objeto.Recogido && !objeto.gameObject.activeSelf && eventos == 1, "Recoge una vez al contacto");
                objeto.Recoger();
                Check(eventos == 1, "Evento de recogida no se duplica");
                objeto = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<ObjetoRecogible>();
                objeto.transform.position = interaction.transform.position + Vector3.forward;
                objeto.transform.localScale = Vector3.one * 0.2f;
                Physics.SyncTransforms();
                Check(interaction.IntentarInteractuar(), "Segunda recogida disponible tras animación");
                objeto.transform.position += Vector3.right * 4f;
                siguiente = EditorApplication.timeSinceStartup + interaction.brazos.recoger.length + 0.2f;
                SessionState.SetInt(Key, 4);
            }
            else if (stage == 4 && EditorApplication.isPlaying)
            {
                Check(!objeto.Recogido, "Cancela contacto si objeto sale del alcance");
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = interaction.transform.position + Vector3.forward;
                go.transform.localScale = new Vector3(1f, 2f, 0.1f);
                puerta = go.AddComponent<Rigidbody>(); puerta.useGravity = false;
                var hinge = go.AddComponent<HingeJoint>();
                hinge.axis = Vector3.up; hinge.anchor = new Vector3(-0.5f, 0, 0);
                Physics.SyncTransforms();
                Check(interaction.IntentarInteractuar(), "E empuja puerta y reproduce clip");
                Check(interaction.brazos.Ocupado, "Empuje ocupa los brazos");
                siguiente = EditorApplication.timeSinceStartup + 0.15;
                SessionState.SetInt(Key, 5);
            }
            else if (stage == 5 && EditorApplication.isPlaying)
            {
                Check(puerta.angularVelocity.sqrMagnitude > 0.0001f, "Puerta responde físicamente al empuje");
                Debug.Log("BRAZOS_VALIDATION_OK");
                SessionState.SetInt(Key, 6);
                EditorApplication.ExitPlaymode();
            }
            else if (stage == 6 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            if (limite > 0 && EditorApplication.timeSinceStartup > limite) throw new Exception("Timeout de validación");
        }
        catch (Exception e)
        {
            SessionState.SetInt(Key, 0);
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void Capturar(string nombre)
    {
        var camera = interaction.GetComponent<Camera>();
        var prior = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        texture.Apply();
        Directory.CreateDirectory("Docs/Brazos");
        File.WriteAllBytes("Docs/Brazos/" + nombre + ".png", texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = prior;
        UnityEngine.Object.DestroyImmediate(texture);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
