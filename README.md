# Cierzo Arena

Prototipo MOBA 3D original inspirado en el genero, sin usar nombres, assets ni propiedad intelectual de otros juegos.

## Estado actual

Prototipo en Unity 6 (`ProjectSettings/ProjectVersion.txt`: Unity 6000.5.3f1) con el núcleo jugable local, una prueba de red autoritativa temprana y una cámara técnica para mapa grande:

- Estructura profesional de carpetas y scripts modulares (unidad jugable, selección, órdenes de movimiento y ataque, NavMesh runtime, vida, cámara isométrica).
- Escena de prueba en `Assets/Scenes/PrototypeArena.unity`.
- **M1** — Selección y movimiento (Manual Play Mode).
- **M2** — Órdenes y combate básico: ataque, persecución, rango, cadencia, daño, muerte, stop (Manual Play Mode).
- **M2.1** — Feedback visual mínimo: barras de vida world-space, números de daño, destello de impacto, ocultar al morir (Manual Play Mode).
- **M2.2** — Baseline de QA automatizada.
- **M2.3** — Datos y estado mínimo de unidad: `UnitDefinition` / `UnitDefinitionProvider`.
- **M2.4** — Frontera de órdenes: `UnitOrderCommand` desacopla intención de ejecución (16/16 tests).
- **M2.5** — Spike multijugador autoritativo (Netcode for GameObjects + Unity Transport) en `Assets/Scenes/MultiplayerSpikeArena.unity`: servidor autoritativo, órdenes de red, vida/daño/muerte replicados, el cliente no decide el daño (validado con dos instancias).
- **M3A** — Cámara técnica isométrica para mapa grande: follow/free, recentrado, zoom y límites.
- **M3B** — Spike de navegación a gran escala en `Assets/Scenes/NavigationScaleSpike.unity`: mapa amplio con dos regiones separadas por un barranco y unidas por un puente estrecho, obstáculos grandes, zona bloqueada y persecución a distancia. Valida que el NavMesh runtime escala con `LargeNavMeshBootstrap` (bake único de cobertura completa) e instrumentación de path (`NavPathProbe`).
- **M3C** — Primer greybox completo del mapa MOBA en `Assets/Scenes/MobaGreyboxArena.unity` (builder por menú `Cierzo Arena → Create MOBA Greybox Arena`): bases Azure/Ember enfrentadas, rutas central/norte/sur, río técnico diagonal, puentes/chokepoints, zonas neutrales, obstáculos y límites. Solo formas y materiales técnicos, sin arte final. Implementado, pendiente de validación manual.
- **M4.1** — Cámara MOBA real, movimiento libre: `MobaCameraController` (nuevo, independiente de la cámara técnica M3A) con paneo de teclado (WASD / flechas) y edge scrolling configurable, entrada encapsulada en `MobaCameraInput` y matemática pura testeable. Aún no integrado en escenas.
- **M4.2** — Cámara MOBA real, zoom y límites: zoom ortográfico limitado (rueda arriba = zoom in) y límites reales del mapa vía `CameraWorldBounds`. El clamp mantiene toda la región visible dentro del área permitida proyectando el viewport sobre un plano horizontal según la orientación real de la cámara, por lo que respeta zoom, aspect ratio e inclinación; si el viewport supera el mapa en un eje, ese eje se centra. Cubierto por tests deterministas (Edit Mode + Play Mode). Aún no integrado en escenas.

La version real del proyecto esta en `ProjectSettings/ProjectVersion.txt`: Unity 6000.5.3f1.

## Como abrir

1. En Unity Hub, pulsa `Add` o `Open`.
2. Selecciona esta carpeta:

   `C:\Users\adria\Documents\Codex\CierzoArena`

3. Abre el proyecto con Unity 6.5.3f1.

## Como crear y probar la escena

1. Para crear la version de prueba del Milestone 2.1, usa `Cierzo Arena > Create Prototype Scene`.
2. Pulsa Play.
3. Haz clic izquierdo sobre la unidad azul para seleccionarla.
4. Haz clic derecho sobre el suelo para moverla o sobre el objetivo Ember para atacarlo.
5. Comprueba que cada unidad tiene una barra de vida sobre ella y que cada impacto muestra dano flotante y un destello breve.
6. Pulsa `S` para detener la orden actual.

## Controles

- Clic izquierdo: seleccionar unidad.
- Clic derecho en suelo: mover.
- Clic derecho en enemigo: perseguir y atacar.
- S: detener movimiento o ataque.

### Cámara técnica (M3A)

- WASD / flechas: mover la cámara libremente (pasa a modo libre).
- Rueda del ratón: zoom con límites.
- F: recentrar en la unidad y volver al seguimiento.

## Estado del milestone

Los Milestones 1 a 2.5, M3A y M3B están completados y validados. M3C (greybox del mapa MOBA) está implementado y pendiente de validación manual. M4 (cámara MOBA real) está en progreso: M4.1 (movimiento libre) y M4.2 (zoom limitado y límites reales) completados con tests; M4.3–M4.4 pendientes. No avanzar al siguiente sub-milestone hasta recibir una nueva indicación.
