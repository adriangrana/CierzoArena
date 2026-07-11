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
- **M7** — Creeps, oleadas y aggro defensivo: seis spawners generan oleadas Azure/Ember en top, mid y bottom. Los creeps melee/ranged siguen rutas, buscan el enemigo válido más cercano, mantienen foco, respetan leash y usan `BasicAttack`. Si un héroe daña a un héroe aliado cercano, creeps y torres cambian temporalmente al agresor. En red, solo el servidor genera y simula creeps.
- **M8** — Muerte y respawn de héroes: los héroes pasan por `Alive`, `Dead` y `Respawning`; al morir se limpian órdenes y objetivos, se oculta su presentación y reaparecen con vida máxima en el `HeroSpawnPoint` de su equipo. Azure queda seleccionado al iniciar y al reaparecer, por lo que el clic derecho sobre creeps enemigos ordena perseguir y atacar de inmediato. El servidor controla y replica el ciclo en la escena multijugador.
- **M9** — Experiencia y niveles: el héroe que logra el último golpe a un creep o héroe enemigo recibe experiencia autoritativa. La curva usa 100 XP al nivel 1 y un crecimiento de 1,25; cada nivel añade 80 de vida máxima (y vida actual), 8 de daño y 0,2 de velocidad. El nivel se conserva tras morir y se replica en red.
- **M10** — Oro y experiencia compartida: los creeps reparten su XP total entre héroes enemigos vivos dentro de 14 unidades, preservando el resto de forma determinista. Sólo el último golpe de un héroe concede oro (40 melee, 55 ranged); oro y XP se mantienen tras respawn y se replican desde el servidor.
- **M11** — Tienda, inventario e ítems básicos: cada base tiene una tienda aliada y un catálogo de cinco objetos. El inventario tiene seis huecos, conserva objetos tras muerte/respawn y recalcula vida máxima, daño, movimiento y cadencia desde sus slots. En red, el cliente propietario sólo solicita compra/venta; el servidor valida zona, equipo, oro, vida y estado de partida, y replica los IDs del inventario.
- **M12** — Maná y habilidades básicas: los héroes tienen maná regenerable, puntos de habilidad y cuatro slots. Q lanza un proyectil dirigido, W y R dañan áreas y E concede velocidad temporal. Coste, cast point, cancelación y cooldown se validan en servidor; host/cliente reciben el mismo estado.
- **M13** — Estados de combate: efectos temporales autoritativos (stun, root, silence, slow, escudo y buffs) con duración, refresco, limpieza al morir y snapshots de red. W ralentiza y R aturde en área.
- **M14** — Visión y niebla de guerra: héroes, creeps y estructuras aportan visión circular por equipo. Los enemigos móviles desaparecen fuera de visión; el terreno permanece como contexto del mapa y las estructuras enemigas conservan una representación oscurecida de su último estado conocido. Sus barras, ataques y cambios (incluida una destrucción no vista) sólo se actualizan al recuperar visión. El minimapa aplica la misma regla.
- **M15** — Jungla y campamentos neutrales: campamentos de tres tamaños generan neutrales hostiles a ambos equipos. Tienen aggro, leash, retorno con curación, XP por proximidad, oro por último golpe y respawn íntegro autoritativo. Los neutrales respetan la niebla y en red sólo el servidor los crea y simula.

La version real del proyecto esta en `ProjectSettings/ProjectVersion.txt`: Unity 6000.5.3f1.

## Como abrir

1. En Unity Hub, pulsa `Add` o `Open`.
2. Selecciona esta carpeta:

   `C:\Users\adria\Documents\Codex\CierzoArena`

3. Abre el proyecto con Unity 6.5.3f1.

## Como crear y probar la escena

