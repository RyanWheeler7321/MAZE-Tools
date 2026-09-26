<img src="icon.svg" alt="MAZE Tools icon" width="96">

# MAZE Tools

Some of the Unity runtime and editor code from MAZE, the Unity playground project I build and test my game ideas in. It's mostly rendering, vector graphics, transitions and LOD tools.

## Requirements

- Unity 6.3 or newer with URP 17 (I'm on 6000.6)
- uGUI (`com.unity.ugui`)
- Unity's built-in Vector Graphics module (`com.unity.modules.vectorgraphics`) for the SVG importer

## Components

### Environment

- `MazeAO` adds screen-space ambient occlusion with its own profile, controller, editor, and URP render feature.
- `MazeAtmosphere` provides shared sky, horizon, ambient, and distance-color settings for the other environment systems.
- `MazeClouds` builds layered procedural cloud fields from editable profiles, scatter volumes, and authored cloud points.
- `MazeFog` handles distance, close, volume, and horizon fog through one controller and render feature.
- `MazeFocus` controls depth-of-field focus with target tracking, distance limits, and profile-driven behavior.
- `MazeBloom` provides a separate profile-driven bloom pass instead of relying on a global post-processing volume.
- `MazeLightFX` handles light shafts and lens-flare style screen effects.

### Rendering

- `MazeFilter` is a layered screen-filter system with pixel, blur, dither, halftone, palette, edge, and object-mask effects.
- `MazeTone` handles exposure, contrast, grading, and tone adjustments through a dedicated profile and pass.
- `MazeRenderService` and the shared RenderGraph helpers run the custom fullscreen passes and keep a list of which ones ran each frame.

### Graphics and transitions

- `MazeVector` is a runtime vector-graphics system for shapes, styles, recipes, UI graphics, world meshes, batching, painting, and reusable vector assets.
- `MazeVectorControlIcons` supplies reusable input glyph definitions and icon variants built through MazeVector.
- `MazeTransitions` provides square and vector transition profiles, patterns, handles, and several rendering backends.

### Authoring and diagnostics

- `MazeLOD` creates and manages generated mesh LOD levels with runtime controllers and editor baking tools.
- `MazeTextureModify` is an editor window for non-destructive texture adjustments, channel inspection, tiling checks, comparison views, and history.
- `MazeSvgImporter` turns SVG files into MazeVector assets with reusable import settings.
- `MazeProfiler` records feature groups, timings, automatic candidates, and current custom render-pass activity.
- `MazeDiagnosticsLog` writes short structured runtime logs for the included systems.

## Layout

- `Source/Runtime` contains project-facing components and reusable runtime code.
- `Source/Editor` contains inspectors, importers, bakers, and authoring windows.
- `Source/Shaders` contains the shared URP rendering shaders.
