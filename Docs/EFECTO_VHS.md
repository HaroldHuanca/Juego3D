# VHS de la cámara del jugador

La implementación activa utiliza el módulo CRT de **VNTG**, junto con bruma de
**URP Volumetric Light**, configurados por `EstiloAnalogico` en la cámara de
`SampleScene`. Ver [recursos, licencias y ajustes](RECURSOS_VISUALES.md).

La intensidad ambiental sigue `AmbienteSonoro.MezclaInterior`: grano más marcado
dentro de los edificios, bruma más ligera y luz frontal atenuada. Se mantienen
interferencias breves, separadas por varios segundos, fuera del centro de visión.

No se dibujan marcos de cámara, REC ni fechas. No hay pixelación, reducción de
resolución, interlazado ni persistencia de fotogramas. VNTG se aplica una vez sobre
el escenario y los brazos; la niebla solo afecta al escenario.

`VHSCamara` mantiene las referencias al ambiente y la cámara de brazos. Desactivarlo
elimina VNTG; desactivar `EstiloAnalogico` elimina también la luz frontal, el perfil
de postprocesado y la bruma del jugador. El renderer/shader VHS anterior permanece
guardado pero desactivado, para evitar acumular dos filtros.
