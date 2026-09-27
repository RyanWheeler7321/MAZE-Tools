<img src="icon.svg" alt="MAZE Tools icon" width="96">

# MAZE Tools

Some of the Unity runtime and editor code from MAZE, the Unity playground project I build and test my game ideas in. It's mostly rendering, vector graphics, transitions and LOD tools.

## Unity version

I'm on Unity 6.6 with URP, but it should work on other versions or be pretty easy to port. It also uses uGUI, and the SVG importer needs Unity's built-in Vector Graphics module.

## Components

### Environment

- `MazeAO` is screen-space ambient occlusion as a URP render feature, with its own settings and editor.
- `MazeAtmosphere` sets the sky, horizon, ambient and distance colors that the other environment systems use.
- `MazeClouds` makes layered procedural clouds that you can scatter through a volume or place by hand.
- `MazeFog` does distance, close, volume and horizon fog in one render feature.
- `MazeFocus` is depth of field with separate close and far blur.
- `MazeBloom`
- `MazeLightFX` does light shafts and lens flares.

### Rendering

- `MazeFilter` stacks screen filters like pixel, blur, dither, halftone, palette and edge, and can limit them to certain objects.
- `MazeTone` does tonemapping, exposure and contrast.
- `MazeRenderService` keeps track of which custom render passes ran each frame.

### Graphics and transitions

- `MazeVector` draws vector shapes at runtime, for UI and meshes in the world.
- `MazeVectorControlIcons` is a set of controller and keyboard button icons made with MazeVector.
- `MazeTransitions` does square and vector screen transitions, drawn fullscreen, on a mesh or as a UI overlay.

### Editor and debugging

- `MazeLOD` generates mesh LOD levels in the editor and switches them at runtime.
- `MazeTextureModify` is an editor window for adjusting textures, with channel views, tiling checks, before/after comparison and a history that keeps the original.
- `MazeSvgImporter` turns SVG files into MazeVector assets.
- `MazeProfiler` profiles MAZE and Unity systems to find where performance can improve.
- `MazeDiagnosticsLog` writes short logs from these systems while the game runs.
