using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class MonsterValidation
{
    static MonstruoWarehouse monster;
    static float deadline;
    static int phase;
    static Vector3 initial;
    static MonsterValidation() { if (SessionState.GetBool("MonsterValidation", false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool("MonsterValidation", true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (monster == null)
            {
                monster=UnityEngine.Object.FindAnyObjectByType<MonstruoWarehouse>();
                Check(monster != null,"Falta el monstruo");
                initial=monster.transform.position;
                var player=monster.jugador.GetComponent<PlayerMovement>();
                if(player!=null) player.enabled=false;
                var cc=monster.jugador.GetComponent<CharacterController>(); if(cc!=null) cc.enabled=false;
                monster.jugador.position=new Vector3(-20,1,-80);
                deadline=Time.time+5f;
                return;
            }
            Check(monster.Dentro(monster.transform.position), "El monstruo salió del almacén");
            if(Time.time<deadline) return;
            if(phase==0)
            {
                Check(Vector3.Distance(initial,monster.transform.position)>.2f,"No caminó durante la patrulla");
                var agent=monster.GetComponent<NavMeshAgent>();
                var filter=new NavMeshQueryFilter {agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                Check(NavMesh.SamplePosition(monster.transform.position+monster.transform.forward*3f,out var hit,3f,filter),"No hay punto cercano");
                monster.jugador.position=hit.position;
                phase=1; deadline=Time.time+1.5f;
            }
            else if(phase==1)
            {
                Check(monster.EstadoActual==MonstruoWarehouse.Estado.Persiguiendo,"No persigue al jugador cercano");
                monster.jugador.position=new Vector3(-20,1,-80);
                phase=2; deadline=Time.time+.6f;
            }
            else
            {
                Check(monster.EstadoActual!=MonstruoWarehouse.Estado.Persiguiendo,"Continúa persiguiendo al jugador fuera");
                Check(monster.animador.runtimeAnimatorController.animationClips.Length==1,"Hay otras animaciones activadas");
                Debug.Log("MONSTER_VALIDATION_OK: caminata, patrulla, persecucion, abandono y limite interior");
                Finish(0);
            }
        }
        catch(Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    static void Finish(int code)
    {
        SessionState.SetBool("MonsterValidation",false);
        EditorApplication.update-=Tick;
        EditorApplication.Exit(code);
    }
}
