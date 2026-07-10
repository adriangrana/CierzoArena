# CierzoArena Roadmap

Objetivo final asumido: MOBA 3D original, competitivo, eventualmente 5v5 online, Free to Play, publicable en Steam, sin copiar IP de ningún MOBA existente.

## Current Project Status

| Milestone | Status | Validation |
|---|---|---|
| M1 — Selección y movimiento | Completed | Manual Play Mode |
| M2 — Órdenes y combate básico | Completed | Manual Play Mode |
| M2.1 — Feedback visual mínimo | Completed | Manual Play Mode |
| M2.2 — Baseline de QA | Completed | 9/9 automated tests |
| M2.3 — Datos y estado mínimo de unidad | Completed | Manual + automated tests |
| M2.4 — Frontera de órdenes y simulación | Completed | 16/16 automated tests |
| M2.5 — Spike multijugador autoritativo temprano | Completed | Manual (2 instancias) + tests |
| M3A — Cámara técnica para mapa grande | Completed | Manual Play Mode + tests |
| M3B — Navegación a gran escala | Completed | Manual Play Mode + tests |
| M3C — Greybox completo del mapa MOBA | Implemented | Pending manual validation |
| M4 — Cámara MOBA real | Implemented | Automated tests (M4.1–M4.4) + pending manual validation |

## Roadmap Principles

- Compilation is not validation.
- Interactive milestones require Play Mode verification.
- Available automated tests must be run, and existing automated tests must remain green.
- Networking authority is validated early, not late.
- Avoid mass content before core systems stabilize.
- Architecture changes must preserve existing tests.

## Leyenda

- ✅ Completado y validado manualmente
- 🔵 Completado con validación automatizada
- 🟡 Próximo / planeado
- ⬜ Futuro / planeado
- 🚪 Gate: no avanzar sin validación

---

## FASE I — Núcleo jugable local

### Milestone 1 — Selección y movimiento

Status: ✅ Completado y validado (Manual Play Mode)

- [x] Unidad Azure seleccionable
- [x] Clic izquierdo
- [x] Movimiento con clic derecho
- [x] NavMesh
- [x] Pathfinding
- [x] Cámara isométrica básica
- [x] Parada y movimiento verificables
- [x] Play Mode validado

Resultado: una unidad controlable.

### Milestone 2 — Órdenes y combate básico

Status: ✅ Completado y validado (Manual Play Mode)

- [x] Orden de movimiento (Move)
- [x] Orden de ataque (Attack)
- [x] Persecución dinámica de objetivo móvil
- [x] Entrada en rango
- [x] Cadencia de ataque
- [x] Daño
- [x] Vida
- [x] Muerte
- [x] Stop para detener
- [x] Movimiento cancela ataque
- [x] Protección contra fuego amigo (aliados)

Resultado: una unidad se comporta ya como una unidad básica de MOBA.

### Milestone 2.1 — Feedback visual mínimo

Status: ✅ Completado y validado (Manual Play Mode)

- [x] Barra de vida world-space (Azure y Ember)
- [x] Daño flotante
- [x] Destello de impacto
- [x] Barra/unidad desaparece al morir
- [x] Validación manual

Resultado: el combate deja de ser invisible.

### Milestone 2.2 — Baseline de QA

Status: 🔵 Completado / Validación automatizada (9/9 passing)

Distribución final de la suite:

- [x] Edit Mode: 2 tests
- [x] Play Mode: 7 tests
- [x] Total: 9 tests
- [x] Resultado manual confirmado: Edit Mode 2/2, Play Mode 7/7 → 9/9 passing

Notas:

- Los tests de componentes `MonoBehaviour` deben alinearse con el ciclo de vida real de Unity: el comportamiento en runtime se valida en Play Mode; Edit Mode solo cubre validaciones puras (p. ej. `OnValidate`).
- Este milestone establece el baseline de QA. La ampliación de cobertura, CI y build verification se aborda más adelante (ver Milestone 45).

> 🚪 Gate: cualquier cambio de arquitectura debe mantener estos 9 tests en verde.

