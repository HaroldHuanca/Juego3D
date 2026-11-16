using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Prepara GI del local y genera lightmaps en el editor, nunca durante el juego.</summary>
[InitializeOnLoad]
public static class TiendaIluminacionSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SettingsPath = "Assets/Settings/TiendaGI.lighting";
    private const string ModelPath = "Assets/6twelve/Models/6twelve.fbx";
    private const string ProbeName = "Probes GI tienda";
    private const string PendingKey = "TiendaGI.BakePending";
    private static readonly string RequestPath = Path.GetFullPath("Library/TiendaGI.request");
    private static readonly string StatusPath = Path.GetFullPath("Library/TiendaGI-status.txt");
    private static double nextCheck;
    private static string lastStatus;

    static TiendaIluminacionSetup()
    {
        EditorApplication.update += ProcessRequest;
        Lightmapping.bakeCompleted += BakeCompleted;
    }

    private static void Status(string message)
    {
        if (lastStatus == message) return;
        lastStatus = message;
        File.WriteAllText(StatusPath, DateTime.UtcNow.ToString("O") + "\n" + message);
        Debug.Log("TIENDA_GI: " + message);
    }

    private static void ProcessRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Status("ESPERANDO: salir de Play para preparar y hornear la escena.");
            return;
        }
        if (Lightmapping.isRunning) return;
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            Status("ESPERANDO: abrir Assets/Scenes/SampleScene.unity.");
            return;
        }
        // Consume the explicit one-shot request. Never repeat a bake after a compile.
        File.Delete(RequestPath);
        try { PrepararYHornear(); }
        catch (Exception exception)
        {
            SessionState.SetBool(PendingKey, false);
            Status("ERROR: " + exception.Message);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Videojuego/Iluminacion/Preparar y hornear tienda")]
    public static void PrepararYHornear()
    {
        Preparar();
        SessionState.SetBool(PendingKey, true);
        Status("HORNEANDO: calculando luz directa, rebotes y probes. Esperar a la finalizacion.");
        if (!Lightmapping.BakeAsync())
        {
            SessionState.SetBool(PendingKey, false);
            throw new InvalidOperationException("Unity no pudo iniciar el horneado. Revisar la consola y la ventana Lighting.");
        }
    }

    [MenuItem("Tools/Videojuego/Iluminacion/Preparar tienda sin hornear")]
    public static void Preparar()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Preparar la iluminacion fuera de Play y sin otro horneado activo.");
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Abrir SampleScene antes de preparar la tienda.");
        if (SceneManager.sceneCount != 1)
            throw new InvalidOperationException("Mantener solo SampleScene abierta para no hornear escenas aditivas por accidente.");
        var lights = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
            .Where(l => l.name.StartsWith("LuzTienda", StringComparison.Ordinal)).ToArray();
        if (lights.Length == 0) throw new InvalidOperationException("No se encontraron las luces LuzTienda.");

        // Model UV2 is needed for a separate, non-overlapping lightmap atlas.
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (!importer.generateSecondaryUV)
        {
            importer.generateSecondaryUV = true;
            importer.SaveAndReimport();
        }

        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "TiendaGI" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        // Unity 6 keeps manual baking as a serialized workflow setting.
        var serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("m_GIWorkflowMode").intValue = 1;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        settings.directionalityMode = LightmapsMode.CombinedDirectional;
        settings.lightmapResolution = 16;
        settings.lightmapMaxSize = 2048;
        settings.lightmapPadding = 4;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 128;
        settings.minBounces = 2;
        settings.maxBounces = 4;
        settings.indirectScale = 1;
        settings.albedoBoost = 1;
        settings.ao = false;
        settings.lightProbeSampleCountMultiplier = 2;
        Lightmapping.lightingSettings = settings;
        EditorUtility.SetDirty(settings);

        Bounds room = new Bounds(lights[0].transform.position, Vector3.zero);
        foreach (var light in lights)
        {
            room.Encapsulate(light.transform.position);
            Undo.RecordObject(light, "Preparar luces mixtas tienda");
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.bounceIntensity = 1;
            light.color = new Color(0.98f, 1f, 0.99f);
            light.useColorTemperature = true;
            light.colorTemperature = 6500;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        room.Expand(new Vector3(6, 6, 7));
        room.center += Vector3.down * 1.5f;
        int geometry = 0;
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
        {
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
            if (source != ModelPath) continue;
            bool moving = renderer.GetComponentInParent<Rigidbody>() != null ||
                renderer.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 &&
                renderer.name.IndexOf("frame", StringComparison.OrdinalIgnoreCase) < 0;
            bool glass = renderer.sharedMaterials.Any(m => m != null &&
                (m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 || m.name == "VidrioTienda"));
            // Keep moving doors, transparent panes and distant outdoor geometry out of the bake.
            bool contribute = !moving && !glass && room.Intersects(renderer.bounds) &&
                Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) < 60;
            var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            var updated = contribute ? flags | StaticEditorFlags.ContributeGI : flags & ~StaticEditorFlags.ContributeGI;
            if (flags != updated)
            {
                Undo.RecordObject(renderer.gameObject, "Preparar geometria GI tienda");
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, updated);
            }
            if (!contribute) continue;
            Undo.RecordObject(renderer, "Preparar lightmaps tienda");
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = 1;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            geometry++;
        }
        if (geometry == 0) throw new InvalidOperationException("No se encontro geometria estatica de la tienda para hornear.");
        foreach (var light in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)))
            if (light.name.StartsWith("Relleno interior", StringComparison.Ordinal))
            {
                Undo.RecordObject(light, "Desactivar relleno provisional");
                light.enabled = false;
            }
        BuildProbes(lights);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena preparada.");
        AssetDatabase.SaveAssets();
        Status("PREPARADO: " + geometry + " superficies GI, " + lights.Length + " luces mixtas y probes para moviles. Falta completar Bake.");
    }

    private static void BuildProbes(Light[] lights)
    {
        var parent = lights[0].transform.parent;
        if (parent == null) throw new InvalidOperationException("Las luces de la tienda deben compartir un objeto padre.");
        var existing = parent != null ? parent.Find(ProbeName) : null;
        var go = existing != null ? existing.gameObject : new GameObject(ProbeName);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Crear probes GI tienda");
            go.transform.SetParent(parent, false);
        }
        var group = go.GetComponent<LightProbeGroup>();
        if (group == null) group = Undo.AddComponent<LightProbeGroup>(go);
        Undo.RecordObject(group, "Distribuir probes GI tienda");
        // Three heights through the aisles. Keep the grid inside the shop, away from the exterior.
        var positions = new System.Collections.Generic.List<Vector3>();
        for (int x = 0; x < 5; x++)
            for (int z = 0; z < 6; z++)
                foreach (float y in new[] { -0.6f, 0.55f, 1.65f })
                    positions.Add(go.transform.InverseTransformPoint(parent.TransformPoint(
                        new Vector3(Mathf.Lerp(-5.8f, 2.1f, x / 4f), y, Mathf.Lerp(-10.8f, -1.6f, z / 5f)))));
        // Checkout / annex uses the same light rig but sits outside the main aisle grid.
        foreach (float z in new[] { -6f, -4f })
            foreach (float x in new[] { 5.5f, 8f })
                foreach (float y in new[] { -0.6f, 0.55f, 1.65f })
                    positions.Add(go.transform.InverseTransformPoint(parent.TransformPoint(new Vector3(x, y, z))));
        Physics.SyncTransforms();
        int geometryMask = ~((1 << 8) | (1 << 9));
        var validPositions = positions.Where(p => !Physics.CheckSphere(go.transform.TransformPoint(p),
            0.12f, geometryMask, QueryTriggerInteraction.Ignore)).ToArray();
        if (validPositions.Length < 8)
            throw new InvalidOperationException("No hay suficientes posiciones libres para los probes del local.");
        group.probePositions = validPositions;
    }

    private static void BakeCompleted()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);
        int maps = LightmapSettings.lightmaps.Count(m => m != null && m.lightmapColor != null);
        int probes = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
        if (maps == 0 || probes == 0)
        {
            Status("ERROR: horneado finalizado sin lightmaps o sin probes. Revisar Lighting y consola.");
            return;
        }
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Status("ERROR: SampleScene no esta cargada para verificar el resultado.");
            return;
        }
        int surfaces = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
            .Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) != 0 &&
                r.lightmapIndex >= 0 && r.lightmapIndex < maps);
        if (surfaces == 0)
        {
            Status("ERROR: hay datos de iluminacion, pero ninguna superficie tiene lightmap asignado.");
            return;
        }
        if (!EditorSceneManager.SaveScene(scene))
        {
            Status("ERROR: se genero la iluminacion pero no se pudo guardar la escena.");
            return;
        }
        AssetDatabase.SaveAssets();
        Status("COMPLETADO: " + maps + " lightmaps, " + surfaces + " superficies y " + probes +
            " probes horneados. Pendiente revision visual.");
    }
}
