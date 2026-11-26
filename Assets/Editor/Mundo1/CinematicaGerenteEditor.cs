using UnityEditor;
using UnityEngine;
using Mundo1;

namespace Mundo1.Editor
{
    [CustomEditor(typeof(CinematicaGerente))]
    public class CinematicaGerenteEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CinematicaGerente cinematica = (CinematicaGerente)target;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("🛠️ PRUEBAS RÁPIDAS EN PLAY MODE", EditorStyles.boldLabel);

            GUI.enabled = Application.isPlaying;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🗣️ Probar Hablar", GUILayout.Height(30)))
            {
                cinematica.ProbarAnimacionHablar();
            }

            if (GUILayout.Button("🧍 Probar Idle", GUILayout.Height(30)))
            {
                cinematica.ProbarAnimacionIdle();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("⚡ Probar Relámpago y Cambio de Skin", GUILayout.Height(32)))
            {
                cinematica.ProbarRelampagoYSwap();
            }

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("🎬 EJECUTAR CINEMÁTICA COMPLETA", GUILayout.Height(38)))
            {
                cinematica.IniciarCinematica();
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = true;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entra en Play Mode para probar las animaciones y el relámpago con los botones de arriba.", MessageType.Info);
            }
        }
    }
}
