# VHS sutil en primera persona

La cámara `Camara` de `SampleScene` tiene `VHSCamara`. Su intensidad exterior es
0.12 y la interior 0.30. Utiliza directamente `AmbienteSonoro.MezclaInterior`,
por lo que acompaña el mismo fundido del VHS sonoro al entrar y salir de los edificios.
No analiza los picos del audio ni genera destellos al producirse un glitch sonoro.

El shader añade grano fino, una separación de color inferior a un píxel,
deriva horizontal suave y líneas de intensidad muy baja. Atenúa la distorsión
cerca del centro y el grano en sombras; conserva resolución, exposición y encuadre.

`VHSRendererFeature` está configurado en los renderers PC y Mobile. Solo se aplica
a las cámaras registradas por `VHSCamara`, una vez al final de la composición del
escenario y los brazos. El menú, Scene View y cámaras ajenas quedan sin este filtro.
Funciona con Render Graph de URP 17.5, sin activar el resto del postprocesado.

Para ajustarlo: selecciona `Camara > VHSCamara` y modifica `Intensidad Interior`
o `Intensidad Exterior`. Desactivar el componente elimina el efecto por completo.
El shader es propio del proyecto (`Assets/Shaders/VHSSutil.shader`).
