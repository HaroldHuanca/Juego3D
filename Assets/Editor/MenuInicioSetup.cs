using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Genera el menú sin modificar el escenario original.</summary>
public static class MenuInicioSetup
{
    public const string MenuPath = "Assets/Scenes/MenuInicio.unity";
    public const string GamePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Juego de terror/Abrir pantalla de inicio")]
    public static void Abrir()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(MenuPath);
    }

    public static void Crear()
    {
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/MainMenu/HorrorBackground.png");
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Cámara del menú", typeof(Camera));
        camera.tag = "MainCamera";
        var view = camera.GetComponent<Camera>();
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;
        view.cullingMask = 0;
        new GameObject("Control del menú", typeof(MenuInicio));
        EditorSceneManager.SaveScene(scene, MenuPath);

        var others = EditorBuildSettings.scenes.Where(s => s.path != MenuPath && s.path != GamePath);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(GamePath, true)
        }.Concat(others).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("MENU_SETUP_OK: MenuInicio es la primera escena; SampleScene es el primer escenario.");
    }
}
