# CLAUDE.md — Raccoon Heist

## What this game is

Raccoon Heist is a 1–4 player online co-op stealth-comedy game (fully playable solo). Players are raccoons
robbing a small corner shop at night while the owner — **Harold**, a fat, furious old
man in boxers and slippers — sleeps in the back room. Fill the night's shopping list,
drag the loot out through the vent before dawn, and do not wake Harold. When Harold
catches a raccoon, he physically grabs it and hurls it out the front door. Getting
yeeted is the punishment. It should always be funny.

Genre reference points: Lethal Company, Burglin' Gnomes, Content Warning, R.E.P.O.
Tone: chaotic, physical, clip-worthy. Failure is funnier than success.

## Core loop (one round ≈ 10–15 min)

1. Rounds start **outside**, at the den in the back alley. Case the joint, pick an
   entry. Entries are a menu of tradeoffs (speed / noise / what fits through):
   the floor vent (quiet, slow, small loot), the pet flap (opens from inside only),
   a creaky back window into the storage room (unlock from inside first), and a
   roof skylight (one-way drop in — never an exit).
2. A randomized **shopping list** demands specific items (some fragile, some heavy
   two-raccoon carries).
3. Players sneak, climb shelves, and carry physics-based loot to the ONE scoring
   point: a **hole in the floorboards of Harold's back room**, right by his cot,
   leading down to the den. Lure him out or be extremely silent. Staging loot
   quietly outside his doorway is allowed — someone still has to carry it in.
4. Raccoons can dive into the floor hole themselves: a LOUD, committal panic
   escape. You land in the den, safe but outside — the walk back in is the cost.
   Sneaking back out of the room is the optimal, terrifying play.
5. Noise wakes Harold in stages: **sleeping → stirring → suspicious patrol
   (flashlight) → rampage**. His snore is the all-clear signal; silence is terror.
6. Caught raccoons are grabbed and hurled across the street **into the bins**
   (lid clatter tells the whole team). The long walk back is the punishment; a
   teammate opening the pet flap from inside is the fast rescue. Solo play stays
   viable — re-entry is a time cost, never a lockout.
7. Dawn is the timer: the shop gradually brightens, and at 06:00 Harold's alarm
   rings and he gets up for good.
8. Surplus loot buys den upgrades between nights. Lists grow greedier each night.

## Design pillars

- **Diegetic signaling over UI.** No event popups. Information is sound
  (snoring, slipper footsteps, doorbell), light (dawn, flashlight beam, back-room
  lamp), and world events (a van's headlights raking the window). HUD is limited to
  the shopping list, carried item, and the player's own noise meter. If the game
  announces everything, players have nothing to tell each other.
- **Proximity voice chat is a core mechanic**, not a feature. Whisper-panic between
  friends is the product.
- **Physics comedy.** Loot, raccoons, and Harold's victims are physics objects.
  Identical animations + physics chaos = infinite variation.
- **Readable antagonist.** Harold is always present in the space, never spawns in.
  His states are telegraphed loudly enough to read from across the shop.
- **Small and dense.** One shop, one Harold, high interaction density. Add content
  post-launch, not pre-launch.

## Tech stack

- **Unity 6** (URP), C#
- **FishNet** for netcode — host-authoritative
- **Facepunch Steamworks** for lobbies/transport/invites
- **Dissonance** for proximity voice chat
- **ProBuilder** for greybox; final environment is a modular Blender kit
- Development on macOS (Apple Silicon); primary shipping target is Windows/Steam

## World scale and layout (fixed constants)

- 1 Unity unit = 1 meter. Snap the environment to a 1 m grid.
- Raccoon: ~0.5 m tall, jump ~1 m, crouches to 0.3 m (squeeze into crevices).
  Camera at raccoon eye height (~0.3 m).
- Harold: ~1.8 m tall.
- Main shop: **14 × 16 m**, ceiling 3 m. Back room (Harold's): **3 × 4 m**,
  connected by ONE doorway (the single tension point — do not add entrances).
- Storage room: **6 × 5 m** behind the shop's east side, open doorway (no door):
  tall stock racks, crate maze, single bare bulb. Loot-rich, hide-rich.
- Shelf units: 2 × 0.5 × 1.8 m with climbable boards; 0.5 m squeeze-gaps between
  units. Aisles: 1.2 m. Fridge case off the west wall (top is the slow-but-safe
  high route; gap behind it is a hiding slot). Ceiling beams at 2.4 m are the
  floor-free "raccoon highway" — Harold walks under them.
- Hiding spots are crouch-sized (0.3 m): under the cot, under the counter, behind
  the fridge, inside the crate maze. Every hiding spot must have >= 2 exits where
  feasible — hiding should feel tense, not safe.
- Vent entry at floor level near the front-east corner; counter + register near
  the front; Harold's cot in the back room. The **scoring hole** is in the back
  room's floorboards near the cot (raccoon-sized — divable, loudly).
- Outside: kept small — one street face (bright, exposed, streetlamp), a side
  passage with the vent, and a back alley (dark, cluttered: dumpster, pallets,
  bins). The **den** is in the alley (hole under a fence/dumpster) — spawn point,
  loot delivery target, and upgrade shop. Outside verticality follows the same
  <= 1 m jump-step rule: crates -> dumpster -> window sill -> roof. Yeeted
  raccoons land in the bins across the street.

## Art & audio direction

- Low-poly, flat-shaded, chunky proportions, muted warm palette. Dark shop lit by
  moonlight through the window + Harold's flashlight cone. Lighting is the star.
- Characters ~5–20k tris, props ~200–2k tris. Unlit/simple-lit materials, no PBR.
- Audio is gameplay: every Harold state and world event has a directional,
  in-world sound identity. Debug text is scaffolding only — every string gets
  replaced by a sound or light cue before it ships.

## Code conventions

- Scene layout is generated by **editor scripts** from the constants above —
  layout lives in code, not hand-placed boxes.
- Harold is a plain C# state machine driving an Animator + NavMeshAgent. States:
  Sleeping, Stirring, Suspicious, Patrol, Chase, Grab/Yeet, AlarmAwake.
- Every gameplay-feel value (noise radii, hearing thresholds, timers, throw force,
  walk speeds) is a serialized field or ScriptableObject — tuning happens in the
  inspector during playtests, never as magic numbers.
- Networking from day one: features are built host-authoritative on FishNet from
  the first commit. Carried items attach to the carrier (no free-ragdoll sync)
  until proven otherwise.
- Keep systems in `Assets/Scripts/<Domain>/` (Player, Harold, Loot, Noise, Net,
  World, UI). One behaviour per file.
