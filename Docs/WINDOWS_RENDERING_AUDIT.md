# Windows rendering audit

Date: 2026-07-14. Target: Standalone Windows x64.

## Active render configuration

- `ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}`.
- Every Quality level, including the Standalone default (`Ultra`, index 5), has `customRenderPipeline: {fileID: 0}`.
- There are no project assets of type `UniversalRenderPipelineAsset`, `UniversalRendererData`, or `ScriptableRendererData`; Windows has no URP Asset and no URP Renderer Data.
- The only built scenes are `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/MobaGreyboxArena.unity`.

## Cause

`com.unity.render-pipelines.universal` was a direct package dependency with no consumer in project scripts, custom shaders, or other packages. Its editor postprocessor registers `UniversalRenderPipelineGlobalSettings` after every domain reload, even though this project does not select URP in Graphics or Quality settings.

That registration makes the build include `Universal Render Pipeline/Lit`. With no active URP Asset, Unity 6000.5 filters none of the Lit keyword space before native variant preparation, which produced the ~36 GB allocation and native crash. A shader stripper is too late because the allocation happens before `IPreprocessShaders` receives variants.

## Correction

The unused direct URP dependency was removed from `Packages/manifest.json`. Unity then regenerated `Packages/packages-lock.json` without the solely-URP dependency graph (`render-pipelines.core`, `shadergraph`, and `universal-config`). The existing `Assets/UniversalRenderPipelineGlobalSettings.asset` is preserved; without the URP package it is no longer an active player-build dependency and cannot inject URP/Lit.

## Rendering safety

No active renderer feature was changed: this is a Built-in Render Pipeline project. The active arena scene keeps its existing terrain, wall and water materials (`MAT_RockyTerrain_02`, `MAT_ConcreteWall_01`, and `MAT_CierzoWater`) and its fog shaders (`CierzoArena/Fog Of War Soft Overlay` and `Hidden/CierzoArena/Fog Mask Blend`). Therefore terrain, water, shadows, fog of war, heroes, creeps and towers remain on their current render path.

The Built-in `Standard` shader is explicitly included in `GraphicsSettings` because the arena creates compatibility materials at runtime for legacy package assets. This preserves its ShadowCaster pass in a Windows player; it does not add any URP shader or URP variant space. At arena startup the directional light and mesh renderers are also normalized to the prototype's shadow policy (soft shadows, high resolution, 65 m distance).

The fog-of-war overlay and mask-blend shaders are explicitly included in `GraphicsSettings` and are serialized in `Assets/Resources/Rendering/FogOfWarShaders.asset`. They are created by `Shader.Find` at runtime, which made Unity strip both from the Windows player even though they were available in the Editor. The Resources asset is the concrete player-build dependency; the Graphics Settings entries provide a second guard. They are two fixed Built-in shaders with no multi-compile declarations, so this inclusion is bounded and cannot reintroduce the URP/Lit variant explosion. `WindowsFogOfWarShaderBuildValidator` prevents a future build from silently omitting either shader.

## Required validation after package resolution

Post-restart validation completed:

- Unity registered 54 packages; `com.unity.render-pipelines.universal`, `render-pipelines.core`, `shadergraph`, and `universal-config` are absent from the resolved package graph.
- The latest script compilation succeeded (`Tundra build success`, exit code 0) with no script errors.
- The active Graphics and Quality pipeline assignments remain null. A legacy serialized URP global-settings mapping may remain in `GraphicsSettings.asset`, but it has no resolved URP code or shaders behind it and therefore cannot introduce `Universal Render Pipeline/Lit` into the player build.
- The current editor log contains no `Universal Render Pipeline/Lit` preparation entry after the restart.