### Milestone 2.3 — Datos y estado mínimo de unidad

Status: ✅ Completado y validado (Manual + tests automatizados)

Objetivo: separar configuración reutilizable de estado mutable, sin construir todavía un sistema universal de estadísticas.

- [x] Configuración reutilizable separada del estado en runtime
- [x] Distinguir configuración (inmutable) de estado mutable
- [x] Centralizar max health
- [x] Centralizar movement speed
- [x] Centralizar attack damage
- [x] Centralizar attack range
- [x] Centralizar attacks per second
- [x] Evitar un sistema universal de estadísticas prematuro

Nota: es el precursor mínimo del Milestone 5 (sistema formal completo). No construir la catedral de abstracciones todavía.

> 🚪 Gate: los valores de balance siguen siendo visibles/ajustables en el inspector y los 9 tests existentes siguen en verde.

### Milestone 2.4 — Frontera de órdenes y simulación

Status: ✅ Completado y validado (16/16 tests automatizados)

Objetivo: desacoplar la intención (jugador / IA / red) de la ejecución de gameplay.

- [x] Separar intención de ejecución de gameplay
- [x] Comando Move
- [x] Comando Attack
- [x] Comando Stop
- [x] El input no controla directamente la lógica de gameplay
- [x] Preparado para futura autoridad de servidor
- [x] Sin una arquitectura excesivamente compleja

Nota: precursor del Milestone 7 (sistema de órdenes completo).

> 🚪 Gate: las órdenes actuales (Move / Attack / Stop) siguen funcionando y los 9 tests existentes siguen en verde.

---

## FASE II — Spike de red autoritativa temprana

Crítica de diseño asumida: no podemos esperar a tener decenas de héroes para introducir red. La autoridad de servidor se valida pronto, justo después de tener órdenes y datos desacoplados.

### Milestone 2.5 — Spike multijugador autoritativo temprano

Status: ✅ Completado y validado (Manual con 2 instancias + tests)

Prueba de networking adelantada (anteriormente estaba demasiado tarde en el plan). Implementada con Netcode for GameObjects + Unity Transport, unidades como prefabs de red spawneadas por el servidor con ownership atómico.

Alcance:

- [x] Servidor / host
- [x] Dos clientes
- [x] Órdenes de movimiento
- [x] Orden de ataque
- [x] Vida
- [x] Daño
- [x] Muerte
- [x] Servidor autoritativo
- [x] El cliente no decide el daño

Fuera de alcance (todavía):

- Matchmaking
- Steam
- 5v5
- Reconnect
- Cuentas

> 🚪 Gate crítico: demostrar que la arquitectura puede vivir en red antes de crear habilidades complejas, creeps completos, torres completas, economía, inventario o producción masiva de contenido.

---

## FASE III — Campo de batalla técnico

Solo después de validar red temprana se aborda el mapa grande, y primero por sus fundamentos técnicos: cámara, navegación y, por último, el greybox.

### Milestone 3A — Cámara técnica para mapa grande

Status: ✅ Completado y validado (Manual Play Mode + tests)

Objetivo: cámara técnica para explorar un mapa mayor. No es todavía la cámara final (esa es el Milestone 4).

- [x] Seguimiento (follow)
- [x] Movimiento libre de cámara (free movement)
- [x] Recentrado (recenter)
- [x] Zoom básico
- [x] Límites
- [x] Navegación/exploración sobre un mapa grande

> 🚪 Gate: recorrer con comodidad un mapa mayor antes de abordar la navegación a gran escala (M3B).

### Milestone 3B — Navegación a gran escala

Status: ✅ Completado y validado (Manual Play Mode + tests)

Dependencia: requiere la cámara técnica de M3A para poder inspeccionar el mapa grande.

Objetivo: validar NavMesh sobre un mapa grande antes de construir el greybox.

