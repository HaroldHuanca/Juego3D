# Regla del Proyecto: Uso Obligatorio de Unity MCP (`unityMCP`)

Para este proyecto (**Juego3D**) y en **todas las conversaciones (actuales y futuras)**, se debe utilizar siempre el servidor MCP de Unity (`unityMCP`) como medio principal de comunicación, diagnóstico e interacción con Unity Editor.

## Directrices obligatorias

1. **Priorizar `unityMCP` sobre suposiciones o consultas manuales:**
   - Para inspeccionar escenas, jerarquías, GameObjects, componentes, posiciones o referencias en el Inspector, usar las herramientas y recursos de `unityMCP` (`manage_scene`, `find_gameobjects`, `manage_gameobject`, `mcpforunity://scene/gameobject/...`, `mcpforunity://editor/state`, `mcpforunity://instances`).
   - Para revisar errores, advertencias o logs de Unity, usar `read_console` en lugar de pedir al usuario que copie y pegue la consola.
2. **Verificación tras cambios en scripts o escena:**
   - Después de crear o modificar scripts `.cs`, usar `read_console` (y `mcpforunity://editor/state` si está compilando) para confirmar que no existan errores de compilación ni advertencias nuevas.
   - Al realizar cambios en la escena activa, comprobar el estado `isDirty` y guardar o advertir explícitamente si hay cambios pendientes sin guardar.
3. **Si `unityMCP` no responde o no hay instancia activa:**
   - Informar claramente al usuario que revise la ventana **MCP for Unity** en Unity Editor (puerto `8080` y sesión activa) antes de continuar con cambios dependientes del editor.
