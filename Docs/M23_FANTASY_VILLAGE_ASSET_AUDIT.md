# M23 — Auditoría de Low Poly Fantasy Village Environment

## 1. Ruta real del paquete

Ruta encontrada en el proyecto (verificada, no hardcodeada):

```
Assets/OccaSoftware/Low Poly Fantasy Village/
```

Subcarpetas:

- `Materials/` — `Color.mat`, `Light.mat`
- `Meshes/` — `Village.fbx`
- `Prefabs/` — 60+ prefabs (ver catálogo)
- `Samples/Demo/Scene/Village.unity` — escena demo principal
- `Samples/Demo/Showcase/Showcase.unity` — escena de showcase
- `Textures/`

Publisher: **OccaSoftware**. No se ha vuelto a descargar ni se ha añadido ningún otro paquete ambiental.

## 2. Escena demo encontrada

- `Assets/OccaSoftware/Low Poly Fantasy Village/Samples/Demo/Scene/Village.unity`
- `Assets/OccaSoftware/Low Poly Fantasy Village/Samples/Demo/Showcase/Showcase.unity`

Se usan **solo como catálogo e inspiración**. No se copian dentro del mapa de CierzoArena ni se modifican.

## 3. Render pipeline

Proyecto: **Built-in Render Pipeline** (el pipeline activo). Verificado por:

- `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}` (ningún SRP asignado).
- `ProjectSettings/QualitySettings.asset`: todos los niveles con `customRenderPipeline: {fileID: 0}`.
- `Assets/Scripts/Editor/EnvironmentArtPipeline.cs` y README M21: el proyecto usa Built-in y materiales `Standard`.

Nota: el paquete `com.unity.render-pipelines.universal` (17.5.0) está instalado y hay un `UniversalRenderPipelineGlobalSettings.asset`, pero **no** está asignado como pipeline activo, por lo que el render efectivo es Built-in.

Los materiales del paquete (`Color.mat`, `Light.mat`) referencian el shader **URP/Lit** (guid `933532a4fcc9baf4fa0491de14d08ed7`) y almacenan su atlas en `_BaseMap`. Ese shader no está soportado por el pipeline Built-in activo: sin conversión, el fallback Standard busca `_MainTex` y los props se ven blancos. La adaptación se realiza exclusivamente mediante variantes locales en `Assets/CierzoArena/Art/Environment/FantasyVillage/Materials/`, sin tocar los originales.

## 4. Catálogo de prefabs (clasificación interna)

Total: 60+ prefabs únicos en `Prefabs/`.

| Categoría | Prefabs |
| --- | --- |
| Edificios (casas) | `House_1`, `House_2`, `House_3` |
| Composición / aldea | `Village`, `Showcase` |
| Caminos (piezas) | `Path Piece_1` … `Path Piece_16` |
| Caminos (completos) | `Path_1`, `Path_2` |
| Puentes | `Bridge` |
| Barcos | `Boat` |
| Árboles (frondosos) | `Tree_1` … `Tree_8` |
| Árboles (pino) | `Pine Tree_1` … `Pine Tree_5` |
| Flores | `Flower_1` … `Flower_5`, `Flower Pot` |
| Acantilados | `Cliff_1` … `Cliff_8` |
| Montañas | `Mountain_1` … `Mountain_4` |
| Rocas | `Rock_1` … `Rock_4` |
| Props | `Bench`, `Crate`, `Fence`, `Lantern` |

## 5. Selección propuesta (conjunto reducido y coherente)

