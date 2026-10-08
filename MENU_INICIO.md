# Closing Time — Pantalla de inicio

Abre `Assets/Scenes/MenuInicio.unity` y pulsa Play. También puedes abrirla desde **Juego de terror > Abrir pantalla de inicio** en Unity.

La escena está configurada como la primera de Build Profiles / Scene List. **Jugar** carga `Assets/Scenes/SampleScene.unity` de forma asíncrona; **Opciones** se muestra como adorno y está desactivado; **Créditos** muestra los cuatro desarrolladores; **Regresar al inicio** vuelve al menú. Escape también regresa desde los créditos. **Salir** cierra la aplicación compilada y detiene Play Mode en el editor.

El menú admite ratón y navegación del Input System con teclado o mando. No tiene música ni fuentes de audio.

En el objeto **Control del menú** puedes cambiar el título, la ruta del primer escenario y la intensidad del parpadeo (0 desactiva el parpadeo del fondo). La interfaz se crea al iniciar Play Mode, se ajusta a la resolución y se destruye al cargar el escenario. El escenario original no fue modificado.

El fondo generado con IA está en `Assets/Resources/MainMenu/HorrorBackground.png`. El prompt final y la procedencia están en `Assets/Resources/MainMenu/Generation.txt`. Creepster se usa en los títulos; su licencia se incluye en `Creepster-OFL.txt`.

Validación automática en Unity, incluida en `Assets/Editor/MenuInicioValidation.cs`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\JuegoTerror' -executeMethod MenuInicioValidation.Run -logFile 'C:\JuegoTerror\Logs\MenuInicioValidation.log'
```

La validación entra en Play Mode, comprueba recursos y botones, abre los créditos, verifica los cuatro nombres, regresa, carga el escenario y verifica Salir en el editor. Genera capturas de inicio a 1920×1080 y 1024×768, y de créditos a 1920×1080, en `Logs/MenuPreview/`. Termina con `MENU_VALIDATION_OK` si todo pasa. Ejecuta este comando con el proyecto cerrado en el editor.
