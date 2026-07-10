# Cierzo Arena - Conventions

## Project direction

Cierzo Arena is an original 3D MOBA prototype. Do not use protected names, characters, abilities, icons, sounds, models, maps, or lore from existing MOBAs.

## Technical conventions

- Target engine: Unity 6.
- Language: C#.
- Keep gameplay code modular and data-driven where practical.
- Prefer small components over large manager classes.
- Use clear namespaces under `CierzoArena`.
- Keep prototype systems simple until the core loop feels playable.

## Folder conventions

- `Assets/Scripts/Core`: shared interfaces, team ownership, utilities.
- `Assets/Scripts/Combat`: health, damage, attacks, targeting.
- `Assets/Scripts/Units`: player units, selection, movement.
- `Assets/Scripts/Camera`: camera controllers.
- `Assets/Scripts/Editor`: editor-only scene/build helpers.
- `Assets/Scenes`: Unity scenes.
- `Assets/Prefabs`: reusable Unity prefabs.
- `Assets/Materials`: prototype materials.

## Gameplay constraints

- First milestone: one selectable unit, click movement, simple attack, health, death.
- Avoid online multiplayer until local combat, creeps, lanes and towers are reliable.
- Balance numbers should be easy to find in inspector fields.
