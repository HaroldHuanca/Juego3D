# Recursos visuales integrados

## VNTG — Colby-O

- Origen: https://github.com/Colby-O2/VNTG
- Revisión fijada: `9ea9070eafe833c21a02a8ce587d8ec93b2404ff`.
- Instalación: `Assets/ThirdParty/VNTG`. Solo CRT: cuatro scripts y shader, sin materiales PSX, demos ni vídeos.
- Licencia y avisos originales: `LICENSE.md`, `THIRD_PARTY_NOTICE.md` en esa carpeta.

Adaptaciones locales: ejecución una sola vez en la última cámara de la composición;
componente de volumen inactivo por defecto fuera del perfil del jugador; resolución
real del render target y omisión de la rejilla cuando Screen Settings está desactivado;
color neutro al desactivar el tinte de sombras; separación RGB limitada a 1.5 píxeles
por canal y reducida en el centro; ruido reducido en sombras; interferencias limitadas
a la parte superior/inferior durante 0.28 segundos, cada 5–11 segundos aproximadamente;
destino de cámara conservado al componer.

## URP Volumetric Light — Cristian Qiu Félez

- Origen: https://github.com/CristianQiu/Unity-URP-Volumetric-Light
- Versión: 0.5.91, revisión `354ebc5b6ed3c2c40dc9797c813b56d5da213c4e`.
- Instalación: `Assets/ThirdParty/URPVolumetricFog`, con Runtime, Editor y Shaders.
- Licencia MIT original incluida en `LICENSE.md`.

Adaptaciones locales: niebla solo en la cámara base, independientemente de si esta
ejecuta el postprocesado; destino de color conservado para la cámara de brazos.
Así la niebla se aplica al escenario y el postprocesado final incluye ambos.

## Configuración del juego

`EstiloAnalogico`, en `Camara`, crea un perfil exclusivo para el jugador en la capa
30, libre en este proyecto. Conserva y restaura la configuración de las cámaras al
desactivarse. No modifica los perfiles de volumen existentes ni el menú.

- VNTG a resolución nativa, sin interlazado, límite de FPS, curvatura, marcos,
  subpíxeles ni pixelación. El shader anterior queda desactivado en ambos renderers.
- Grano exterior 0.065/interior 0.095; separación de color contenida; líneas finas.
- Bloom 0.12, Neutral, saturación -8, exposición +0.25, viñeta suave 0.16.
- Niebla exterior 0.035/interior 0.008, alcance 55 m, 32 pasos y una iteración de blur.
- Luz frontal cálida suave de alcance 20 m, menor intensidad dentro del edificio.
- Solo la luz frontal y los dos focos de poste más cercanos al inicio aportan luz volumétrica adicional.
- PC y Mobile usan Forward+ para las luces adicionales. No se reduce render scale.
- La transición de grano, bruma y luz utiliza `AmbienteSonoro.MezclaInterior`.

La iluminación ambiental de `SampleScene` usa un gradiente frío conservando relleno
en las sombras. Los materiales, texturas y modelos del escenario siguen siendo los
del juego; las referencias orientan el tratamiento de imagen y luz.

Para ajustar visibilidad, modificar `Grano Exterior/Interior`, `Niebla Exterior/Interior`,
`Intensidad Linterna` y `Pasos Niebla` en `Camara > EstiloAnalogico`.
La niebla volumétrica tiene coste de GPU: reducir primero Pasos Niebla a 16 si hace falta.
