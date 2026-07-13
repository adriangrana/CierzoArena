# M23 — Rework funcional de bases

## Orientación base-relative

Las bases se generan de forma espejada desde el centro del mapa (origen):

- **Azure Base**: centro `(-60, 0, -60)` (esquina SW).
- **Ember Base**: centro `(60, 0, 60)` (esquina NE).
- **Forward** = `(-baseCenter).normalized` (de la base hacia el centro del mapa).
- **Right** = `(forward.z, 0, -forward.x)` (perpendicular en el plano).
- **Back** = `-forward` (hacia la zona segura / spawn).

Azure y Ember comparten el mismo layout jugable transformado/espejado; las diferencias son solo decorativas (acentos cian vs. rojo/cobre).

## Layout conceptual (de atrás hacia el mapa)

```
Back  ── Spawn Courtyard (héroe detrás del núcleo)
      ── Shop District (lateral, junto al spawn)
      ── Core / Town Center
      ── Core Guard Left + Core Guard Right  (última línea)
      ── Cruce interior
      ── Top / Mid / Bottom Gateways (3.ª torre de cada carril)
Map   ── Carriles hacia el mapa
```

## Estructuras y anchors generados por el builder

- **Core** (`StructureKind.Core`, `Tier.Core`) en el centro de la base, con collider de selección propio, barra de vida, marcador de minimapa y lógica de victoria.
- **Torres de carril** (3 por carril): `Tier.Outer`, `Tier.Inner`, `Tier.Gate`. La torre `Gate` es la 3.ª torre y defiende la entrada del carril a la base.
- **Core Guard Left / Right** (`StructureKind.Tower`, `Lane.None`, `Tier.CoreGuard`): situadas delante del núcleo, `forward*15` y `±right*11`, separadas del núcleo y sin bloquear navegación. Reutilizan el modelo de torre existente (`TowerController` + `DefensiveAggroResponder`).

## Regla BaseBreached

Una base está **breached** cuando **cualquiera** de sus tres carriles ha perdido sus tres torres estratégicas:

```
LaneBreached(lane)  = Outer(lane) destruida AND Inner(lane) destruida AND Gate(lane) destruida
BaseBreached        = LaneBreached(Top) OR LaneBreached(Mid) OR LaneBreached(Bottom)
```

Las Core Guard towers tienen `Lane.None`, por lo que **no** cuentan para el breach.

## Regla CoreVulnerable

```
CoreGuardsVulnerable = BaseBreached
CoreVulnerable       = CoreGuardLeft destruida AND CoreGuardRight destruida
```

- Antes del breach: las dos Core Guards están **protegidas** (no atacables, no reciben daño, no seleccionables como objetivo por héroes ni creeps).
- Con el breach: ambas Core Guards se vuelven atacables.
- El núcleo permanece protegido mientras **viva al menos una** Core Guard.
- Solo con ambas Core Guards destruidas el núcleo se vuelve atacable y su destrucción provoca victoria.

Compatibilidad: en escenas sin Core Guards autorizadas (spike de red), se aplica la regla previa (cualquier carril despejado abre el núcleo).

## Fuente única de verdad

Toda la regla vive en `StructureProgressionController` (autoritativo en servidor). No se duplica en héroes, creeps, UI, torres ni núcleo. API pública:

- `IsBaseBreached(TeamId)`
- `AreCoreGuardsVulnerable(TeamId)`
- `IsCoreVulnerable(TeamId)`
- `IsAttackable(StructureEntity)` — puerta única de vulnerabilidad usada por `StructureEntity.CanReceiveDamageFrom`.

El servidor es dueño de las estructuras; la evaluación es autoritativa y se replica.

## Rutas de creeps

Las `LaneRoute` terminan apuntando al núcleo enemigo (`emberCore` / `azureCore`). Con el nuevo layout, la progresión estratégica es:
carril → torre 1 → torre 2 → torre 3 (gateway) → interior → Core Guards (cuando vulnerables) → Core (cuando vulnerable). Los creeps atacan solo estructuras atacables (`IsAttackable`), por lo que respetan automáticamente la protección de Core Guards y núcleo.

## NavMesh, Attack Anchors, minimapa

