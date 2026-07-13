# Recursos de terceros

Este registro refleja exactamente la información incluida dentro del proyecto a fecha de M21. Los recursos sin licencia distribuida junto al archivo no se consideran verificados para una distribución comercial hasta conservar su licencia original.

| Nombre | Autor / fuente | Licencia incluida | Fecha de incorporación | Carpeta | Uso en CierzoArena | Modificaciones |
|---|---|---|---|---|---|---|
| ConcreteWall (`concrete_wall_009`) | No indicada en los archivos importados | No se encontró LICENSE, README ni NOTICE | 2026-07-12 (fecha de los archivos del workspace) | `Assets/Resources/Art/Environment/Materials/ConcreteWall/` | Material `MAT_ConcreteWall_01`, arquitectura de la vertical slice | Importación 2K, normal lineal, tiling y material Built-in compartido. Fuente original conservada. |
| RockyTerrain (`rocky_terrain_02`) | No indicada en los archivos importados | No se encontró LICENSE, README ni NOTICE | 2026-07-12 (fecha de los archivos del workspace) | `Assets/Resources/Art/Environment/Materials/RockyTerrain/` | Material `MAT_RockyTerrain_02`, rocas, bordes y fosa | Importación 2K, normal/roughness lineales, tiling y material Built-in compartido. Fuente original conservada. |
| Stylize Water Texture 1.0 | LowlyPoly, Asset Store package id 153577 | Información de origen conservada en los `.meta`; no se encontró un archivo de licencia textual dentro del paquete | Importado previamente al proyecto | `Assets/Stylize Water Texture/` | Texturas base/normal/roughness de `MAT_CierzoWater` | No se usa el shader legado. Se reutilizan texturas en un material `Standard` Built-in y se anima UV por `MaterialPropertyBlock`. |
| Free Animated Low Poly Goblin 1.0 | cresnit, Asset Store package id 313526 | Información de origen conservada en los `.meta`; no se encontró un archivo de licencia textual dentro del paquete | 2026-07-13 | `Assets/cresnit/` | Modelo visual de los creeps de línea | Se instancia como hijo visual; se conservan collider, NavMesh, combate y red del creep. Sus materiales se adaptan en ejecución a `Standard` Built-in si el paquete usa un shader incompatible. |
| Awesome Stylized Mage Tower 1.0 | Asset Store package id 53793 | Información de origen y licencia Store conservadas en los `.meta`; no se encontró un archivo de licencia textual dentro del paquete | 2026-07-13 | `Assets/Tower/` | Modelo visual de las torres de línea | Se instancia como hijo visual y conserva el root autoritativo de estructura, su collider, bloqueo de navegación, vida y red. |

## Nota de distribución

Antes de publicar una build, verificar y archivar la licencia comercial o de redistribución de ConcreteWall y RockyTerrain. M21 no descarga ni añade recursos externos; únicamente procesa archivos ya presentes en el workspace.