- [x] Prueba de navegación en un mapa grande
- [x] Rutas largas
- [x] Obstáculos
- [x] Límites
- [x] Múltiples regiones
- [x] Evaluar estrategia runtime vs preconstruida (prebaked) vs híbrida
- [x] No asumir que el `RuntimeNavMesh` actual (Bounds alrededor de la unidad) escala a un mapa grande

Resultado: escena técnica `NavigationScaleSpike` (builder por menú) con dos regiones separadas por un barranco y unidas por un puente estrecho, obstáculos grandes, zona bloqueada y marcadores de destino. El `RuntimeNavMesh` actual escala al spike usando `LargeNavMeshBootstrap` (bake único de cobertura completa en lugar del parche local por unidad); instrumentación de path vía `NavPathProbe`.

> 🚪 Gate: pathfinding fiable de extremo a extremo del mapa.

### Milestone 3C — Greybox completo del mapa MOBA

Status: 🟡 Implementado / pendiente de validación manual

Dependencia: requiere M3A (cámara técnica) y M3B (navegación a gran escala) resueltos y validados. No iniciar el greybox hasta que cámara y navegación funcionen sobre un mapa grande.

- [x] Nueva escena separada
- [x] Base Azure
- [x] Base Ember
- [x] Top
- [x] Mid
- [x] Bot
- [x] Jungla Azure
- [x] Jungla Ember
- [x] Corredor central / río / cauce
- [x] Rutas alternativas
- [x] Chokepoints
- [x] Obstáculos
- [x] Marcadores de torres
- [x] Marcadores de campamentos
- [x] Spawn points
- [x] Límites
- [x] NavMesh completo
- [x] Sin materiales magenta

Resultado: escena técnica `MobaGreyboxArena` (builder por menú `Cierzo Arena → Create MOBA Greybox Arena`) con topología MOBA clásica: bases Azure (esquina inferior-izquierda, SW) y Ember (superior-derecha, NE) en esquinas opuestas, mid diagonal directa entre bases, y líneas top/bot en "L" pegadas a los bordes (top por el borde oeste y luego el norte; bot por el sur y luego el este). El río técnico corre por la anti-diagonal partiendo el cuadrado en dos mitades, con un puente/chokepoint por línea (top en la esquina NW, mid al centro, bot en la esquina SE). Cada línea lleva tres marcadores de torre por equipo escalonados a lo largo de su recorrido (puerta, interior, exterior), y cada base define tres spawn points, uno por línea. Campamentos neutrales en las junglas laterales, obstáculos en los triángulos de jungla entre líneas, límites cuadrados. Cubierta por `LargeNavMeshBootstrap` y cámara M3A. Los marcadores son solo visuales (no bloquean navegación). Pendiente de validación manual del propietario.

> 🚪 Gate: recorrer manualmente todo el mapa.

---

## FASE IV — Cámara final y datos formales

### Milestone 4 — Cámara MOBA real

Status: 🟡 Implementado / pendiente de validación manual

Aquí sí dejamos atrás la cámara técnica del Milestone 3A. Se abordó en sub-fases verificables para no romper sistemas existentes:

- **M4.1 — Movimiento libre** ✅ (validado con tests automatizados)
- **M4.2 — Zoom limitado y límites reales del mapa** ✅ (validado con tests automatizados)
- **M4.3 — Seguimiento y recentrado del héroe local** ✅ (validado con tests automatizados)
- **M4.4 — Integración en escena y cierre** 🟡 (implementado; pendiente de validación manual y multi-resolución)

Checklist global:

- [x] Seguimiento del héroe (M4.3)
- [x] Cámara libre (M4.1)
- [x] Edge scrolling (M4.1)
- [x] Movimiento con teclado (M4.1)
- [x] Tecla para recentrar héroe (M4.3, Space)
- [x] Zoom limitado (M4.2)
- [x] Límites del mapa (M4.2)
- [x] Velocidad configurable (M4.1, ampliada en M4.2)
- [x] Comportamiento consistente en distintas resoluciones (M4.2 base / M4.4 integración)

