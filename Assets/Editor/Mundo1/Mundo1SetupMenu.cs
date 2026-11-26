using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Mundo1;
using Mundo1.UI;
using Tienda.SistemaVentas;

namespace Mundo1.Editor
{
    public static class Mundo1SetupMenu
    {
        private const string PrefabRemy = "Assets/Prefabs/Clientes/Remy.prefab";

        private static readonly Vector3 PosicionGerenteDefecto = new Vector3(30.895031f, 1.19203019f, 47.9250832f);
        private static readonly Quaternion RotacionGerenteDefecto = Quaternion.Euler(0f, 85.05483f, 0f);
        private static readonly Vector3 PosicionJugadorFrenteGerenteDefecto = new Vector3(33.3301849f, 2.44203019f, 47.9833031f);
        private static readonly Quaternion RotacionJugadorFrenteGerenteDefecto = Quaternion.Euler(0f, 270f, 0f);
        private static readonly Vector3 PosicionSalidaPuertaDefecto = new Vector3(24.75f, 1.5f, 51.25f);
        private static readonly Vector3 PosicionExteriorFinalDefecto = new Vector3(26.900589f, 1.34153366f, 58.2430878f);
        private static readonly Vector3 PosicionCamaraExteriorDefecto = new Vector3(24.25f, 2.5f, 64.5f);
        private static readonly Quaternion RotacionCamaraExteriorDefecto = Quaternion.Euler(0f, 180f, 0f);

