# Coal Piles

A lightweight Vintage Story mod that lets you neatly stack solid fuels in dense 3D ground piles—holding up to **128 items per block** without conflicting with vanilla coal pile mechanics.

![Coal Piles Demo](media/video/demo.webp)
![Coal Piles Demo](media/video/demo1.webp)
![Coal Piles Demo](media/video/demo2.webp)

## Features
- **High Capacity:** Store up to **128 items** per block (8 layers of 16 chunks) with scaling hitboxes.
- **6 Supported Fuels:** Charcoal, Bituminous Coal, Lignite, Anthracite, Coke, and Contaminated Coal (1.22+).
- **Fast Hold-to-Fill:** Hold the interaction keys to rapidly stream items into a pile.
- **Native 3D Visuals:** Uses each fuel's original 3D item model and textures, arranged cleanly from the center outward.
- **Gravity Support:** Piles drop down when the block underneath is removed (preserving fuel type and count).
- **Vanilla-Safe Controls:** Uses `Ctrl` so vanilla `Shift + Right-Click` coal piles stay intact.

## Controls

| Action | Input | Behavior |
| :--- | :--- | :--- |
| **Start a pile** | `Ctrl + Right-Click` on ground | Places 1 item |
| **Add (Single)** | `Ctrl + Right-Click` on pile | Tap for **1**, or **hold** (~12/sec) |
| **Add (Bulk)** | `Ctrl + Shift + Right-Click` on pile | Tap for **16**, or **hold** (16 / 0.15s) |
| **Take** | `Right-Click` / `Shift + Right-Click` | Removes **1** or **16** items |

> Place at least **1 item** with `Ctrl + Right-Click` before using bulk placement or hold-to-fill. Breaking a pile drops all stored fuel.

## Known Issues
- Aiming at the **side** of a pile while pressing `Ctrl + Shift + Right-Click` may trigger vanilla coal placement. Aim at the **top** of the pile for reliable bulk stacking.

## Installation
Drop the `.zip` file into your Vintage Story `Mods` folder and enable **Coal Piles** in the Mod Manager.

> **Warning:** 
Empty or break all piles before removing this mod, or stored items will become inaccessible.
**Tested on Windows only.**