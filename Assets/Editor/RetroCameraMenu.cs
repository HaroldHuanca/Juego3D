using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menús y herramientas del Editor para generar, configurar y guardar el Canvas de Cámara Retro.
/// </summary>
public static class RetroCameraMenu
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = "Assets/Prefabs/UI/RetroCameraOverlay.prefab";
    private const string ProfilePath = "Assets/Settings/RetroCameraProfile.asset";

    [MenuItem("Herramientas/Cámara Retro/📹 Crear Canvas en Escena Actual", false, 10)]
    public static GameObject CrearCanvasEnEscena()
    {
        // 1. Asegurar que la cámara principal de la escena tenga Post-Processing activado en URP
        AsegurarPostProcessingEnCamara();

        // 2. Si ya existe un objeto en la escena (incluso desactivado), lo actualizamos y reconstruimos
        RetroCameraOverlay existente = Object.FindAnyObjectByType<RetroCameraOverlay>(FindObjectsInactive.Include);
        if (existente != null)
        {
            existente.ConstruirUIProcedural();
            AsegurarVolumeHijo(existente.gameObject, existente);
            Selection.activeGameObject = existente.gameObject;
            EditorGUIUtility.PingObject(existente.gameObject);
            EditorUtility.SetDirty(existente.gameObject);
            GuardarEscenaActiva();
            Debug.Log("<color=green>[Cámara Retro] ¡Canvas actualizado y escena guardada con éxito!</color>");
            return existente.gameObject;
        }

        // 3. Crear el GameObject raíz
        GameObject go = new GameObject("[Canvas_CamaraRetro]");
        Undo.RegisterCreatedObjectUndo(go, "Crear Canvas Cámara Retro");

        RetroCameraOverlay overlay = go.AddComponent<RetroCameraOverlay>();

        // 4. Generar la UI completa en el Canvas
        overlay.ConstruirUIProcedural();

        // 5. Asegurar el Volume de URP
        AsegurarVolumeHijo(go, overlay);

        // 6. Marcar y guardar la escena automáticamente para no perder el Canvas al reiniciar
        EditorUtility.SetDirty(go);
        GuardarEscenaActiva();

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        Debug.Log("<color=green>[Cámara Retro] ¡Canvas y Volume de cámara retro creados y guardados en la escena!</color> Presiona 'Play' para probarlo.");
        return go;
    }

    private static void AsegurarVolumeHijo(GameObject go, RetroCameraOverlay overlay)
    {
        Transform volPrevio = go.transform.Find("Volume_CamaraRetro");
        GameObject volObj = volPrevio != null ? volPrevio.gameObject : new GameObject("Volume_CamaraRetro");
        volObj.transform.SetParent(go.transform, false);

        Volume volume = volObj.GetComponent<Volume>();
        if (volume == null) volume = volObj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;

        VolumeProfile perfilAsset = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (perfilAsset != null)
        {
            volume.profile = perfilAsset;
        }

        overlay.AsignarVolume(volume);
    }

    private static void GuardarEscenaActiva()
    {
        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        if (!Application.isPlaying && escena.IsValid() && !string.IsNullOrEmpty(escena.path))
        {
            EditorSceneManager.SaveScene(escena);
        }
    }

    private static void AsegurarPostProcessingEnCamara()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (cam != null)
        {
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null && !camData.renderPostProcessing)
            {
                Undo.RecordObject(camData, "Activar Post Processing en Cámara");
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
                Debug.Log("<color=cyan>[Cámara Retro] Se activó 'Post Processing' en la cámara principal.</color>");
            }
        }
    }

    [MenuItem("Herramientas/Cámara Retro/📦 Generar Prefab en Prefabs-UI", false, 11)]
    public static void GenerarPrefab()
    {
        if (!Directory.Exists(PrefabFolder))
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
        }

        RetroCameraOverlay overlay = Object.FindAnyObjectByType<RetroCameraOverlay>(FindObjectsInactive.Include);
        GameObject objetoBase = null;

        if (overlay == null)
        {
            objetoBase = CrearCanvasEnEscena();
        }
        else
        {
            objetoBase = overlay.gameObject;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(objetoBase, PrefabPath, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GuardarEscenaActiva();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"<color=green>[Cámara Retro] Prefab y escena guardados exitosamente en:</color> {PrefabPath}");
    }
}