        private static GameObject BuscarEnEscena(Scene escena, string nombre)
        {
            if (!escena.IsValid() || !escena.isLoaded) return null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                var transforms = raiz.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == nombre) return transforms[i].gameObject;
                }
            }
            return null;
        }

        private static GameObject BuscarPorNombres(Scene escena, params string[] nombres)
        {
            foreach (var nombre in nombres)
            {
                var encontrado = BuscarEnEscena(escena, nombre);
                if (encontrado != null) return encontrado;
            }
            return null;
        }

        private static GameObject ObtenerOCrearPunto(Scene escena, string nombre, Vector3 posDefecto, Quaternion rotDefecto, string undoLabel)
        {
            var punto = BuscarEnEscena(escena, nombre);
            if (punto == null)
            {
                punto = new GameObject(nombre);
                punto.transform.position = posDefecto;
                punto.transform.rotation = rotDefecto;
                Undo.RegisterCreatedObjectUndo(punto, undoLabel);
            }
            return punto;
        }

        private static void GuardarEscena(Scene escena)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            if (!Application.isPlaying && escena.IsValid() && !string.IsNullOrEmpty(escena.path))
            {
                EditorSceneManager.SaveScene(escena);
            }
        }

        [MenuItem("Herramientas/Mundo 1/🎬 Configurar Prólogo y Director en Escena", false, 1)]
        public static void ConfigurarMundo1EnEscena()
        {
            var escena = EditorSceneManager.GetActiveScene();

            // 1. Buscar o crear el GameObject raíz del Director de Mundo 1 (incluyendo desactivados)
            GameObject managerGo = BuscarEnEscena(escena, "[MUNDO_1_MANAGER]");
            if (managerGo == null)
            {
                managerGo = new GameObject("[MUNDO_1_MANAGER]");
                Undo.RegisterCreatedObjectUndo(managerGo, "Crear Gestor de Mundo 1");
            }

            var director = managerGo.GetComponent<DirectorTurno>() ?? managerGo.AddComponent<DirectorTurno>();
            var tormenta = managerGo.GetComponent<ControladorTormenta>() ?? managerGo.AddComponent<ControladorTormenta>();
            var transicion = managerGo.GetComponent<TransicionNivel>() ?? managerGo.AddComponent<TransicionNivel>();
            var monitorCaja = managerGo.GetComponent<MonitorCajaMundo1>() ?? managerGo.AddComponent<MonitorCajaMundo1>();
            var hud = managerGo.GetComponent<HUDTareasMundo1>() ?? managerGo.AddComponent<HUDTareasMundo1>();

            // 2. Buscar o instanciar al Gerente con modelo 3D real (incluyendo desactivados o bajo --NPC--)
            var cinematicaExistente = Object.FindAnyObjectByType<CinematicaGerente>(FindObjectsInactive.Include);
            GameObject gerenteGo = cinematicaExistente != null
                ? cinematicaExistente.gameObject
                : BuscarPorNombres(escena, "[Gerente_Prologo]", "Remy", "Adan");

            if (gerenteGo == null)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRemy);
                if (prefabAsset != null)
                {
                    gerenteGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                    gerenteGo.name = "[Gerente_Prologo]";
                    Undo.RegisterCreatedObjectUndo(gerenteGo, "Instanciar Gerente");
                }
                else
                {
                    gerenteGo = new GameObject("[Gerente_Prologo]");
                    Undo.RegisterCreatedObjectUndo(gerenteGo, "Crear Gerente");
                }
            }
            else if (gerenteGo.name != "[Gerente_Prologo]")
            {
                Undo.RecordObject(gerenteGo, "Renombrar Gerente");
                gerenteGo.name = "[Gerente_Prologo]";
            }

            // Asegurar que el gerente tenga el componente de cinemática
            var cinematica = gerenteGo.GetComponent<CinematicaGerente>() ?? gerenteGo.AddComponent<CinematicaGerente>();

            // Limpiar componentes de cliente antiguo en el gerente para que no interfieran
            var clienteIAVieja = gerenteGo.GetComponent<Tienda.Clientes.ClienteIA>();
            if (clienteIAVieja != null) Object.DestroyImmediate(clienteIAVieja);

            var iconoViejo = gerenteGo.GetComponent<Tienda.Clientes.IconoFlotanteCliente>();
            if (iconoViejo != null) Object.DestroyImmediate(iconoViejo);

            // 3. Solo ubicar al gerente si quedó en (0,0,0) tras crearse desde cero
            if (gerenteGo.transform.position.magnitude < 1f)
            {
                var puntoCaja = BuscarPorNombres(escena, "Punto_CajaRegistradora", "Punto_Caja");
                if (puntoCaja != null)
                {
                    gerenteGo.transform.position = puntoCaja.transform.position;
                    gerenteGo.transform.rotation = puntoCaja.transform.rotation;
                }
                else
                {
                    gerenteGo.transform.position = PosicionGerenteDefecto;
                    gerenteGo.transform.rotation = RotacionGerenteDefecto;
                }
            }

            // 4-6. Buscar puntos existentes (preservando su posición) o crearlos con la posición de respaldo de la tienda
            var puntoJugador = ObtenerOCrearPunto(
                escena, "Punto_JugadorFrenteGerente",
                PosicionJugadorFrenteGerenteDefecto, RotacionJugadorFrenteGerenteDefecto,
                "Crear Punto Jugador Frente Gerente");

            var puntoSalida = ObtenerOCrearPunto(
                escena, "Punto_SalidaPuerta",
                PosicionSalidaPuertaDefecto, Quaternion.identity,
                "Crear Punto Salida");

            var puntoExteriorFinal = ObtenerOCrearPunto(
                escena, "Punto_ExteriorFinal",
                PosicionExteriorFinalDefecto, Quaternion.identity,
                "Crear Punto Exterior Final");

            var puntoExterior = ObtenerOCrearPunto(
                escena, "Punto_CamaraExterior",
                PosicionCamaraExteriorDefecto, RotacionCamaraExteriorDefecto,
                "Crear Punto Camara Exterior");

            // 7. Auto-vincular todas las propiedades en CinematicaGerente
            var soCinematica = new SerializedObject(cinematica);
            soCinematica.FindProperty("objetoGerenteRaiz").objectReferenceValue = gerenteGo;
            soCinematica.FindProperty("animadorGerente").objectReferenceValue = gerenteGo.GetComponentInChildren<Animator>(true);
            soCinematica.FindProperty("agenteGerente").objectReferenceValue = gerenteGo.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
            soCinematica.FindProperty("puntoCamaraExterior").objectReferenceValue = puntoExterior.transform;
            soCinematica.FindProperty("puntoSalidaPuerta").objectReferenceValue = puntoSalida.transform;
            soCinematica.FindProperty("puntoExteriorFinal").objectReferenceValue = puntoExteriorFinal.transform;
            soCinematica.FindProperty("puntoJugadorFrenteGerente").objectReferenceValue = puntoJugador.transform;

            // Overlay retro: buscar existente (incluso desactivado) o crearlo si falta
            var overlay = Object.FindAnyObjectByType<RetroCameraOverlay>(FindObjectsInactive.Include);
            if (overlay == null)
            {
                var canvasGo = RetroCameraMenu.CrearCanvasEnEscena();
                if (canvasGo != null) overlay = canvasGo.GetComponent<RetroCameraOverlay>();
            }
            if (overlay != null)
            {
                soCinematica.FindProperty("overlayCamaraRetro").objectReferenceValue = overlay;
            }

            // Modelo normal
            soCinematica.FindProperty("modeloGerenteNormal").objectReferenceValue = gerenteGo;

            // Monstruo
            var monstruoObj = BuscarEnEscena(escena, "FinalMonster 1") ?? GameObject.FindGameObjectWithTag("Monster");
            if (monstruoObj != null)
            {
                soCinematica.FindProperty("modeloGerenteMonstruo").objectReferenceValue = monstruoObj;
            }

            // Jugador y sus scripts
            var jugador = GameObject.FindGameObjectWithTag("Player") ?? BuscarEnEscena(escena, "Player");
            if (jugador != null)
            {
                var mov = jugador.GetComponent<PlayerMovement>() ?? jugador.GetComponentInChildren<PlayerMovement>(true);
                var camObj = jugador.GetComponentInChildren<Camera>(true);
                soCinematica.FindProperty("transformJugadorRaiz").objectReferenceValue = jugador.transform;
                soCinematica.FindProperty("scriptMovimientoJugador").objectReferenceValue = mov;
                if (camObj != null)
                {
                    soCinematica.FindProperty("camaraPrincipal").objectReferenceValue = camObj;
                    var scriptCam = camObj.GetComponent<Camara>() ?? jugador.GetComponentInChildren<Camara>(true);
                    if (scriptCam != null)
                    {
                        soCinematica.FindProperty("scriptCamaraJugador").objectReferenceValue = scriptCam;
                    }
                }
            }

            soCinematica.ApplyModifiedProperties();

            // 8. Luces de la tienda en DirectorTurno
            var luces = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(l => l.name.StartsWith("LuzTienda") || l.name.StartsWith("Fluorescente"))
                .Select(l => l.gameObject).ToArray();

            if (luces.Length > 0)
            {
                var soDirector = new SerializedObject(director);
                var propLuces = soDirector.FindProperty("lucesTienda");
                propLuces.ClearArray();
                for (int i = 0; i < luces.Length; i++)
                {
                    propLuces.InsertArrayElementAtIndex(i);
                    propLuces.GetArrayElementAtIndex(i).objectReferenceValue = luces[i];
                }
                soDirector.ApplyModifiedProperties();
            }

            // 9. Desactivar el SPAWNER_CLIENTES y NPCs antiguos en la escena
            var spawnerViejo = Object.FindAnyObjectByType<Tienda.Clientes.SpawnerClientes>();
            if (spawnerViejo != null && spawnerViejo.gameObject.activeSelf)
            {
                Undo.RecordObject(spawnerViejo.gameObject, "Desactivar Spawner Antiguo");
                spawnerViejo.gameObject.SetActive(false);
                EditorUtility.SetDirty(spawnerViejo.gameObject);
                Debug.Log("<color=yellow>[Mundo 1] Se desactivó 'SPAWNER_CLIENTES' para evitar conflictos.</color>");
            }

            var grupoNPC = BuscarEnEscena(escena, "--NPC--");
            if (grupoNPC != null)
            {
                for (int i = 0; i < grupoNPC.transform.childCount; i++)
                {
                    var hijo = grupoNPC.transform.GetChild(i).gameObject;
                    if (hijo != gerenteGo && hijo.name != "Punto_CajaRegistradora" && hijo.name != "Punto_Caja")
                    {
                        Undo.RecordObject(hijo, "Desactivar NPC Antiguo");
                        hijo.SetActive(false);
                        EditorUtility.SetDirty(hijo);
                    }
                }
            }

            EditorUtility.SetDirty(managerGo);
            EditorUtility.SetDirty(gerenteGo);

            // Auto-asignar audios si están presentes en Assets/Audio
            try
            {
                AutoAsignarAudios();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Mundo 1] Aviso al asignar audios: {ex.Message}");
            }

            GuardarEscena(escena);

            Selection.activeGameObject = gerenteGo;
            EditorGUIUtility.PingObject(gerenteGo);

            Debug.Log("<color=green>[Mundo 1] ¡Configuración completada y escena guardada en disco!</color>");
        }

        [MenuItem("Herramientas/Mundo 1/🧭 Regenerar y Hornear NavMesh", false, 2)]
        public static void CrearYHornearNavMesh()
        {
            var escena = EditorSceneManager.GetActiveScene();

            // Buscar si ya existe un NavMeshSurface en la escena
            var surface = Object.FindAnyObjectByType<NavMeshSurface>(FindObjectsInactive.Include);
            if (surface == null)
            {
                var go = new GameObject("[NavMesh_Superficie]");
                surface = go.AddComponent<NavMeshSurface>();
                Undo.RegisterCreatedObjectUndo(go, "Crear NavMeshSurface");
            }

            // Hornear la malla de navegación
            surface.BuildNavMesh();

            EditorUtility.SetDirty(surface.gameObject);
            GuardarEscena(escena);

            Selection.activeGameObject = surface.gameObject;
            EditorGUIUtility.PingObject(surface.gameObject);

            Debug.Log("<color=green>[NavMesh] ¡Malla de navegación (NavMesh) creada, horneada y escena guardada exitosamente!</color>");
        }

        [MenuItem("Herramientas/Mundo 1/🔊 Auto-Asignar Audios Descargados", false, 3)]
        public static void AutoAsignarAudios()
        {
            var escena = EditorSceneManager.GetActiveScene();
            int asignados = 0;

            AudioClip CargarClip(string ruta)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
                if (clip == null)
                {
                    Debug.LogWarning($"[Auto-Audio] No se encontró o aún no se ha importado el clip en: {ruta}");
                }
                return clip;
            }

            // 1. HUDTareasMundo1
            var hud = Object.FindAnyObjectByType<HUDTareasMundo1>(FindObjectsInactive.Include);
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                var c1 = CargarClip("Assets/Audio/UI/sonidoTareaCompletada.mp3");
                var c2 = CargarClip("Assets/Audio/UI/sonidoNuevaTarea.mp3");
                var c3 = CargarClip("Assets/Audio/Ambiente/ambientehorror.mp3");

                if (c1 != null) { so.FindProperty("sonidoTareaCompletada").objectReferenceValue = c1; asignados++; }
                if (c2 != null) { so.FindProperty("sonidoNuevaTarea").objectReferenceValue = c2; asignados++; }
                if (c3 != null) { so.FindProperty("musicaFondo").objectReferenceValue = c3; asignados++; }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hud);
            }

            // 2. RetroCameraOverlay
            var cam = Object.FindAnyObjectByType<RetroCameraOverlay>(FindObjectsInactive.Include);
            if (cam != null)
            {
                var so = new SerializedObject(cam);
                var c1 = CargarClip("Assets/Audio/Camara/sonidoEncender.mp3");
                var c2 = CargarClip("Assets/Audio/Camara/sonidoApagar.mp3");
                var c3 = CargarClip("Assets/Audio/Camara/sonidoGlitch.mp3");

                if (c1 != null) { so.FindProperty("sonidoEncender").objectReferenceValue = c1; asignados++; }
                if (c2 != null) { so.FindProperty("sonidoApagar").objectReferenceValue = c2; asignados++; }
                if (c3 != null) { so.FindProperty("sonidoGlitch").objectReferenceValue = c3; asignados++; }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cam);
            }

            // 3. CinematicaGerente
            var cin = Object.FindAnyObjectByType<CinematicaGerente>(FindObjectsInactive.Include);
            if (cin != null)
            {
                var so = new SerializedObject(cin);
                var c1 = CargarClip("Assets/Audio/Cinematica/sonidoPasosExterior.mp3");
                var c2 = CargarClip("Assets/Audio/Cinematica/sonidoCampanillaPuerta.mp3");
                var c3 = CargarClip("Assets/Audio/Cinematica/sonidoBipCamara.mp3");

                if (c1 != null) { so.FindProperty("sonidoPasosExterior").objectReferenceValue = c1; asignados++; }
                if (c2 != null) { so.FindProperty("sonidoCampanillaPuerta").objectReferenceValue = c2; asignados++; }
                if (c3 != null) { so.FindProperty("sonidoBipCamara").objectReferenceValue = c3; asignados++; }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cin);
            }

            // 4. ControladorTormenta
            var tor = Object.FindAnyObjectByType<ControladorTormenta>(FindObjectsInactive.Include);
            if (tor != null)
            {
                var so = new SerializedObject(tor);
                var cLluvia = CargarClip("Assets/Audio/Ambiente/clipLluvia.mp3");
                var cTrueno1 = CargarClip("Assets/Audio/Ambiente/trueno.mp3");
                var cTrueno2 = CargarClip("Assets/Audio/Ambiente/trueno2.mp3");

                if (cLluvia != null) { so.FindProperty("clipLluvia").objectReferenceValue = cLluvia; asignados++; }

                var propTruenos = so.FindProperty("clipsTruenos");
                propTruenos.ClearArray();
                int idx = 0;
                if (cTrueno1 != null)
                {
                    propTruenos.InsertArrayElementAtIndex(idx);
                    propTruenos.GetArrayElementAtIndex(idx).objectReferenceValue = cTrueno1;
                    idx++;
                    asignados++;
                }
                if (cTrueno2 != null)
                {
                    propTruenos.InsertArrayElementAtIndex(idx);
                    propTruenos.GetArrayElementAtIndex(idx).objectReferenceValue = cTrueno2;
                    asignados++;
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(tor);
            }

            // 5. DirectorTurno
            var dir = Object.FindAnyObjectByType<DirectorTurno>(FindObjectsInactive.Include);
            if (dir != null)
            {
                var so = new SerializedObject(dir);
                var c1 = CargarClip("Assets/Audio/Terror/sonidoApagon.mp3");
                var c2 = CargarClip("Assets/Audio/Terror/sonidoRespiracionCerca.mp3");
                var c3 = CargarClip("Assets/Audio/Terror/sonidoJumpscare.mp3");

                if (c1 != null) { so.FindProperty("sonidoApagon").objectReferenceValue = c1; asignados++; }
                if (c2 != null) { so.FindProperty("sonidoRespiracionCerca").objectReferenceValue = c2; asignados++; }
                if (c3 != null) { so.FindProperty("sonidoJumpscare").objectReferenceValue = c3; asignados++; }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(dir);
            }

            // 6. CajaRegistradoraUI
            var caja = Object.FindAnyObjectByType<CajaRegistradoraUI>(FindObjectsInactive.Include);
            if (caja != null)
            {
                var so = new SerializedObject(caja);
                var c1 = CargarClip("Assets/Audio/UI/sonidoCobroExitoso.mp3");
                var c2 = CargarClip("Assets/Audio/UI/sonidoError.mp3");

                if (c1 != null) { so.FindProperty("sonidoCobroExitoso").objectReferenceValue = c1; asignados++; }
                if (c2 != null) { so.FindProperty("sonidoError").objectReferenceValue = c2; asignados++; }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(caja);
            }

            GuardarEscena(escena);
            Debug.Log($"<color=green>[Auto-Audio] ¡Se asignaron {asignados} clips de audio y se guardó la escena!</color>");
        }
    }
}
