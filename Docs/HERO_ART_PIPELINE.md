# Hero portrait and ability-icon pipeline

Run **Cierzo Arena > Heroes > Build Roster Portraits and Icons** in Unity, or
allow the editor reload bootstrap to run once. `HeroRosterArtBuilder` creates
only missing project-local `Texture2D` assets in:

- `Assets/Resources/Art/UI/HeroPortraits/<HeroIdPascal>Portrait.asset`
- `Assets/Resources/Art/UI/AbilityIcons/<AbilityIdPascal>Icon.asset`

It never overwrites the six imported founding portraits. The runtime catalog
loads these assets by stable ID; if assets have not yet been imported (for
example on a clean command-line test run), it supplies an equivalent in-memory
portrait/icon so selection and tooltips remain safe. The HUD gives an authored
per-ability icon priority over the legacy six-hero atlas.

To add a hero, add one `N(...)` entry to `HeroRosterFactory` with four `A(...)`
definitions, a unique lower_snake_case HeroId, stats and tags. Then run the menu
command above. No scene, prefab or network ID changes are required.
