# Project (Preview)

Candidate project base from the current import understanding.
This document is not canonical truth yet.

## Identity

- Project Title: `cssDOOM-main`
- Import Kind: `MixedImport`
- Interpretation Mode: `SingleProject`
- Health: `Healthy`
- Truth Status: `Preview only / not canonical yet`

## What this looks like

- The project appears to be a JavaScript-based game or interactive application with assets, sound, and rendering components, possibly related to a Doom-like game or mod. truth=context_only.
- Asset files include images and sounds, with large JSON map files indicating level or map data, consistent with a game or level editor.
- Dependency edges show imports mainly within the src directory, especially in game and input modules, with assets referenced directly in code.
- The project contains a set of configuration JSON files named E1M1.json through E1M9.json, hinting at multiple levels or maps.
- The assets include sprites, weapons, and menu images, with specific assets like M_THERMM.png, CHAINGUN.png, and various sound files.

## Observed structure

- Source Roots: `.`, `src`
- Main Entry: Unknown
- Key Modules: `Audio` [Likely], `Game` [Likely], `Index.js` [Likely], `Input` [Likely], `Renderer` [Likely]

## Runtime / stack signals

- **Languages**: JavaScript

## What is confirmed / likely / unknown

- **Likely**: The project is related to a Doom-like game or mod, based on asset naming and map file conventions (based on indirect signals).
- **Unknown**: No explicit build system, frameworks, or runtime platform details are present in the evidence., The project is a JavaScript game or interactive application with modular subsystems and asset management (requires explicit file/symbol/dependency evidence).

## Materials worth reading

- `public\assets\menu\M_THERMM.png` [Confirmed] usefulness=High: Bounded image metadata prepared, used as a menu asset.
- `public\assets\menu\M_THERMO.png` [Confirmed] usefulness=High: Bounded image metadata prepared, used as a menu asset.
- `public\assets\menu\M_THERMR.png` [Confirmed] usefulness=High: Bounded image metadata prepared, used as a menu asset.
- `public\assets\menu\M_ULTRA.png` [Confirmed] usefulness=High: Bounded image metadata prepared, used as a menu asset.
- `public\assets\menu\TITLEPIC.png` [Confirmed] usefulness=High: Bounded image metadata prepared, used as a menu asset.

## Open uncertainty

- No explicit build system, frameworks, or runtime platform details are present in the evidence.
- The project is a JavaScript game or interactive application with modular subsystems and asset management (requires explicit file/symbol/dependency evidence).

## Canonical readiness

- First confirm target: `project.md`
- Derived companion after confirm: `capsule.md`
- Current preview looks bounded enough for explicit confirm, but it is still not truth until confirmed.