- El NavMesh se construye **en runtime**, no con bake de editor: `LargeNavMeshBootstrap` (creado por el builder) llama a `RuntimeNavMesh.EnsureBuilt` en `Awake`, recolectando colliders de la **capa Ground (6)** dentro de un `Bounds` de 200×40×200. No existe asset de NavMesh horneado ni paso manual de bake; basta con entrar en Play. Las decoraciones del pueblo se colocan en la capa default (0) con colliders simples o ninguno, por lo que **no** afectan al NavMesh (ninguna maceta bloquea una oleada).
- Los creeps usan el punto de aproximación de cada estructura (`StructureEntity.GetApproachPoint`) y `NavMesh.SamplePosition` para el núcleo, en lugar del centro del mesh.
- El minimapa refleja núcleo, torres de carril y Core Guards mediante los marcadores de estructura existentes; no dibuja casas/props.

## Implementación (builders y assets)

Todo es determinista y vive en código de editor + assets serializados:

- `Assets/Scripts/Environment/FantasyVillageEnvironmentPalette.cs` — ScriptableObject (Runtime) con el subconjunto curado de prefabs.
- `Assets/Scripts/Environment/TeamBaseLayoutDefinition.cs` — ScriptableObject (Runtime) con offsets base-relativos y `Resolve(baseCenter)`.
- `Assets/Scripts/Editor/FantasyVillagePaletteBuilder.cs` — puebla la paleta y el layout vía `AssetDatabase` (solo editor). Menú `Cierzo Arena → Environment → Build Fantasy Village Palette`.
- `Assets/Scripts/Editor/FantasyVillageBaseBuilder.cs` — construye por base la jerarquía Gameplay (anchors + attack anchors) y Visuals (town center, plaza, courtyard, shop district, casas de perímetro, caminos, árboles, flores, farolas, cajas), con colliders simplificados y flags static, sin tocar prefabs originales.
- `Assets/Scripts/Editor/MobaGreyboxArenaBuilder.cs` — integra: resuelve paleta+layout, coloca spawn/tienda/cámara desde el layout, invoca `BuildVillageBases` tras las torres y extiende las `LaneRoute` con `WithInterior` (gateway → interior → core-defense approach → core approach).
- `Assets/Scripts/Editor/M23EnvironmentValidator.cs` — menú `Cierzo Arena → Environment → Validate M23 Base Rework`, informe pass/fail de la escena abierta.

Jerarquía por base: `TeamBaseRoot/{Gameplay, Visuals, Debug}` con anchors explícitos (HeroSpawn, Respawn, CameraStart, Shop, Shopkeeper, gateways, interiores, core approaches, attack anchors).

## Materiales del paquete en Built-in (causa del render blanco)

**Síntoma:** los props del paquete "Low Poly Fantasy Village" (árboles, casas, tejados, agua, farolas) se veían **blancos** en escena y en Play.

**Causa raíz exacta:** los materiales del paquete (`Color.mat`, `Light.mat`) usan el shader **URP/Lit** (guid `933532a4fcc9baf4fa0491de14d08ed7`) y su atlas de color (`Textures/Gradient.png`) está enlazado a la propiedad `_BaseMap`, que **solo existe en URP**. Este proyecto corre el **pipeline Built-in** (`GraphicsSettings.m_CustomRenderPipeline = {fileID: 0}`; el paquete URP 17.5.0 está instalado pero no es el pipeline activo). En Built-in, URP/Lit no está soportado: el fallback Standard lee el `_MainTex` vacío (ignora `_BaseMap`) y, con `_Color` = blanco, **renderiza blanco sólido**. No era escala, ni el prefab equivocado, ni un override nuestro.

**Solución (sin tocar el pipeline ni los assets originales):** `Assets/Scripts/Editor/FantasyVillageMaterialRemapper.cs` genera, una vez por material de origen, una **variante Standard de Built-in** guardada en `Assets/CierzoArena/Art/Environment/FantasyVillage/Materials/`, copiando el atlas `_BaseMap → _MainTex`, `_BaseColor → _Color`, normal (`_BumpMap`) y emisión (`_EmissionColor`). Al instanciar cualquier prop del paquete, los builders llaman a `RemapInstance`, que re-apunta los `sharedMaterials` no soportados a su variante. Es determinista (cacheada en memoria y persistida como asset) y reversible (los materiales originales del paquete nunca se modifican).

