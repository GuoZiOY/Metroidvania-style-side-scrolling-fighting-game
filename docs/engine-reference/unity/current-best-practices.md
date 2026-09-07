# Unity 6000.4 — Current Best Practices

> New practices and features since LLM training cutoff (May 2025)
> Last verified: 2026-07-28

## Graphics & Rendering

### DirectStorage Support
Microsoft DirectStorage enables asset loading (textures, meshes, DOTS data) directly from NVMe drives, bypassing the CPU for **up to 40% load time reduction** on Windows Standalone.
- Enable via *Player Settings → Enable Direct Storage*
- Best for: large open worlds, streaming-heavy games, SSD-equipped PCs

### URP Render Graph (Mandatory)
URP Compatibility Mode is fully removed in 6000.4. All custom render passes must use the Render Graph API. This is not optional — any existing URP custom passes using the old API will not compile.

### HDRP Improvements
- **Volumetric Lighting Density Cutoff**: Disable fog lighting below density threshold for performance
- **HDRP Render Pipeline Converter**: Now supports upgrading Built-in RP materials to HDRP
- Improved PSO tracing and warming with new `GraphicsStateCollection` API

### Shader Graph
- New **Terrain Properties Node**: Pass terrain input properties into Terrain Lit shader graphs

## Platform Support

### macOS
- `CAMetalDisplayLink` support reduces stuttering and improves frame pacing (opt-in via Player Settings)

### Web
- Burst compiler now supports Web platform with multithreading
- `Microphone` scripting API now available on Web

### Windows
- DirectStorage (see Graphics section)

### Linux
- HIDAPI Controller Support for Desktop Linux

### VisionOS
- "Target minimum visionOS Version" exposed in Player Settings

## Editor & Workflow

### Dynamic Overlay Panels
Dock overlays in collapsible panels on either side of the Scene view. Stack and reorder overlays in a single zone.

### Grid & Snap Improvements
- Custom grid position/rotation
- Redesigned Grid and Snap overlay
- Separated Grid Size and Incremental Snap Size settings
- New Grid Tool Handle Rotation
- Customizable shortcuts

### Build Profiles UI
- Option to hide classic platforms
- Platform Browser groups by category (mobile, desktop, XR, web, console, industry, servers)

### Entities (ECS) as Core
Entities, Collections, Mathematics, and Entities Graphics now ship directly with the Editor — no separate package install required.

### Graph Toolkit Built In
Graph Toolkit no longer requires a separate package; integrated into the Editor.

### Project Auditor Built In
Now an Editor module — no separate package install.

### Animator Improvements
- `Evaluate Entry Transitions On Start`: State machines can start in a non-default state, eliminating the one-frame delay. Defaults to **true** for new controllers, **false** for existing assets.

### UI Builder (UI Toolkit)
- Drag-and-drop UXML/USS support
- Read-only ToggleButtonGroup, TabView, Tabs controls
- Default theme configurable in Project Settings
- Create UI Document from Hierarchy context menu

## 2D

- **Runtime Sprite Atlases**: Create/manage sprite atlases dynamically via `SpriteAtlasManager.CreateSpriteAtlas` API
- **Custom 2D Rendering & Post-Processing**: Custom render passes for 2D in URP

## Input System

- `OnMouseDown`, `OnMouseDrag`, `OnMouseUp` now work with the new Input System package

## Physics

- `Physics.RebuildBroadphaseRegions` un-deprecated (multi-box pruning broadphase restored)
- 2D: New query overloads returning `NativeArray<RaycastHit2D>` with allocator support
- 2D: `ColliderArray2D` collection type
- Physics module can now be **disabled** to reduce build size

## Audio

- Enhanced Audio Foundation: Android plugin enabled
- Scriptable Audio Processors: API improvements

## Version Control

- New Editor Toolbar button for Unity Version Control
- Status icons on prefab assets in Hierarchy (Edit Mode)
- Changeset/shelveset diff panel and properties panel
- Context menu actions for prefabs and prefab variants

## Key Package Versions (6000.4.0f1)

| Package | Version |
|---------|---------|
| Cinemachine | 2.10.6 |
| Timeline | 1.8.11 |
| Netcode for GameObjects | 2.10.0 |
| Input System | 1.19.0 |
| Addressables | 2.9.1 |
| Asset Manager for Unity | 1.10.0 |
