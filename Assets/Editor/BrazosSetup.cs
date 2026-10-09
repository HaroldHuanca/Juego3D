using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BrazosSetup
{
    public const string Carpeta = "Assets/Art/FirstPersonArms";
    public const string Modelo = Carpeta + "/arms_rig.fbx";

    [MenuItem("Juego de terror/Configurar brazos en escena actual")]
    public static void ConfigurarActual()
    {
        PrepararAssets();
        Instalar();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Run()
    {
        PrepararAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Instalar();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("BRAZOS_SETUP_OK");
    }

    private static void PrepararAssets()
    {
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.loopTime = clip.name.EndsWith("relax", StringComparison.OrdinalIgnoreCase);
            clip.lockRootPositionXZ = true;
            clip.lockRootHeightY = true;
            clip.lockRootRotation = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        foreach (string nombre in new[] { "arms_01", "arms_gloves_01" })
        {
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Carpeta + "/" + nombre + ".png");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.maxTextureSize = 512;
            textureImporter.SaveAndReimport();
            string path = Carpeta + "/" + nombre + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Carpeta + "/" + nombre + ".png"));
            mat.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(mat);
        }
        CrearRecogible();
    }

    private static AnimationClip Clip(string nombre)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") &&
                (c.name == nombre || c.name.EndsWith("|" + nombre)));
        if (clip == null) throw new Exception("No se encuentra el clip " + nombre);
        return clip;
    }

    private static int PrepararCapa()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        int layer = LayerMask.NameToLayer("BrazosFPS");
        if (layer >= 0) return layer;
        for (int i = 8; i < 32; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue != "") continue;
            layers.GetArrayElementAtIndex(i).stringValue = "BrazosFPS";
            tags.ApplyModifiedProperties();
            return i;
        }
        throw new Exception("No hay una capa libre para BrazosFPS.");
    }

    private static void Instalar()
    {
        var mirada = UnityEngine.Object.FindAnyObjectByType<Camara>();
        if (mirada == null) throw new Exception("La escena necesita el componente Camara del jugador.");
        var principal = mirada.GetComponent<Camera>();
        var player = mirada.Player != null ? mirada.Player : mirada.transform.parent;
        int layer = PrepararCapa();
        var anterior = mirada.transform.Find("BrazosFPS");
        if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);

        var root = new GameObject("BrazosFPS");
        root.transform.SetParent(mirada.transform, false);
        // El jugador original tiene escala no uniforme; el rig conserva sus proporciones.
        Vector3 escala = mirada.transform.lossyScale;
        root.transform.localScale = new Vector3(1f / escala.x, 1f / escala.y, 1f / escala.z);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo), root.transform);
        rig.name = "Rig de brazos";
        var huesoCamara = rig.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "camera");
        rig.transform.localPosition = huesoCamara == null ? new Vector3(0f, -1.74f, 0f) : -root.transform.InverseTransformPoint(huesoCamara.position);
        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Carpeta + "/arms_01.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }
        var animator = rig.GetComponent<Animator>();
        if (animator == null) animator = rig.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        string controllerPath = Carpeta + "/Brazos.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        var idle = machine.AddState("Reposo"); idle.motion = Clip("relax");
        machine.defaultState = idle;
        machine.AddState("Recoger").motion = Clip("grab.R");
        machine.AddState("Empujar").motion = Clip("push.R");
        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(controller);
        var brazos = root.AddComponent<BrazosPrimeraPersona>();
        brazos.animador = animator;
        brazos.puntoVista = mirada.transform;
        brazos.reposo = Clip("relax"); brazos.recoger = Clip("grab.R"); brazos.empujar = Clip("push.R");

        var cameraObject = new GameObject("Camara de brazos", typeof(Camera));
        cameraObject.transform.SetParent(mirada.transform, false);
        var overlay = cameraObject.GetComponent<Camera>();
        overlay.cullingMask = 1 << layer;
        overlay.nearClipPlane = 0.01f; overlay.farClipPlane = 5f;
        overlay.fieldOfView = principal.fieldOfView;
        overlay.allowHDR = principal.allowHDR;
        // Forma parte del mismo grupo para que reinstalar no deje cámaras duplicadas.
        cameraObject.transform.SetParent(root.transform, true);
        cameraObject.transform.position = principal.transform.position;
        cameraObject.transform.rotation = principal.transform.rotation;
        var overlayData = overlay.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        var overlaySettings = new SerializedObject(overlayData);
        overlaySettings.FindProperty("m_ClearDepth").boolValue = true;
        overlaySettings.ApplyModifiedPropertiesWithoutUndo();
        overlayData.renderShadows = false;
        var baseData = principal.GetUniversalAdditionalCameraData();
        baseData.cameraStack.RemoveAll(c => c == null);
        baseData.cameraStack.Add(overlay);
        principal.cullingMask &= ~(1 << layer);
        if (player != null && player.TryGetComponent<MeshRenderer>(out var cuerpo)) cuerpo.enabled = false;
        var interaction = mirada.GetComponent<InteraccionJugador>();
        if (interaction == null) interaction = mirada.gameObject.AddComponent<InteraccionJugador>();
        interaction.brazos = brazos;
        interaction.jugador = player;
        interaction.capasInteractuables = ~(1 << layer);
        Debug.Log("BRAZOS_CLIPS: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<AnimationClip>().Select(c => c.name)));
    }

    private static void CrearRecogible()
    {
        string path = Carpeta + "/ObjetoRecogibleEjemplo.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var ejemplo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ejemplo.name = "Objeto recogible";
        ejemplo.transform.localScale = Vector3.one * 0.15f;
        ejemplo.AddComponent<ObjetoRecogible>();
        PrefabUtility.SaveAsPrefabAsset(ejemplo, path);
        UnityEngine.Object.DestroyImmediate(ejemplo);
    }
}
