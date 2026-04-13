# OarWeThereYet: Project Mandates & Standards

This project is a multiplayer rafting game built with **Godot 4.5** and **C# (Mono)**. Adherence to these standards ensures consistency across the codebase. If you learn information that you think would be useful to know in the future, add it to this file.

## Technical Stack
- **Engine:** Godot 4.5 (Forward Plus renderer)
- **Primary Language:** C# (Standard Godot/C# conventions)
- **Secondary Language:** GDScript (reserved for simple glue logic or specific autoloads)
- **Physics:** Jolt Physics (3D)
- **Key Addons:**
  - `Terrain3D`: For landscape and terrain management.
  - `Waterways`: For river flow and buoyancy systems.
  - `GodotSteam`: For Steam integration and multiplayer networking.
  - `DebugDraw3D`: For development visualization.

## Architectural Patterns
- **Composition over Inheritance:** Use components (like the `Health` component in `Boat.cs`) to manage state and behavior rather than deep inheritance hierarchies.
- **Signal-Based Communication:** Utilize `GlobalSignalServer` (C#) for decoupled communication between major systems. Local signals should follow Godot's `EventHandler` naming convention in C#.
- **Voice Input Routing:** Mic device selection should be broadcast through `GlobalSignalServer.AssignInputDevice` and applied by `prox_chat.gd` using `AudioServer.input_device`.
- **Stateless Visuals:** Keep logic in scripts and use shaders (`WaterShader/`) or minimal textures for the "TABS-like" low-poly aesthetic.
- **Multiplayer Synchronization:** Implement `ISyncBuffer` for objects that need state synchronization across the network (e.g., the `Boat`).

## Coding Standards (C#)
- **Namespaces:** Match the folder structure under `scenes_scripts`.
- **Naming Conventions:**
  - `PascalCase`: Classes, Methods, Public Properties, Public Fields, Enums.
  - `_camelCase`: Private fields (must start with an underscore).
  - `camelCase`: Local variables, method parameters.
- **Signals:** Always use the `[Signal]` attribute and the `EventHandler` suffix (e.g., `[Signal] public delegate void SeatEnteredEventHandler(...)`).
- **Exports:** Group related exports using `[ExportGroup]` or `[ExportSubgroup]` for better Inspector organization in the Godot Editor.
- **Nodes:** Cache node references in `_Ready()` using `GetNode<T>()` rather than fetching them every frame.

## Project Structure
- `scenes_scripts/`: Contains all logic and scene files, organized by functional area (e.g., `boat/`, `player/`, `UI/`).
- `art/`: All 3D models (`.blend`), textures, and themes.
- `terrain_data/`: Resources specific to `Terrain3D`.
- `WaterShader/`: Custom shaders for water and other effects.
- `addons/`: Third-party plugins. Do not modify these directly unless necessary for bug fixes.
