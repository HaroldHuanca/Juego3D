using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Comprobación real del menú en Play Mode, ejecutable con -executeMethod.</summary>
[InitializeOnLoad]
public static class MenuInicioValidation
{
    private const string Key = "MenuInicioValidation.Stage";
    private static double nextCheck;
    private static double deadline;

    static MenuInicioValidation()
    {
        EditorApplication.update += Tick;
        deadline = EditorApplication.timeSinceStartup + 120;
    }

    public static void Run()
    {
        Assert(EditorBuildSettings.scenes[0].path == MenuInicioSetup.MenuPath, "Menú primero en Build Settings");
        Assert(Resources.Load<Texture2D>("MainMenu/HorrorBackground") != null, "Fondo IA importado");
        Assert(Resources.Load<Font>("MainMenu/Creepster-Regular") != null, "Tipografía importada");
        EditorSceneManager.OpenScene(MenuInicioSetup.MenuPath);
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0 || EditorApplication.timeSinceStartup < nextCheck) return;
        if (EditorApplication.timeSinceStartup > deadline) { Fail(new Exception("Timeout de validación")); return; }
        try
        {
            if (stage == 1 && EditorApplication.isPlaying)
            {
                var menu = UnityEngine.Object.FindAnyObjectByType<MenuInicio>();
                if (menu == null || menu.GetComponentInChildren<Canvas>() == null) return;
                Assert(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Cursor disponible");
                Assert(UnityEngine.Object.FindObjectsByType<AudioSource>().Length == 0, "Sin audio en el menú");
                var buttons = menu.GetComponentsInChildren<Button>();
                Assert(buttons.Length == 4, "Cuatro botones en inicio");
                Assert(!buttons.Single(b => b.name == "Opciones").interactable, "Opciones de adorno");
                Capture(menu, "MenuInicio-1920x1080.png", 1920, 1080);
                Capture(menu, "MenuInicio-1024x768.png", 1024, 768);
                buttons.Single(b => b.name == "Créditos").onClick.Invoke();
                Assert(menu.GetComponentsInChildren<Button>().Single().name == "Regresar", "Vista de créditos abierta");
                string[] names = { "Angelo Ccama Condori", "Harold Huanca Ccasa", "Jose Leonardo Cruz Gonzales", "Jorge Salvador Rodrigo Chipa" };
                var texts = menu.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
                foreach (string name in names) Assert(texts.Contains(name), "Nombre completo: " + name);
                Assert(EventSystem.current.currentSelectedGameObject.name == "Regresar", "Foco en regresar");
                Capture(menu, "Creditos-1920x1080.png", 1920, 1080);
                menu.GetComponentsInChildren<Button>().Single().onClick.Invoke();
                Assert(menu.GetComponentsInChildren<Button>().Length == 4, "Regreso al inicio");
                menu.GetComponentsInChildren<Button>().Single(b => b.name == "Jugar").onClick.Invoke();
                SessionState.SetInt(Key, 2);
                nextCheck = EditorApplication.timeSinceStartup + 1;
            }
            else if (stage == 2 && EditorApplication.isPlaying && SceneManager.GetActiveScene().path == MenuInicioSetup.GamePath)
            {
                Assert(UnityEngine.Object.FindAnyObjectByType<MenuInicio>() == null, "Menú descargado al jugar");
                Debug.Log("MENU_GAME_LOAD_OK: Jugar carga SampleScene.");
                SessionState.SetInt(Key, 3);
                EditorApplication.ExitPlaymode();
            }
            else if (stage == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(MenuInicioSetup.MenuPath);
                SessionState.SetInt(Key, 4);
                EditorApplication.EnterPlaymode();
            }
            else if (stage == 4 && EditorApplication.isPlaying)
            {
                var menu = UnityEngine.Object.FindAnyObjectByType<MenuInicio>();
                if (menu == null || menu.GetComponentInChildren<Canvas>() == null) return;
                SessionState.SetInt(Key, 5);
                menu.GetComponentsInChildren<Button>().Single(b => b.name == "Salir").onClick.Invoke();
            }
            else if (stage == 5 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                EditorSceneManager.OpenScene(MenuInicioSetup.MenuPath);
                Debug.Log("MENU_VALIDATION_OK: créditos, regresar, jugar, salir, recursos y ausencia de audio verificados.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("MENU_CHECK_OK: " + message);
    }

    private static void Fail(Exception error)
    {
        SessionState.SetInt(Key, 0);
        Debug.LogException(error);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void Capture(MenuInicio menu, string filename, int width, int height)
    {
        var canvas = menu.GetComponentInChildren<Canvas>();
        var camera = Camera.main;
        var target = new RenderTexture(width, height, 24);
        var prior = RenderTexture.active;
        camera.targetTexture = target;
        // Renderiza la misma interfaz a una textura para revisar el diseño en modo batch.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        camera.cullingMask = ~0;
        Canvas.ForceUpdateCanvases();
        canvas.GetComponent<CanvasScaler>().SendMessage("Update");
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs/MenuPreview");
        File.WriteAllBytes("Logs/MenuPreview/" + filename, image.EncodeToPNG());
        RenderTexture.active = prior;
        camera.targetTexture = null;
        camera.cullingMask = 0;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
