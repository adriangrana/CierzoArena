# Cierzo Arena

Prototipo MOBA 3D original inspirado en el genero, sin usar nombres, assets ni propiedad intelectual de otros juegos.

## Estado actual

Esta primera version cubre el Milestone 1 de movimiento MOBA basico en Unity 6.5:

- Estructura profesional de carpetas.
- Scripts modulares para unidad jugable, seleccion, ordenes de movimiento y ataque, NavMesh runtime, vida y camara isometrica.
- Escena de prueba generada en `Assets/Scenes/PrototypeArena.unity`.
- Plano simple, luz, camara isometrica y unidad aliada provisional.
- Salud basica preparada para sistemas posteriores.
- Verificado manualmente en Play Mode: seleccion, clic derecho, raycast al suelo, NavMesh y movimiento completan el flujo esperado.
- Milestone 2 implementado: ataque a enemigos, persecucion hasta rango, cadencia configurable, dano, muerte y orden de parada. Pendiente de validacion manual en Play Mode.

La version real del proyecto esta en `ProjectSettings/ProjectVersion.txt`: Unity 6000.5.3f1.

## Como abrir

1. En Unity Hub, pulsa `Add` o `Open`.
2. Selecciona esta carpeta:

   `C:\Users\adria\Documents\Codex\CierzoArena`

3. Abre el proyecto con Unity 6.5.3f1.

## Como crear y probar la escena

1. Para crear la version de prueba del Milestone 2, usa `Cierzo Arena > Create Prototype Scene`.
2. Pulsa Play.
3. Haz clic izquierdo sobre la unidad azul para seleccionarla.
4. Haz clic derecho sobre el suelo para moverla o sobre el objetivo Ember para atacarlo.
5. Pulsa `S` para detener la orden actual.

## Controles

- Clic izquierdo: seleccionar unidad.
- Clic derecho en suelo: mover.
- Clic derecho en enemigo: perseguir y atacar.
- S: detener movimiento o ataque.

## Estado del milestone

El Milestone 1 esta verificado manualmente en Play Mode. El Milestone 2 esta implementado y requiere validacion manual antes de avanzar al Milestone 3.
