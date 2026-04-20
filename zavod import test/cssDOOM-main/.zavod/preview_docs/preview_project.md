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

- Import material interpretation prepared: import=MixedImport, sourceRoots=2, materials=12, observations=98, patterns=9, signals=11, languages: JavaScript, build: package_json, entry candidates: 1, modules=3, entries=1, entry candidates: 1, module candidates: 3, truth=context_only.
- It uses CSS transforms and custom properties to position walls, floors, and sprites in 3D space based on DOOM's level geometry from WAD files, with a JavaScript game loop managing state, input, and AI.
- Assets include numerous PNG images for HUD elements, textures, and map data JSON files, with large map files indicating asset-heavy levels.

## Observed structure

- Source Roots: `.`, `src`
- Main Entry: `index.js` [Unknown]
- Key Modules: `Audio` [Likely], `Game` [Likely], `Index.js` [Likely]

## Runtime / stack signals

- **Languages**: JavaScript
- **Build Systems**: package_json

## What is confirmed / likely / unknown

- **Likely**: It is a full game engine with a custom CSS-based renderer, input handling, and map loading, based on the evidence of module candidates, code edges, and assets.
- **Unknown**: The exact game state management and AI logic are not explicitly detailed in the evidence., The project is a CSS and JavaScript implementation of DOOM for web browsers (requires explicit file/symbol evidence).

## Materials worth reading

- `README.md` [Likely] usefulness=Medium: Describes the CSS rendering approach, level geometry construction, and input handling, providing a detailed project overview.

## Open uncertainty

- The exact game state management and AI logic are not explicitly detailed in the evidence.
- The project is a CSS and JavaScript implementation of DOOM for web browsers (requires explicit file/symbol evidence).

## Canonical readiness

- First confirm target: `project.md`
- Derived companion after confirm: `capsule.md`
- Current preview looks bounded enough for explicit confirm, but it is still not truth until confirmed.