M4.1 introduce `MobaCameraController` (nuevo, independiente de `IsometricCameraRig`) con paneo de teclado (ejes Horizontal/Vertical) y edge scrolling configurable, entrada encapsulada en `MobaCameraInput` (el controlador no toca `Input`/`Screen`/`Application` directamente) y matemática pura testeable.

M4.2 añade zoom ortográfico limitado (rueda arriba = zoom in, reduciendo `orthographicSize`) y límites reales del mapa mediante `CameraWorldBounds` (nuevo componente de área de juego con gizmos). El clamp mantiene toda la región visible dentro de los bounds calculando la proyección del viewport sobre un plano horizontal a partir de la orientación real de la cámara (intersección de las cuatro esquinas, sin `55` codificado), por lo que respeta zoom, aspect ratio e inclinación. Los offsets visibles son asimétricos (la cámara inclinada ve más hacia delante que hacia atrás) y, cuando el viewport supera el mapa en un eje, ese eje se centra de forma determinista.

M4.3 añade seguimiento del héroe local mediante un `LocalHeroProvider` desacoplado (en Runtime, sin conocer Netcode): el controlador empieza en `FollowHero`, sigue al héroe en `LateUpdate`, pasa a `Free` con input manual real, y Space recentra. `NetworkUnitController` registra su unidad como héroe local solo cuando es owner (nunca una unidad remota), tolerando spawn tardío y despawn sin búsquedas globales por frame ni tráfico de red de cámara.

M4.4 integra la cámara MOBA en la greybox principal (`MobaGreyboxArena`) sustituyendo a la cámara técnica M3A: la cámara empieza encuadrada y siguiendo a Azure (registrado como héroe local vía `SceneLocalHeroRegistrar`), con bounds reales a ±86 (cara exterior de los muros de navegación, centro ±84 + grosor 4), zoom 12–55, y un offset de encuadre `followPlaneOffset` que centra al héroe compensando la inclinación (independiente del zoom). La escena de red (`MultiplayerSpikeArena`) también pasa a la cámara MOBA con su propio `LocalHeroProvider`, de modo que host y cliente siguen cada uno su unidad owner. `IsometricCameraRig` permanece en el proyecto para las escenas spike y sus tests. Pendiente de validación manual (local + host/cliente) y de comprobación en varias resoluciones.

Resultado: explorar el mapa como un MOBA real.

### Milestone 5 — Sistema formal de unidades y estadísticas

Status: ⬜ Futuro

Antes de crear 100 cosas, necesitamos datos coherentes. Amplía el Milestone 2.3 hacia el sistema completo.

- [ ] Vida máxima
- [ ] Regeneración
- [ ] Maná
- [ ] Regeneración de maná
- [ ] Daño mínimo/máximo
- [ ] Armadura
- [ ] Resistencia mágica
- [ ] Velocidad de movimiento
- [ ] Rango de ataque
- [ ] Velocidad de ataque
- [ ] Nivel
- [ ] Equipo
- [ ] Estado vivo/muerto

Separar:

```text
Datos
↓
Simulación
↓
Presentación
```

Probablemente con assets de datos configurables, pero sin crear una catedral de abstracciones.

---

## FASE V — Combate verdaderamente MOBA

### Milestone 6 — Modelo avanzado de ataque

Status: ⬜ Futuro

El ataque actual sirve de prototipo. Aquí se convierte en combate serio.

- [ ] Ataques melee
- [ ] Ataques ranged
- [ ] Proyectiles
- [ ] Attack point / windup
- [ ] Impacto
- [ ] Backswing
- [ ] Cancelación
- [ ] Animación provisional
- [ ] Objetivo perdido
- [ ] Objetivo muerto
- [ ] Cambio de objetivo
- [ ] Distancia coherente

> 🚪 Gate: el combate debe sentirse responsivo antes de añadir héroes.

### Milestone 7 — Sistema de órdenes completo

Status: ⬜ Futuro

Amplía la frontera de órdenes del Milestone 2.4 hacia el conjunto completo.

- [ ] Move
- [ ] Attack
- [ ] Attack-Move
- [ ] Stop
- [ ] Hold Position
- [ ] Cast
- [ ] Follow
- [ ] Cancel

