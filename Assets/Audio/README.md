# Audio del juego

- `Jugador/Pasos/KenneyRPG`: footstep00–footstep09, usados en orden y repetidos al caminar.
- `Jugador`: futuros sonidos de respiración, ropa, daño, etc.
- `Interacciones`: puertas, cajones, interruptores y objetos.
- `Interacciones/Puertas/KenneyRPG`: doorOpen_1–2 y doorClose_1–4.
- `Ambiente`: ambientes continuos y sonidos del escenario.
- `Musica`: música de exploración, tensión y persecución.
- `Enemigos`: voces, movimiento y ataques.
- `UI`: inventario, menús y avisos.
- `Licencias`: licencias originales de cada paquete.

## Ambiente interior y exterior

`Ambiente sonoro` en `SampleScene` tiene `AmbienteSonoro`, con la cámara del jugador
como oyente. Dos volúmenes en coordenadas del mundo delimitan el hangar y el
minimarket (incluidas sus habitaciones). Se visualizan al seleccionar el objeto.

- `Ambiente/Interior/VHS_Loop.wav`: VHS en interiores; volumen 0.25.
- `Ambiente/Exterior/Grillos_Noche_Loop.wav`: grillos nocturnos fuera; volumen 0.45.
- Transición suave de 1.5 s, con una franja espacial de 1 m en los límites.
- Los dos clips siguen avanzando al cruzar, sin reiniciarse ni acumular fuentes.
- Los bucles preparados mezclan final y principio durante 4 s, sin un corte seco.
- Importación estéreo, Vorbis de calidad 0.85 y streaming para limitar memoria.

Los originales del usuario permanecen intactos en Descargas. Se retiró un segundo
de cada extremo, se equilibró la ganancia y se mezcló la unión con potencia constante.
El script `Tools/preparar_ambientes.py` permite regenerar los WAV a partir de ellos.
Los créditos están en `Licencias/Freesound_Ambientes.txt` y en el menú del juego.
Si cambia la distribución de los edificios, ajusta `Interiores` en el Inspector.

En `SampleScene`, selecciona `Camara` y el componente `PasosJugador` para ajustar
el volumen (0.65 inicialmente), cambiar la lista o asignar un grupo de AudioMixer.
Los diez clips están referenciados directamente, por lo que se incluyen en el build.

Cada apoyo del ciclo de `Camara` dispara un sonido. Hay dos apoyos por ciclo,
compartiendo la fase del balanceo de cámara y brazos. El ritmo depende de la
distancia real recorrida sobre el suelo y de la transición caminar/correr.
La secuencia es 00, 01, …, 09, 00, …; al detenerse no se generan nuevos pasos.
El clip del último apoyo termina de sonar de forma natural.

`AudioSource.loop` está desactivado deliberadamente: repetir un archivo por su
duración desincronizaría el audio. La repetición la controla el ciclo de pasos.
El sonido del propio jugador se reproduce en 2D. No genera pasos al permanecer
quieto, empujar una pared sin avanzar, estar en el aire o pausar el tiempo.
Desactivar el balanceo visual con `BalanceoActivo` no desactiva el sonido.

Para incorporar otro paquete, crea una subcarpeta en su categoría y guarda su
licencia en `Licencias`. Kenney RPG Audio usa CC0; se incluye la licencia original.

Validación: `Tools > Audio > Validar pasos (escena aislada)` crea una escena de
prueba temporal, simula caminar/correr/saltar/bloqueo/pausa y comprueba la secuencia.
Requiere salir de Play Mode y guardar la escena actual antes de ejecutarla.

## Puertas con interacción E

Las seis puertas de `SampleScene` tienen `CierrePuerta` y `AudioPuerta`.
Acercarse o caminar contra ellas no las abre. Al mirar una puerta cerrada dentro
del alcance aparece **E · Abrir puerta**. Una pulsación abre la hoja por completo
(90 grados; 125 en las puertas marrones `Door_02` y `Door_04` para dejar libre
el marco), hacia el lado contrario al jugador. Permanece abierta 3 segundos y
se cierra automáticamente, sin balanceo. Mantener E no repite la acción.

Si el jugador ocupa el hueco o el recorrido de la hoja, la puerta espera abierta.
Al salir, comienza de nuevo la espera de 3 segundos. Si entra cuando ya se está
cerrando, la puerta vuelve a abrirse sin repetir el sonido de apertura. La zona
se calcula con los colliders originales, sus escalas y el pivote, y funciona
desde ambos lados. `Margen Seguridad` añade 0.25 m al volumen de detección.

`CierrePuerta` permite ajustar `Angulo Apertura`, `Duracion Movimiento` (0.9 s)
y `Segundos Abierta` (3 s). El tiempo de espera comienza al terminar la apertura.
La animación respeta la pausa y los límites de la bisagra. El Rigidbody es
cinemático: la puerta no depende de impulsos ni del muelle para completar el giro.

Cada puerta elige un clip aleatorio de apertura y otro de cierre, evitando
repeticiones inmediatas. `AudioPuerta` sigue el ángulo de la animación; el cierre
suena al llegar a la posición cerrada. Sonido 3D, volumen inicial 0.8 y alcance
máximo de 18 metros. Los seis clips están en `Interacciones/Puertas/KenneyRPG`.

Para una puerta nueva, colócala cerrada en la escena, configura el eje y anclaje
de su `HingeJoint`, y añade `CierrePuerta` y `AudioPuerta`. Asigna los dos clips
a `Abrir` y los cuatro a `Cerrar`. La interacción del jugador la detectará
mediante el Collider y Rigidbody de la hoja. No actives `Loop` en el audio.
