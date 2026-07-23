# AGENTS.md — Raccoon Heist (agent handoff notes)

Read **CLAUDE.md** first — it is the design doc and source of truth for game design,
world scale, layout constants, and code conventions. This file only adds the
practical things an agent needs to work in this project without breaking it.

## The generator owns the world

- The playable scene is built by `Assets/Scripts/World/Editor/ShopGreyboxGenerator.cs`
  via the menu item **Raccoon Heist → Generate Shop Greybox**.
- Regenerating DELETES the `ShopGreybox` and `Raccoon` root objects and rebuilds
  them from code. Anything hand-placed under those roots is lost on regenerate —
  never hand-edit the generated hierarchy; change the generator and re-run.
- The on-disk `SampleScene.unity` may look almost empty (camera/light/volume only):
  generated content exists only after running the menu item, and only persists if
  the scene is saved. This is intentional.
- All layout derives from `Assets/Scripts/World/ShopConstants.cs` (1 unit = 1 m).

## Asset pipeline conventions

- Synty prefabs are looked up BY NAME via `SyntyPrefab(name)` — it prefers the
  PolygonCity copy when both packs contain the same prefab name (they do: roads,
  trees). `PlaceSynty(name, floorPos, yaw)` grounds by measured mesh bounds and
  auto-adds a box collider if the prefab ships none. Everything degrades to
  greybox boxes if a prefab is missing — keep that fallback pattern.
- `SyntyReport.txt` (repo root, gitignored) holds measured bounds for all ~2,760
  Synty prefabs: `name|sizeX|sizeY|sizeZ|pivotToBottomY|centerOffsetX|centerOffsetZ`.
  Regenerate it via **Raccoon Heist → Dump Synty Asset Report**. Use it instead of
  guessing footprints/pivots.
- Emission policy: **Raccoon Heist → Reset Synty Emission** sets all Synty
  materials to a no-emission baseline. Night glow is applied PER INSTANCE by the
  generator through `MakeEmissive()` (creates `*_Emissive` material variants under
  `Assets/Materials/Greybox/Emissive/`). Do not blanket-enable emission on shared
  Synty materials — packs share texture atlases, so that turns on car headlights
  and shopfront glass everywhere.
- Procedural textures (brick/asphalt/planks/night sky) are generated as PNGs into
  `Assets/Materials/Greybox/Textures/` by `EnsureTex` — they are cached by NAME,
  so changing a texture's pixel function requires renaming it (e.g. `_v2`) or
  deleting the PNG.
- Post-processing lives in `Assets/Materials/Greybox/NightPost.asset`; the
  generator overwrites its overridden values on every regenerate. To make a tuning
  change permanent, bake it into `BuildAtmosphere()` in the generator.
- The environment deliberately separates **playable footprint** from **visual
  footprint**. Gameplay remains the compact front street/east passage/rear alley
  loop; `BuildVisualStreetRing()` and the backdrop rows continue streets around the
  other sides behind physical-looking fence/roadwork boundaries. Do not expand the
  CharacterController's playable area just because distant road geometry exists.

## Gameplay code rules (from CLAUDE.md, enforced)

- Every gameplay-feel value is a serialized field — no magic numbers.
- One behaviour per file, `Assets/Scripts/<Domain>/` (Player, Harold, Loot, Noise,
  Net, World, UI).
- Netcode: FishNet host-authoritative is the next planned milestone. The raccoon
  controller (`Assets/Scripts/Player/RaccoonController.cs`) is deliberately the
  ONLY gameplay behaviour so far, so the FishNet port stays one-file cheap. Avoid
  adding more pre-netcode gameplay systems.
- Harold's back room keeps ONE doorway. The scoring hole, entries menu, and
  yeet-to-bins rules are in CLAUDE.md's core loop — do not redesign casually.

## Current environment pass

- Lighting/atmosphere is under active iteration (screenshots in repo root are the
  reference workflow — judge changes against them).
- Global depth separation comes from exponential fog plus low-alpha, broad
  `FX_Fog_01` ground haze. Do **not** use `LightRay_Round_01` as localized
  volumetrics: from gameplay cameras its translucent geometry reads as a solid
  cone/slab. The ground-haze material disables the pack's eight-metre camera fade;
  steam remains localized and every plume must sit directly over a visible vent or
  manhole. Rain remains optional rather than forced into the game's weather direction.
