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
- **M4.1** — Cámara MOBA real, movimiento libre: `MobaCameraController` (nuevo, independiente de la cámara técnica M3A) con paneo de teclado (WASD / flechas) y edge scrolling configurable, entrada encapsulada en `MobaCameraInput` y matemática pura testeable.
- **M4.2** — Cámara MOBA real, zoom y límites: zoom ortográfico limitado (rueda arriba = zoom in) y límites reales del mapa vía `CameraWorldBounds`. El clamp mantiene toda la región visible dentro del área permitida proyectando el viewport sobre un plano horizontal según la orientación real de la cámara, por lo que respeta zoom, aspect ratio e inclinación; si el viewport supera el mapa en un eje, ese eje se centra. Cubierto por tests deterministas (Edit Mode + Play Mode).
- **M4.3** — Cámara MOBA real, seguimiento del héroe local: la cámara empieza siguiendo al héroe y Space recentra. Un `LocalHeroProvider` desacoplado (en Runtime, sin conocer Netcode) publica el héroe local; `NetworkUnitController` lo registra solo cuando es owner (nunca una unidad remota), tolerando spawn tardío y despawn, sin búsquedas globales por frame ni tráfico de red de cámara. El input manual real pasa la cámara a modo libre.
- **M4.4** — Cámara MOBA real, integración y cierre: la cámara MOBA sustituye a la cámara técnica M3A en la greybox principal (`MobaGreyboxArena`), arrancando encuadrada y siguiendo a Azure (registrado vía `SceneLocalHeroRegistrar`), con bounds reales a ±86, zoom 12–55 y un `followPlaneOffset` que centra al héroe compensando la inclinación. La escena de red (`MultiplayerSpikeArena`) también usa la cámara MOBA con su propio `LocalHeroProvider`, de modo que host y cliente siguen cada uno su unidad owner. `IsometricCameraRig` permanece disponible para las escenas spike y sus tests. Implementado, pendiente de validación manual (local + host/cliente) y multi-resolución.
- **M5** — Estructuras, torres y victoria: las torres detectan y atacan unidades enemigas con cadencia configurable. Por cada línea, solo la torre exterior puede dañarse al principio; desbloquea interior y luego puerta. El núcleo se vuelve vulnerable al caer las tres puertas. En red, el servidor decide objetivos, daño y ganador; los clientes solo reciben el estado replicado.
- **M6** — Modelo avanzado de ataque: `BasicAttack` usa la secuencia Idle → Approaching → Windup → Backswing. Azure prueba melee (daño en el attack point); Ember y torres usan ranged (proyectil visible al attack point y daño únicamente al impacto). Una orden de mover o de atacar a otro objetivo cancela el windup, mientras que el backswing se puede cancelar sin alterar la cadencia. El servidor simula ataques e impactos; Netcode replica solo la visual del proyectil.

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
5. En melee, comprueba que el daño aparece tras un breve windup; en ranged, que el proyectil aparece tras el windup y el daño aparece solo al impactar.
6. Durante el windup, da una orden de movimiento: no debe haber impacto ni proyectil. Tras liberar un proyectil, cambiar de orden no debe detenerlo ni cambiar su objetivo.
7. Pulsa `S` para detener la orden actual.
8. En `MobaGreyboxArena`, entra en el rango de una torre enemiga: debe hacer windup, lanzar un proyectil y mantenerse inmóvil. Destruir un núcleo muestra el ganador y bloquea el gameplay restante, incluidos ataques y proyectiles pendientes.

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

Los Milestones 1 a 6 están implementados. M6 sustituye el daño instantáneo por una línea temporal de ataque, proyectiles autoritativos y cancelación coherente. Para regenerar las escenas tras cambios de builders, usa los menús `Cierzo Arena > Create MOBA Greybox Arena` y `Cierzo Arena > Create Multiplayer Spike Scene`.