- **Town Center / núcleo visual**: composición con `House_3` (mayor silueta) reforzada con módulos; alternativa `Village`.
- **Casas de aldea (perímetro)**: `House_1`, `House_2`, `House_3`.
- **Caminos**: `Path_1` para plazas/base, `Path Piece_*` para conexiones.
- **Puente principal (río)**: `Bridge`.
- **Árboles**: `Tree_2`, `Tree_5` (frondosos, focales) + `Pine Tree_2`, `Pine Tree_4` (jungla/perímetro).
- **Flores**: `Flower_1`, `Flower_3` en grupos; `Flower Pot` en tienda/plaza.
- **Acantilados/montañas (límites)**: `Cliff_3`, `Cliff_6`, `Mountain_2`, `Mountain_4`.
- **Props de mercado/tienda**: `Crate`, `Bench`, `Lantern`, `Fence`, `Flower Pot`.
- **Orilla del río**: `Boat` (1 unidad), `Cliff_*`, vegetación baja.

## 6. Prefabs descartados (por ahora)

- La mayoría de `Path Piece_*` intermedios (se usan 2-3 para evitar seams).
- `Rock_1..4` salvo puntualmente en jungla/orilla.
- `Showcase` completo (solo referencia).

## 7. Materiales adaptados

Política: **no** se modifican `Color.mat` ni `Light.mat` originales. `FantasyVillageMaterialRemapper` crea variantes Standard de Built-in identificadas por el GUID del material fuente, no por su nombre. La variante transfiere atlas `_BaseMap → _MainTex`, color base, normal y emisión. El GUID fuente queda registrado en el importer del variant para comprobar la trazabilidad tras cerrar/reabrir Unity. Los antiguos assets `Color_BuiltIn.mat` / `Light_BuiltIn.mat`, creados por el prototipo de remapper basado en nombres, se eliminan al reconstruir las variantes.

## 8. Colliders

Los colliders importados no se usan a ciegas. Política M23:

- Casas: footprint simplificado (`BoxCollider`).
- Árboles grandes: collider de tronco solo en perímetro.
- Flores, macetas, caminos, bancos alejados, barcos fuera de agua: **sin** collider.
- Farolas: collider pequeño o ninguno según ubicación.
- Sin `MeshCollider` complejo en props decorativos.

## 9. Limitaciones y dependencias ausentes

- **Buto — Volumetric Fog and Lighting**: NO incluido y NO se añade. Las imágenes promocionales del paquete usan Buto; CierzoArena conserva su **fog of war** propia. Este milestone no requiere niebla volumétrica.
- **NPC vendedor**: el paquete es puramente ambiental; **no incluye personaje vendedor/merchant**. Se construye una zona de tienda con props (`Crate`, `Bench`, `Lantern`, `Fence`, `Flower Pot`) y se deja un `ShopkeeperAnchor` preparado para un NPC futuro. La tienda funcional (tecla `B`, `ShopZone`) sigue operando igual.

## 10. Protección de assets originales

No se modifican prefabs, meshes, materiales, texturas ni escenas originales del paquete. Los prefabs se instancian con `PrefabUtility.InstantiatePrefab` (variantes/instancias en escena), y los colliders/escala/estáticos se ajustan **en la instancia**, nunca en el prefab fuente.

## 11. Paleta y selección efectiva (implementada)

El builder de editor `FantasyVillagePaletteBuilder` genera el asset
`Assets/CierzoArena/Settings/Environment/FantasyVillageEnvironmentPalette.asset`
con referencias serializadas al subconjunto curado (no runtime AssetDatabase):

- MainBuilding: `House_3`
- SecondaryBuildings: `House_1`, `House_2`
- SmallHouses: `House_1`, `House_2`, `House_3`
- PathStraight: `Path_1` · PathPieces: `Path Piece_1/5/9` · Bridge: `Bridge`
- Trees: `Tree_2/5/7` · PineTrees: `Pine Tree_2/4`
- Flowers: `Flower_1/3` · FlowerPot: `Flower Pot`
- Cliffs: `Cliff_3/6/8` · Mountains: `Mountain_2/4` · Rocks: `Rock_2/3`
- Props: `Bench`, `Crate`, `Fence`, `Lantern`, `Boat`

El layout reutilizable vive en
`Assets/CierzoArena/Settings/Environment/TeamBaseLayoutDefinition.asset`
(`TeamBaseLayoutDefinition`), con offsets base-relativos compartidos por Azure y Ember.
