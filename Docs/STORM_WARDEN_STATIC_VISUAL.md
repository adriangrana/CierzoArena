# Storm Warden — visual estático inicial

## Alcance

Esta integración sustituye solo la malla visual placeholder del héroe cuyo `HeroId` es `storm_warden`. No modifica estadísticas, habilidades, movimiento, selección, colliders, red, HUD ni balance. El modelo no tiene rig, por lo que se traslada y rota rígidamente como hijo del gameplay root; no incluye `Animator` ni animaciones.

## Arquitectura data-driven

- `HeroVisualCatalog` contiene entradas opcionales `HeroVisualDefinition`, indexadas únicamente por `HeroId`.
- Una entrada define `VisualPrefab`, posición, rotación, escala local, `GroundOffset` y si oculta el placeholder.
- `HeroVisualController`, añadido al gameplay root por `HeroMatchIdentity.ConfigureHero`, resuelve la entrada una sola vez al cambiar el `HeroId` configurado durante spawn o replicación. Nunca consulta texto de UI, nombres visibles, índices ni `ClientId`.
- Cuando no existe una entrada (los otros cinco héroes), el controlador elimina/restaura solo su instancia visual opcional y conserva el cilindro placeholder y el acento procedural actuales.
- El prefab visual no es un `NetworkObject`: cada host y cliente lo instancia localmente bajo el gameplay root al recibir el mismo `HeroId` replicado. No hay RPC adicional ni registro de prefabs de red.

## Assets y builder

Los archivos importados se encontraron en `Assets/Resources/Art/Heroes/StormWarden/`, no en `Assets/Art/Heroes/StormWarden/`. Los originales no se mueven ni modifican destructivamente.

El menú **Cierzo Arena → Heroes → Build Storm Warden Static Visual** ejecuta `StormWardenVisualAssetBuilder` y genera de forma idempotente:

- `Assets/Art/Heroes/StormWarden/Materials/MAT_StormWarden.mat`
- `Assets/Art/Heroes/StormWarden/Prefabs/StormWardenVisual.prefab`
- `Assets/Resources/Heroes/HeroVisualCatalog.asset`

El wrapper tiene `StormWardenVisual → ModelRoot → instancia del FBX`. No lleva componentes de gameplay, `NetworkObject`, `NetworkTransform`, colliders, cámara, luces ni barra de vida.

## Material y modelo

- Shader: **Built-in `Standard`**, modo Opaque.
- Albedo: `Meshy_AI_Redshield_Warrior_0713162419_texture.png`.
- Normal: `Meshy_AI_Redshield_Warrior_0713162419_texture_normal.png`, importada como `NormalMap` en espacio lineal.
- Metallic: `Meshy_AI_Redshield_Warrior_0713162419_texture_metallic.png`.
- Roughness: se conserva sin modificar. Para esta primera prueba se usa `Smoothness = 0.33` (valor manual dentro de 0.25–0.4) en vez de generar en runtime una textura con alfa invertido. Un futuro paso offline puede empaquetar `R = metallic`, `A = 1 - roughness`.

La configuración de importación del FBX desactiva cámaras, luces, animaciones, Read/Write y generación de colliders; conserva normales importadas y calcula tangentes Mikk. Rig/Animation Type queda en None.

## Transform y bounds

El builder normaliza la altura del modelo a **2.15 m**. Después centra X/Z y apoya su mínimo Y en cero dentro del wrapper. `HeroVisualPrefabMetadata` persiste los valores reales y los bounds finales generados; así se puede informar o ajustar el resultado sin modificar el FBX.

La entrada del catálogo usa `LocalPosition = (0, 0, 0)`, `LocalRotation = (0, 0, 0)` y `LocalScale = (1, 1, 1)` porque esa normalización vive en `ModelRoot`. El movimiento y la rotación vienen del gameplay root existente.

No se crean sockets VFX sin rig: cuando exista uno, se añadirán en los huesos correspondientes sin tocar `HeroVisualController` ni la lógica del héroe.

## Muerte, respawn y fallback

El controlador muestra/oculta los renderers del modelo ante cambios de `HeroLifeCycle` y no instancia nada por frame. Limpiar o cambiar de héroe destruye exclusivamente la instancia bajo `VisualRoot`, reactiva el placeholder y conserva collider, círculo de selección, barra de salud e indicadores de equipo. El mismo flujo se utiliza para spawn inicial, cambio de escena, respawn y nuevas partidas.