No necesariamente todas de golpe, pero este sería el milestone donde formalizamos el sistema.

### Milestone 8 — Framework de habilidades

Status: ⬜ Futuro

Este es uno de los milestones más importantes del juego.

Tipos básicos:

- [ ] Target Unit
- [ ] Target Point
- [ ] No Target
- [ ] Passive
- [ ] Toggle
- [ ] Channeling

Sistemas:

- [ ] Cooldown
- [ ] Coste de maná
- [ ] Rango
- [ ] Área
- [ ] Cast time
- [ ] Interrupción
- [ ] Efectos
- [ ] Buff
- [ ] Debuff
- [ ] Stun
- [ ] Slow
- [ ] Silence
- [ ] Damage over time

> 🚪 Gate: crear varias habilidades distintas sin reprogramar el motor cada vez.

### Milestone 9 — Primer héroe completo

Status: ⬜ Futuro

Aquí aparece el primer héroe verdadero de CierzoArena.

- [ ] Nombre original
- [ ] Rol
- [ ] Estadísticas
- [ ] Q
- [ ] W
- [ ] E
- [ ] R
- [ ] Pasiva si corresponde
- [ ] Iconos provisionales
- [ ] Retrato provisional
- [ ] Modelo placeholder
- [ ] Feedback visual
- [ ] Muerte
- [ ] Respawn

Resultado: primer personaje jugable completo.

---

## FASE VI — Primer MOBA real

### Milestone 10 — Creeps

Status: ⬜ Futuro

- [ ] Creeps Azure
- [ ] Creeps Ember
- [ ] Spawn por oleadas
- [ ] Ruta de línea
- [ ] Aggro
- [ ] Selección de objetivos
- [ ] Melee creeps
- [ ] Ranged creeps
- [ ] Muerte
- [ ] Oro
- [ ] Experiencia

Primero una sola línea.

### Milestone 11 — Una línea funcional completa

Status: ⬜ Futuro

Este será otro gran momento del proyecto.

```text
Base Azure
↓
Torre
↓
Creeps
↓
Combate
↓
Torre
↓
Base Ember
```

Checklist:

- [ ] Una lane completa
- [ ] Oleadas enfrentándose
- [ ] Creeps atacándose
- [ ] Héroe participando
- [ ] Last hit
- [ ] XP
- [ ] Torres provisionales

> 🚪 Gate: jugar varios minutos sin romper el flujo.

### Milestone 12 — Torres

Status: ⬜ Futuro

- [ ] Selección de objetivo
- [ ] Rango
- [ ] Ataque
- [ ] Proyectiles
- [ ] Daño
- [ ] Cambio de aggro
- [ ] Prioridades
- [ ] Vida
- [ ] Destrucción
- [ ] Equipo
- [ ] Feedback

### Milestone 13 — Bases y condición de victoria

Status: ⬜ Futuro

- [ ] Estructura principal Azure
- [ ] Estructura principal Ember
- [ ] Invulnerabilidad si aplica
- [ ] Destrucción
- [ ] Victoria
- [ ] Derrota
- [ ] Pantalla final
- [ ] Reinicio de partida

Resultado: por primera vez CierzoArena tiene una partida que puede terminar.

---

## FASE VII — Progresión dentro de la partida

### Milestone 14 — Experiencia y niveles

Status: ⬜ Futuro

- [ ] XP
- [ ] Radio de experiencia
- [ ] Subida de nivel
- [ ] Estadísticas por nivel
- [ ] Puntos de habilidad
- [ ] Nivel máximo
- [ ] Feedback visual

### Milestone 15 — Oro y last hit

Status: ⬜ Futuro

- [ ] Recompensa por unidad
- [ ] Last hit
- [ ] Kill de héroe
- [ ] Assist
- [ ] Oro pasivo si decidimos usarlo
- [ ] Feedback
- [ ] Contabilidad robusta

### Milestone 16 — Inventario

Status: ⬜ Futuro

