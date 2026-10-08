# 3DTD

A 3D tower defense in low-poly space. You stack building blocks, bolt towers onto any face, and stop enemies that fly along waypoint paths through 3D space.

## Status

Active again since autumn 2026. I upgraded the project to Unity 6, rebuilt the UI and the beginner levels, overhauled performance and revamped the projectile visuals. That work lives on the `fx/projectile-visuals` and `perf/overhaul` branches and is not merged yet; `main` still holds the 2024 version (Unity 2021.3). See [ROADMAP.md](ROADMAP.md).

## Stack

- Unity 6000.5.8f1, URP 17.5, VFX Graph, Burst.
- Legacy Input Manager.
- The paid art packs and the skybox textures are gitignored, so a fresh clone is missing them.

## Open the project

1. Add the `3DTD/` folder (the Unity project inside this repo) in Unity Hub and open it with editor 6000.5.8f1.
2. Open `Assets/Scenes/MainMenuLevel.unity` and press Play, or start a level such as `Beginner Level 01`.

## Controls

| Keys | Action |
| --- | --- |
| WASD, Q E, Shift, Space | Move |
| Right mouse | Look |
| Scroll | Zoom |
| 1–0 | Pick a tile |
| Left click | Place |
| Esc | Cancel or pause |
| Del | Sell |
| O | Toggle the HUD |
