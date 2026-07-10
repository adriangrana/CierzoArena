# Cierzo Arena

Prototipo MOBA 3D original inspirado en el genero, sin usar nombres, assets ni propiedad intelectual de otros juegos.

## Estado actual

Esta primera version incluye una base Unity validada con Unity 6.5:

- Estructura profesional de carpetas.
- Scripts modulares para unidad jugable, seleccion, movimiento por clic derecho, vida y camara isometrica.
- Escena de prueba generada en `Assets/Scenes/PrototypeArena.unity`.
- Plano simple, luz, camara isometrica, unidad aliada y objetivo enemigo de prueba.
- Movimiento por clic derecho con una alternativa ligera de movimiento directo; la siguiente iteracion sustituira esto por NavMeshAgent al introducir obstaculos y rutas.

Unity Hub y Unity 6.5.3f1 estan instalados en este PC. La compilacion de los scripts del proyecto se ha completado correctamente.

## Como abrir

1. En Unity Hub, pulsa `Add` o `Open`.
2. Selecciona esta carpeta:

   `C:\Users\adria\Documents\Codex\CierzoArena`

3. Abre el proyecto con Unity 6.5.3f1.

## Como crear y probar la escena

1. En Unity, abre `Assets/Scenes/PrototypeArena.unity` si no se muestra ya en el editor.
2. Pulsa Play.
3. Haz clic izquierdo sobre la unidad azul para seleccionarla.
4. Haz clic derecho sobre el suelo para moverla.
5. Haz clic derecho sobre el objetivo rojo para atacarlo cuando este en rango.

## Controles

- Clic izquierdo: seleccionar unidad.
- Clic derecho en suelo: mover.
- Clic derecho en unidad enemiga: perseguir y atacar.

## Siguiente objetivo recomendado

El siguiente paso natural es cambiar el movimiento directo por NavMeshAgent cuando Unity este instalado, hornear una NavMesh para el plano y anadir una torre simple con rango de ataque.