- [ ] Slots
- [ ] Añadir objetos
- [ ] Quitar
- [ ] Stack
- [ ] Consumibles
- [ ] Activos
- [ ] Pasivos
- [ ] Cooldowns
- [ ] Drag and drop

### Milestone 17 — Tienda y primeros objetos

Status: ⬜ Futuro

Primer catálogo pequeño:

- [ ] 15–25 objetos
- [ ] Precio
- [ ] Compra
- [ ] Venta
- [ ] Recetas si decidimos usarlas
- [ ] Estadísticas
- [ ] Consumibles
- [ ] Objetos activos

No 200 objetos. Todavía no.

---

## FASE VIII — Mapa completo

### Milestone 18 — Tres líneas funcionales

Status: ⬜ Futuro

- [ ] Top completa
- [ ] Mid completa
- [ ] Bot completa
- [ ] Oleadas
- [ ] Torres
- [ ] Navegación
- [ ] Spawns
- [ ] Equilibrio básico de distancias

### Milestone 19 — Jungla

Status: ⬜ Futuro

- [ ] Campamentos
- [ ] Spawns
- [ ] Neutrales
- [ ] IA
- [ ] Recompensas
- [ ] Respawn
- [ ] Leash
- [ ] Reset
- [ ] Campamentos distintos

### Milestone 20 — Objetivo mayor del mapa

Status: ⬜ Futuro

Nuestro equivalente original, no una copia de ningún objetivo neutral existente.

- [ ] Criatura/objetivo propio
- [ ] Zona específica
- [ ] Mecánica especial
- [ ] Recompensa
- [ ] Respawn
- [ ] Riesgo estratégico

Aquí podemos crear algo con identidad CierzoArena.

---

## FASE IX — Visión y estrategia

### Milestone 21 — Fog of War

Status: ⬜ Futuro

- [ ] Visión por equipo
- [ ] Radio de héroes
- [ ] Visión de creeps
- [ ] Torres
- [ ] Obstáculos
- [ ] Ocultamiento
- [ ] Rendimiento

### Milestone 22 — Wards y visión avanzada

Status: ⬜ Futuro

Solo si encaja con nuestro diseño.

- [ ] Ward
- [ ] Duración
- [ ] Radio
- [ ] Revelado
- [ ] Contra-visión
- [ ] UI

---

## FASE X — Networking a escala

El spike autoritativo temprano (Milestone 2.5) ya demostró que la arquitectura puede vivir en red. Aquí escalamos ese trabajo hacia partidas reales.

### Milestone 23 — Partida multijugador pequeña

Status: ⬜ Futuro

- [ ] Lobby
- [ ] 1v1
- [ ] 2v2 experimental
- [ ] Join
- [ ] Leave
- [ ] Spawn
- [ ] Match start
- [ ] Match end

### Milestone 24 — Reconnect y robustez

Status: ⬜ Futuro

- [ ] Desconexión
- [ ] Reconexión
- [ ] Timeout
- [ ] Jugador abandona
- [ ] Estado recuperado
- [ ] Servidor no confía en cliente

### Milestone 25 — 5v5 técnico

Status: ⬜ Futuro

Todavía sin prometer que sea divertido.

- [ ] 10 jugadores
- [ ] Rendimiento
- [ ] Sincronización
- [ ] Servidor
- [ ] Latencia
- [ ] Estrés
- [ ] Logs

> 🚪 Gate: partida completa de 10 clientes técnicamente estable.

---

## FASE XI — Interfaz completa

### Milestone 26 — HUD final funcional

Status: ⬜ Futuro

- [ ] Retrato
- [ ] HP
- [ ] Mana
- [ ] Q/W/E/R
- [ ] Cooldowns
- [ ] Inventario
- [ ] Oro
- [ ] Nivel
- [ ] Minimap
- [ ] Kill feed
- [ ] Score
- [ ] Tiempo

Con identidad visual propia de CierzoArena.

### Milestone 27 — Menús

Status: ⬜ Futuro