1. Para crear la version de prueba del Milestone 2.1, usa `Cierzo Arena > Create Prototype Scene`.
2. Pulsa Play.
3. Azure ya empieza seleccionado. Haz clic izquierdo sobre él sólo si necesitas volver a seleccionarlo.
4. Haz clic derecho sobre el suelo para moverlo o sobre un enemigo —héroe o creep— para perseguirlo y atacarlo.
5. En melee, comprueba que el daño aparece tras un breve windup; en ranged, que el proyectil aparece tras el windup y el daño aparece solo al impactar.
6. Durante el windup, da una orden de movimiento: no debe haber impacto ni proyectil. Tras liberar un proyectil, cambiar de orden no debe detenerlo ni cambiar su objetivo.
7. Pulsa `S` para detener la orden actual.
8. En `MobaGreyboxArena`, entra en el rango de una torre enemiga: debe hacer windup, lanzar un proyectil y mantenerse inmóvil. Destruir un núcleo muestra el ganador y bloquea el gameplay restante, incluidos ataques y proyectiles pendientes.
9. Espera las oleadas: los creeps Azure y Ember avanzan por las tres líneas, se enfrentan al encontrarse y retoman su ruta al perder el objetivo.
10. Para probar M8, deja que una torre, creep o héroe enemigo mate a Azure: debe desaparecer, mostrar `Respawning in X`, rechazar órdenes y reaparecer seleccionado en su base con la barra llena. Space vuelve a centrar la cámara tras reaparecer. En `MultiplayerSpikeArena`, repite la prueba con host y cliente; cada uno conserva ownership y vuelve a su propio spawn.
11. Para probar M9, da el último golpe a dos creeps enemigos: el panel provisional muestra el nivel y XP; con 120 XP Azure llega a nivel 2 y aumenta vida, daño y velocidad. Matarlo y esperar su respawn no reinicia el nivel.
12. Para probar M10, sitúa dos héroes enemigos cerca de un creep que muere: ambos comparten la XP y sólo quien da el último golpe recibe el oro mostrado como `+40`/`+55`.
13. Para probar M11, consigue oro con últimos golpes y vuelve al círculo luminoso de tu base. Aparece `TEAM SHOP`: compra objetos, comprueba el panel de seis slots y sus estadísticas; vende uno. Fuera de la zona, muerto o tras la victoria no se permite comprar ni vender. Tras reaparecer, los slots y las bonificaciones deben mantenerse.
14. Para probar M12, usa los botones `+ Level` para aprender una habilidad. Q y luego clic izquierdo en enemigo lanza Arc Bolt; W/R y clic en suelo lanzan áreas; E se lanza sobre el héroe. Clic derecho o Escape cancela un cast antes de su punto de lanzamiento. El HUD muestra maná, puntos y cooldowns.
15. Para probar M14, aleja Azure de enemigos: héroes y creeps Ember deben desaparecer, mientras torres y núcleo Ember permanecen como siluetas oscuras sin barra de vida. Destruye una torre cuando Azure no pueda verla y vuelve a acercarte: conserva su último estado hasta que recuperes visión, momento en que se actualiza. El minimapa debe ocultar unidades móviles no vistas y atenuar las estructuras conocidas.
16. Para probar M15, entra en un campamento neutral: los neutrales deben atacar al objetivo válido cercano. Aléjate más allá de su leash para que vuelvan a origen y se curen. Limpia un campamento para recibir XP por proximidad y oro por último golpe; tras el temporizador reaparece la composición completa. Fuera de visión no deben verse ni aparecer en el minimapa.

## Controles

- Clic izquierdo: seleccionar unidad.
- Clic derecho en suelo: mover.
- Clic derecho en enemigo: perseguir y atacar.
- S: detener movimiento o ataque.

### Cámara técnica (M3A)

- Flechas: mover la cámara libremente (pasa a modo libre). Q/W/E/R están reservadas para habilidades.
- Rueda del ratón: zoom con límites.
- F: recentrar en la unidad y volver al seguimiento.

## Estado del milestone

Los Milestones 1 a 15 están implementados. M10 separa experiencia por proximidad y oro por último golpe; M11 permite gastarlo en objetos; M12–M13 añaden habilidades y estados; M14 añade visión por equipo y niebla; M15 añade jungla neutral. Para regenerar las escenas tras cambios de builders, usa los menús `Cierzo Arena > Create MOBA Greybox Arena` y `Cierzo Arena > Create Multiplayer Spike Scene`.
