using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class MonsterSetup
{
    const string Folder = "Assets/Monster/";
    const string Model = Folder + "FinalMonster 1.fbx";

    [MenuItem("Juego de terror/Configurar monstruo de warehouse")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var warehouse = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name == "warehouse6");
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        var clips = importer.defaultClipAnimations.Where(c => c.name.EndsWith("|Caminar") || c.name == "Caminar").ToArray();
        if (clips.Length != 1) throw new Exception("No se encuentra el ciclo Caminar del monstruo.");
        clips[0].loopTime = true;
        clips[0].loopPose = true;
        clips[0].lockRootPositionXZ = true;
        clips[0].lockRootHeightY = true;
        clips[0].lockRootRotation = true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__") && c.name.EndsWith("Caminar"));
        string controllerPath = Folder + "MonstruoCaminar.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var s in machine.states) machine.RemoveState(s.state);
        var walk = machine.AddState("Caminar"); walk.motion = clip; machine.defaultState = walk;
        EditorUtility.SetDirty(controller);

        var old = GameObject.Find("Monstruo Warehouse");
        int previousType = old != null && old.GetComponentInChildren<NavMeshAgent>() != null ? old.GetComponentInChildren<NavMeshAgent>().agentTypeID : -1;
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Monstruo Warehouse");
        var zoneObject = new GameObject("Limite interior warehouse");
        zoneObject.transform.SetParent(root.transform);
        var floor = warehouse.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("06. Piso")).bounds;
        var zone = zoneObject.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(floor.center.x, floor.max.y + 2.4f, floor.center.z);
        zone.size = new Vector3(floor.size.x - 2.5f, 5f, floor.size.z - 2.5f);
        // Volumen de referencia, no interviene en la física del jugador.
        zone.enabled = false;

        var settings = previousType < 0 ? NavMesh.CreateSettings() : NavMesh.GetSettingsByID(previousType);
        settings.agentRadius=.55f; settings.agentHeight=2.5f; settings.agentClimb=.25f; settings.agentSlope=40f;
        settings.overrideVoxelSize=true; settings.voxelSize=.15f;
        var sources = new List<NavMeshBuildSource>();
        foreach(var mesh in warehouse.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null) continue;
            sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Mesh, sourceObject=mesh.sharedMesh,
                transform=mesh.transform.localToWorldMatrix, area=0 });
        }
        var bounds = new Bounds(zone.center, zone.size);
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        if (data == null) throw new Exception("No se pudo generar la navegación del almacén.");
        string navPath=Folder+"WarehouseNavigation.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(data,navPath);
        var instance=NavMesh.AddNavMeshData(data);
        var filter=new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas};
        if (!NavMesh.SamplePosition(new Vector3(floor.center.x, floor.max.y, floor.center.z),out var spawn,15f,filter))
            throw new Exception("No hay suelo navegable para el monstruo.");
        var actor=new GameObject("Monstruo"); actor.transform.SetParent(root.transform); actor.transform.position=spawn.position;
        instance.Remove();
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),actor.transform);
        model.transform.localPosition=Vector3.zero;
        model.transform.localRotation=Quaternion.identity;
        // Mantener la proporción original y ajustar una altura de 2.5 metros.
        var renderers=model.GetComponentsInChildren<Renderer>();
        var body=renderers[0].bounds;
        foreach(var r in renderers) body.Encapsulate(r.bounds);
        float scale=2.5f/body.size.y;
        model.transform.localScale=Vector3.one*scale;
        model.transform.localPosition=new Vector3(-body.center.x+actor.transform.position.x,-body.min.y+actor.transform.position.y,-body.center.z+actor.transform.position.z)*scale;
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Monstruo.mat");
        if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"Monstruo.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"body_Base_Color.png"));
        var normalImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"body_Normal.png");
        normalImporter.textureType=TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"body_Normal.png"));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Smoothness",.15f); EditorUtility.SetDirty(material);
        foreach(var r in renderers) {r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray(); if(r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;}
        var animator=model.GetComponent<Animator>();
        if(animator == null) animator=model.AddComponent<Animator>();
        animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var agent=actor.AddComponent<NavMeshAgent>();
        agent.enabled=false; agent.agentTypeID=settings.agentTypeID; agent.radius=.55f; agent.height=2.5f; agent.speed=1.5f;
        agent.acceleration=5f; agent.angularSpeed=180f; agent.stoppingDistance=.85f;
        var monster=actor.AddComponent<MonstruoWarehouse>();
        monster.zona=zone; monster.animador=animator; monster.navegacion=data;
        monster.jugador=Object.FindAnyObjectByType<PlayerMovement>()?.transform;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MONSTER_SETUP_OK spawn="+spawn.position+" clip="+clip.name+" navigation="+sources.Count);
    }
}