- [ ] Pantalla principal
- [ ] Play
- [ ] Settings
- [ ] Heroes
- [ ] Inventory cosmético
- [ ] Perfil
- [ ] Salir
- [ ] Reconectar

### Milestone 28 — Minimap

Status: ⬜ Futuro

- [ ] Terreno
- [ ] Aliados
- [ ] Enemigos visibles
- [ ] Torres
- [ ] Creeps
- [ ] Objetivos
- [ ] Pings
- [ ] Cámara

---

## FASE XII — Dirección artística

### Milestone 29 — Biblia visual

Status: ⬜ Futuro

Antes de producir toneladas de arte:

- [ ] Paleta Azure
- [ ] Paleta Ember
- [ ] Arquitectura
- [ ] Materiales
- [ ] Forma de torres
- [ ] Lenguaje visual de héroes
- [ ] VFX
- [ ] UI
- [ ] Tipografía
- [ ] Símbolos
- [ ] Logo

Aquí fijamos definitivamente qué es CierzoArena.

### Milestone 30 — Vertical slice artístico

Status: ⬜ Futuro

Solo una parte pequeña, pero casi final:

- [ ] 1 héroe
- [ ] 1 enemigo
- [ ] 1 torre
- [ ] Fragmento de lane
- [ ] Vegetación
- [ ] VFX
- [ ] UI
- [ ] Audio

> 🚪 Gate: comprobar que el estilo visual funciona antes de producir todo.

### Milestone 31 — Pipeline 3D

Status: ⬜ Futuro

- [ ] Modelado
- [ ] Rig
- [ ] Skin
- [ ] Animaciones
- [ ] LOD
- [ ] Materiales
- [ ] Importación
- [ ] Convenciones
- [ ] Presupuesto de polígonos

Este probablemente será uno de nuestros cuellos de botella reales.

### Milestone 32 — Animación

Status: ⬜ Futuro

- [ ] Idle
- [ ] Walk
- [ ] Run
- [ ] Attack
- [ ] Cast
- [ ] Hit
- [ ] Death
- [ ] Channeling
- [ ] Transiciones

### Milestone 33 — VFX

Status: ⬜ Futuro

- [ ] Ataques
- [ ] Habilidades
- [ ] Impactos
- [ ] Buffs
- [ ] Debuffs
- [ ] Muertes
- [ ] Objetivos
- [ ] Torres

### Milestone 34 — Audio

Status: ⬜ Futuro

- [ ] UI
- [ ] Ataques
- [ ] Pasos
- [ ] Habilidades
- [ ] Impactos
- [ ] Ambiente
- [ ] Torres
- [ ] Música
- [ ] Anunciador
- [ ] Mezcla

---

## FASE XIII — Contenido jugable

### Milestone 35 — Primer roster Alpha

Status: ⬜ Futuro

No lanzaríamos con 100 héroes.

Primer objetivo:

- [ ] 6 héroes mínimo
- [ ] idealmente 8–12 para Alpha seria
- [ ] roles diferentes
- [ ] identidades visuales
- [ ] matchups
- [ ] habilidades completas

### Milestone 36 — Catálogo de objetos Alpha

Status: ⬜ Futuro

- [ ] 40–60 objetos iniciales
- [ ] ofensivos
- [ ] defensivos
- [ ] utilidad
- [ ] movilidad
- [ ] consumibles

### Milestone 37 — Balance inicial

Status: ⬜ Futuro

- [ ] TTK
- [ ] XP
- [ ] oro
- [ ] duración de partidas
- [ ] torres
- [ ] creeps
- [ ] héroes
- [ ] objetos
- [ ] comeback mechanics si existen

---

## FASE XIV — Bots y entrenamiento

### Milestone 38 — Bots básicos

Status: ⬜ Futuro

- [ ] Mover
- [ ] Lane
- [ ] Atacar
- [ ] Retirarse
- [ ] Comprar
- [ ] Usar habilidades

### Milestone 39 — Tutorial

Status: ⬜ Futuro

- [ ] Movimiento
- [ ] Ataque
- [ ] Habilidades
- [ ] Compra
- [ ] Torres
- [ ] Victoria