El validador M23 detecta regresiones: cuenta renderers con material faltante o con shader no soportado (URP-en-Built-in) y los marca como FAIL.

## Fase A/B — sustitución del greybox

- **Fase A (recolor):** el suelo dejó de usar `Rocky`/`Concrete` (lectura de cemento) y ahora usa materiales naturales: `Env_Grass.mat` (hierba) para el terreno y `Env_Dirt.mat` (tierra compacta) para carriles y plataformas de base. Piedra/rocoso se reserva para muros, puentes y acantilados donde la lectura de piedra es correcta. El gameplay, los colliders y el NavMesh no cambian (son materiales visuales).
- **Fase B (piedra selectiva):** `FantasyVillageMapAmbienceBuilder.BuildStonePaths` empedra solo las zonas que se leen como "calle": una plaza en anillo alrededor del núcleo central y bocas empedradas cortas donde el carril central desemboca en cada base. Es decorativo (sin collider, ligeramente sobre el suelo para evitar z-fighting); no se pavimenta cada metro.

## Corrección visual posterior al primer build

El primer build mostró props blancos y greybox expuesto. La auditoría confirmó que los variantes iniciales se guardaban por nombre y no copiaban el atlas URP serializado cuando `Material.HasProperty("_BaseMap")` devolvía falso en Built-in. El remapper ahora lee la propiedad serializada como fallback, usa una correspondencia persistente `GUID material original → variant Standard`, y el validador inspecciona el material realmente asignado a cada renderer.

La geometría de gameplay se conserva, pero su `MeshRenderer` provisional queda desactivado: plataformas de base, cintas de carril, cubos de jungla, puentes/bordes de contención, pads de campamentos y núcleo-cubo siguen aportando colliders, NavMesh, selección y rutas sin mostrarse como arte final. El núcleo lógico mantiene su health bar; el Town Center (`House_3`) y el cristal del núcleo son la lectura visual. La decoración de base se limpia y reconstruye determinísticamente bajo `Bases/*/{Gameplay,Visuals,Debug}`; el perímetro y la ambientación se reemplazan bajo `M23 Map Environment`.

## Regeneración y auditoría manual

1. `Cierzo Arena → Environment → Clean Generated M23 Environment`.
2. `Cierzo Arena → Environment → Rebuild M23 Environment`.
3. `Cierzo Arena → Environment → Compare Original And Remapped Prefab` para inspeccionar un original y su variante lado a lado en Console.
4. `Cierzo Arena → Environment → List White Or Unsupported Renderers`.
5. `Cierzo Arena → Environment → Validate M23 Base Rework`.

La escena queda pendiente de esa regeneración en Unity para sustituir las instancias antiguas ya serializadas; no se cambia ninguna torre ni prefab original del paquete.

## Pruebas

Tests Edit Mode en `Assets/Tests/Editor/StructureAndMatchTests.cs`:

- `BaseBreachRequiresAllThreeTowersAndAnyLaneCounts`
- `TopMidBottomEachBreachIndependently`
- `CoreGuardsProtectedBeforeBreachAndVulnerableAfter`
- `CoreProtectedWhileOneGuardAliveAndOpensWhenBothDestroyed`
- `BreachAndCoreStateAreTeamScoped`

Tests Edit Mode en `Assets/Tests/Editor/FantasyVillageM23Tests.cs`:

- `HeroSpawnIsBehindCoreForBothTeams`
- `CoreGuardsAndGatewaysAreInFront`
- `AzureAndEmberShareTheSameRelativeFootprint`
- `ResolveIsDeterministic`
- `EmptyPaletteIsInvalidAndPopulatedPaletteIsValid`
- `BuiltInVariantPreservesPackageAtlasAndUsesGuidMapping`
- `BuiltInVariantPreservesSourceColourWithoutMutatingOriginal`
- `RemapInstanceIncludesInactiveChildrenAndKeepsMaterialSlotCount`

Validación en editor pendiente (requiere Unity): ejecutar el builder para poblar paleta y generar la escena, `Validate M23 Base Rework`, Play Mode, Netcode Host/Client, capturas y perfilado.
