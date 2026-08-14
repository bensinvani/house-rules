# Art reference for the Blender asset pass

AI-generated reference imagery (2026-08-14) on the game's Visual Quality Bar
palette (see docs/superpowers/plans/2026-08-13-blackjack-visuals.md).

- card-deck-reference.png   — deck style system: back + 4 aces + K/Q/J courts + pip layouts
- chip-set-reference.png    — 5 denominations, top + side + stack
- felt-layout-reference.png — print-style felt layout; basis for the felt texture
- table-hero-reference.png  — overall table look: rail, gold trim, chip tray, shoe

These are STYLE references, not final textures: rebuild card faces as clean
vector/procedural art from this system (AI pip counts are not trustworthy at
52-card scale). Nothing here may be referenced by a scene — reference imagery
must never ship in a build.

CAUTION: table-hero-reference.png is a materials/mood reference only — it shows
FIVE betting circles, which is wrong for this game. The layout authority is
felt-layout-reference.png (three circles, matching MaxBoxes = 3).
