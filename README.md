# Cierzo Arena

Prototipo MOBA 3D original inspirado en el genero, sin usar nombres, assets ni propiedad intelectual de otros juegos.

## Estado actual

Esta primera version cubre el Milestone 1 de movimiento MOBA basico en Unity 6.5:

- Estructura profesional de carpetas.
- Scripts modulares para unidad jugable, seleccion, movimiento por clic derecho con NavMeshAgent, NavMesh runtime, vida y camara isometrica.
- Escena de prueba generada en `Assets/Scenes/PrototypeArena.unity`.
- Plano simple, luz, camara isometrica y unidad aliada provisional.
- Salud basica preparada para sistemas posteriores.

La version real del proyecto esta en `ProjectSettings/ProjectVersion.txt`: Unity 6000.5.3f1.

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

## Controles

- Clic izquierdo: seleccionar unidad.
- Clic derecho en suelo: mover.

## Siguiente objetivo recomendado

No avanzar al Milestone 2 hasta verificar en Play Mode que la unidad se selecciona y se mueve correctamente por la NavMesh.