- `BuildAtmosphere()` disables template global volumes and owns one priority-100
  `NightVolume`. Exterior street/alley lights must not use full dropouts: unstable
  world-scale flicker was the cause of the scene appearing to jump between two
  exposure states. The storage-room bulb may retain its subtle local wobble.
- Exterior light pools must originate at a visible lamp fixture and point away
  from its mounting surface. Keep neon glow lights short-range so colour accents
  do not flatten the whole block.
- The storefront's `RACCOON HEIST` title is assembled from measured POLYGON 3D
  letter prefabs in `BuildStorefrontBranding()`. The entrance is centered at
  `EntranceX`; its procedural glass, handle, and pet flap are children of the real
  `EntranceDoorPivot`. `HingedDoor` opens from both sides, but only a street-side
  opening triggers the shop alarm. `BuildUnifiedStorefront()` must stay one continuous frame/fascia fitted
  to the real openings. Do not stack modular shopfront prefabs, duplicate the door
  skin, place posts in front of signs, or add fake entrances in other bays.
- Exterior roads use damp procedural base materials with measured POLYGON road
  line tiles above them. Keep the base: it prevents visible holes at intersections
  and between prefab tiles.
- The east passage and rear alley use subdued outdoor asphalt, matte rough-concrete
  aprons, and dark gutters. Keep those surfaces low-gloss so they read as exterior
  paving under the night lights. Do not replace them with one broad paving plane. The visual
  street ring is deliberately populated with transit, delivery, refuse, planter,
  and street-furniture clusters; traffic lights are intentionally excluded. Keep
  curb fixtures in a loose repeated rhythm, concentrate larger props into small
  story clusters, and leave every carriageway and corner sightline unobstructed.
  Large furniture belongs on the building/fence edge and poles/meters on the curb
  edge; preserve a continuous empty walking strip through the middle of sidewalks.
  Benches face the adjacent road: default yaw on north-facing frontage, 180 degrees
  on south-facing frontage, 90 east-facing, and 270 west-facing.
- Environment review captures include dedicated `MainStreet`, `WestStreet`,
  `EastStreet`, `RearStreet`, `NeighbourFrontage`, and three `*OppositeWalk` angles in addition to
  storefront/playable-route views. Inspect all sides after moving vehicles or curb props.
  `ValidateVehicleBuildingClearance()` must remain in the generation path so a
  clear hero render cannot hide a perimeter overlap.
  `ValidateBackdropPropBuildingClearance()` must also remain enabled; apartment rows
  are aligned to their renderer facade edge because their prefab pivots are unreliable.
- The front road is intentionally a two-lane, ten-metre carriageway, but the far
  pavement is still the playable limit. Streetlight shafts sit on the far pavement
  with their arms over the road, and the central storefront sightline stays clear;
  keep future poles aligned to their actual fixture head and completely off the roof.
  Both main-street pavements use the same loose curb rhythm and clustered dressing as
  the side streets, but never place a prop in the centered entrance approach.
- Each visual perimeter road has a raised sidewalk on both sides. Opposite sidewalks
  are 4.5 m deep with their own curb-side lamps/meters, clear middle walking lane,
  and wall-side furniture. Keep the far apartment facades behind the slab, preserve
  the brighter curb caps, and review both edges in the dedicated street captures; a
  dark or empty slab is failed even if its mesh and props technically exist.
- Parked perimeter cars run parallel to the street and hug alternating curbs; do not
  leave them centered in a traffic lane. Every added vehicle must pass
  `ValidateVehicleBuildingClearance()` and appear in the relevant street capture.
- Do not place unsupported washing lines, clothes racks, or other hanging props in
  open alleys. Overhead dressing needs visible, credible anchors at both ends.
- The west neighbour's main-street face is a closed roller-shutter storefront. Keep
  its pavement clear: never put a bench, bin, or ground-level fire escape against that
  shutter. `ValidateNeighbourFrontageClearance()` enforces both the wall bounds and
  an empty apron in front of it.
- Exterior city ambience comes from `Assets/Audio/Environment/city_ambience.mp3` as
  one looping, non-spatial bed. `ExteriorAmbienceController` fades it across the main
  shop, back-room, and storage-room thresholds while leaving the roof and all outside
  routes audible. Do not replace it with a single point source in the street.
- Unused feature candidates measured and available: subway entrances, city hall,
  rooftop access pieces, shop covers 01/02/04/05.