---

## FASE XV — Plataforma y servicios

### Milestone 40 — Steamworks

Status: ⬜ Futuro

- [ ] App ID
- [ ] Builds
- [ ] Depots
- [ ] Steam login
- [ ] Rich Presence
- [ ] Invites
- [ ] Achievements si procede

### Milestone 41 — Matchmaking

Status: ⬜ Futuro

- [ ] Cola
- [ ] Formación de partida
- [ ] Rating
- [ ] Región
- [ ] Latencia
- [ ] Cancelación
- [ ] Aceptación de partida

### Milestone 42 — Perfil y progresión externa

Status: ⬜ Futuro

Solo si decidimos incluirlo.

- [ ] Cuenta
- [ ] Nivel de jugador
- [ ] Historial
- [ ] Estadísticas
- [ ] Cosméticos
- [ ] Desbloqueos

---

## FASE XVI — Seguridad y estabilidad

### Milestone 43 — Autoridad y anticheat básico

Status: ⬜ Futuro

- [ ] Cliente no decide daño
- [ ] Cliente no decide oro
- [ ] Cliente no decide cooldowns
- [ ] Validación de órdenes
- [ ] Velocidad máxima
- [ ] Integridad

### Milestone 44 — Performance

Status: ⬜ Futuro

- [ ] CPU
- [ ] GPU
- [ ] Draw calls
- [ ] GC allocations
- [ ] 100+ unidades
- [ ] VFX
- [ ] Network bandwidth
- [ ] Servidor

Objetivos concretos de FPS y hardware mínimo.

### Milestone 45 — QA automatizado avanzado

Status: ⬜ Futuro

Amplía el baseline establecido en el Milestone 2.2. El QA automatizado no queda solo cerca del final.

- [ ] Unit tests
- [ ] Integration tests
- [ ] Play Mode tests
- [ ] Build verification
- [ ] CI
- [ ] Smoke tests

---

## FASE XVII — Pruebas reales

### Milestone 46 — Pre-Alpha interna

Status: ⬜ Futuro

- [ ] Nosotros
- [ ] Amigos
- [ ] Bugs críticos
- [ ] Telemetría básica
- [ ] Feedback

### Milestone 47 — Closed Alpha

Status: ⬜ Futuro

- [ ] Usuarios reales
- [ ] NDA opcional
- [ ] Servidores
- [ ] Balance
- [ ] Crashes
- [ ] Retención

### Milestone 48 — Closed Beta

Status: ⬜ Futuro

- [ ] Más jugadores
- [ ] Matchmaking
- [ ] Stress tests
- [ ] Balance
- [ ] Tutorial
- [ ] UX

### Milestone 49 — Open Beta

Status: ⬜ Futuro

- [ ] Acceso público
- [ ] Escalabilidad
- [ ] Moderación
- [ ] Reportes
- [ ] Observabilidad
- [ ] Parches

---

## FASE XVIII — Lanzamiento

### Milestone 50 — Release Candidate

Status: ⬜ Futuro

- [ ] Cero blockers
- [ ] Build estable
- [ ] Store page
- [ ] Trailer
- [ ] Screenshots
- [ ] EULA
- [ ] Política de privacidad
- [ ] Licencias de assets
- [ ] Créditos

### Milestone 51 — Lanzamiento Steam

Status: ⬜ Futuro

- [ ] Build pública
- [ ] Free to Play
- [ ] Servidores
- [ ] Matchmaking
- [ ] Monitoring
- [ ] Soporte
- [ ] Hotfix pipeline

🎉 CierzoArena 1.0

---

## FASE XIX — Después del lanzamiento

### Milestone 52 — Live Operations

Status: ⬜ Futuro

El "último" milestone realmente nunca termina.

- [ ] Balance patches
- [ ] Bugs
- [ ] Nuevos héroes
- [ ] Nuevos cosméticos
- [ ] Temporadas
- [ ] Eventos
- [ ] Moderación
- [ ] Telemetría
