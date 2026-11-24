# Changelog

## [1.1.0] - 2026-11-24

### Added
- **Anthracite Support:** Added Anthracite (`ore-anthracite`) as a 6th supported fuel.
- **Gravity Support:** Piles now fall straight down when the block underneath is removed, preserving fuel type and count (or dropping all items if landing on a non-solid block).

### Fixed
- **Authentic Item Textures:** Updated pile rendering to use each fuel item's native shape textures and UV mapping, matching the exact appearance of dropped items.

## [1.0.0] - 2026-11-22

### Added
* **Dense 3D Coal Piles:** Stack up to **128 items per block** (8 layers of 16 chunks) on any solid ground surface.
* **5 Supported Fuels:** Charcoal, Bituminous Coal, Lignite, Coke, and Contaminated Coal (Vintage Story 1.22+).
* **Original 3D Visuals:** Renders native game item models (scaled to `0.9x`) with their original textures, arranged deterministically from the center outward with 90° rotation per layer.
* **Dynamic Hitboxes:** Collision and selection boxes scale vertically as each layer fills.
* **Vanilla-Safe Controls & Hold-to-Fill:**
  * `Ctrl + Right-Click` on ground to start a pile (1 item).
  * `Ctrl + Right-Click` (tap or hold) on a pile to add 1 item continuously (~12 items/sec).
  * `Ctrl + Shift + Right-Click` (tap or hold) on a pile to add 16 items continuously (every 0.15s).
  * `Right-Click` / `Shift + Right-Click` to take 1 or 16 items.
  * Breaking a pile drops all stored fuel.