# Brazos en primera persona

La escena `Assets/Scenes/SampleScene.unity` usa el rig PSX First Person Arms de Drillimpact. El modelo y las dos texturas están en `Assets/Art/FirstPersonArms`. La procedencia y licencia CC0 están en `ORIGEN.md`.

## Uso

- Abre SampleScene y entra en Play Mode: solo se muestran los brazos del jugador.
- Mira un objeto con `ObjetoRecogible` y pulsa **E**. Se reproduce `grab.R`; el objeto se desactiva cuando llega el momento de contacto, siempre que continúe delante del jugador y a menos de 1,8 metros.
- Mira una puerta física con `Rigidbody` dinámico y `HingeJoint`, y pulsa **E** para empujar con `push.R`. Caminar contra las puertas también activa el gesto y conserva el empuje físico anterior.
- Mientras se ejecuta una animación no se inicia otra interacción.

## Marcar objetos recogibles

Arrastra `Assets/Art/FirstPersonArms/ObjetoRecogibleEjemplo.prefab` a la escena o añade `ObjetoRecogible` a tu propio objeto con un collider **sin Is Trigger**. Cambia `nombreObjeto` en el Inspector. El evento `alRecoger` permite conectar inventario, sonidos o progreso de objetivos. La implementación actual desactiva el objeto; no añade un sistema de inventario ni objetos sostenidos en la mano.

El componente `InteraccionJugador` de la cámara permite cambiar distancia, fuerza e indicador. `BrazosPrimeraPersona` permite ajustar `momentoContacto` entre 0 y 1 del clip de recogida.

## Apariencia y animaciones

El Animator tiene `Reposo` (`relax`), `Recoger` (`grab.R`) y `Empujar` (`push.R`), sin root motion. Los brazos vuelven a reposo al terminar cada acción. Para usar la mano izquierda, sustituye los clips tanto en los estados del Animator como en las referencias de `BrazosPrimeraPersona`.

Para usar guantes, cambia el material del SkinnedMeshRenderer del rig a `arms_gloves_01.mat`. Las texturas usan filtro Point y materiales URP Lit.

Los brazos se renderizan con una cámara Overlay URP en la capa `BrazosFPS`, con profundidad limpia y sin sombras. El escenario excluye esa capa. El rig sigue a la cámara en LateUpdate y conserva sus proporciones pese a la escala no uniforme de la cápsula original. Ajusta la posición local de `Rig de brazos` bajo `BrazosFPS` para cambiar el encuadre.

## Volver a configurar

El menú **Juego de terror → Configurar brazos en escena actual** instala el rig en una escena que contenga el componente `Camara`. Reemplaza la configuración de brazos existente y restaura los clips y material predeterminados; guarda la escena después de usarlo.

La validación de integración se puede ejecutar con Unity en modo batch mediante `-executeMethod BrazosValidation.Run` (sin `-quit`; la prueba cierra Unity al terminar). Comprueba conexiones URP, bloqueo por paredes y distancia, contacto diferido, eventos únicos, bloqueo de acciones simultáneas y respuesta física de una puerta. Los objetos de prueba no se guardan en la escena.

## Caminata de terror

La escena usa una velocidad de **2,2 m/s**, aceleración de **5 m/s²** y frenado de **8 m/s²**. Retroceder usa el 70 % de la velocidad y desplazarse solo de lado el 80 %. Puedes cambiar estos valores en el componente del jugador. `FuerzaEmpuje` conserva la fuerza de las puertas independientemente de la velocidad de caminata.

Mantén **W + Shift izquierdo o derecho** para esprintar a **4,2 m/s**. Al soltar Shift, el jugador frena suavemente hasta volver a la caminata. Shift solo no mueve al jugador; retroceder y moverse solo de lado conservan la caminata. En el componente del jugador puedes desactivar `EsprintActivado` o ajustar `VelocidadEsprint` y `AceleracionEsprint`.

En `Camara`, **Balanceo al caminar** usa ondas suaves con amplitudes de 0,012 en vertical y 0,01 en horizontal. La inclinación lateral es de solo 0,12 grados, sin cabeceo por defecto. Dos pasos recorren 2,4 metros. **Balanceo al esprintar** eleva las amplitudes a 0,018 y 0,013, con una inclinación de 0,2 grados y dos pasos por cada 3,2 metros. A mayor velocidad, los pasos se suceden más rápido. Los valores se mezclan gradualmente según la velocidad real, manteniendo la fase del paso al cambiar de ritmo. El efecto vuelve suavemente a reposo al detenerse, saltar o quedar bloqueado. Puedes desactivarlo con `BalanceoActivo` o poner la inclinación en cero.

Los brazos usan amplitudes de 0,012 al caminar y 0,018 al correr, configurables con `BalanceoCaminata` y `BalanceoEsprint`. Este balanceo adicional se atenúa durante las animaciones de recoger y empujar. La animación `relax` de reposo y el salto existente se conservan.

El CharacterController controla la posición y gravedad del jugador. El Rigidbody auxiliar es cinemático, sin gravedad, y la CapsuleCollider adicional está desactivada para evitar correcciones físicas sobre la misma cápsula. El empuje de puertas continúa aplicándose desde el controlador.
