# Changelog

## [1.1.0] - 2026-11-26

### Added
- **Infinite Vertical Stacking:** Continuing to add fuel to a full pile (`Ctrl + Right-Click` or `Ctrl + Shift + Right-Click`) now automatically starts and fills new piles stacked directly upward.
- **Anthracite Support:** Added Anthracite (`ore-anthracite`) as a 6th supported fuel.
- **Gravity Support:** Piles now fall straight down when the block underneath is removed, preserving fuel type and count (or dropping all items if landing on a non-solid block).

### Changed
- **10-Layer Full-Block Capacity:** Increased maximum pile capacity from 128 items (8 layers) to **160 items (10 layers)** with exact 1.0-block layer spacing and hitboxes so stacked piles sit flush without overlapping or gaps.

### Fixed
- **Linux/macOS Archive Compatibility:** Switched `.zip` packaging from PowerShell to 7-Zip (`7z`) so archive directory paths use forward slashes (`/`).

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