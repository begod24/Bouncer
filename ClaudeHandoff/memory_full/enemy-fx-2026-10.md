---
name: enemy-fx-2026-10
description: "2026-10-07 enemy VFX/shader picks: cartoon fire horse, frost on body, elite glow by affix, volumetric waves, ram/landing dust, Dendy beam, boss extras — all in one pass"
metadata:
  node_type: memory
  type: project
  originSessionId: a91978bc-9a9b-4ed1-a279-e764018fb5df
  modified: 2026-10-07T13:51:48.524Z
---

User's picks (2026-10-07, after my enemy VFX review), build ALL at once («всё сразу»):
- Fire horse: «мультяшный огонь» — procedural toon-flame shader (2–3 stepped colour bands, no textures) in each fire spot, glowing scorch on the ground that cools orange → dark → gone (damage zone readable), rising sparks; burning mane + tail on the horse, sparks from the runners while ramming.
- Enemy bodies: frost crust on the body for the WHOLE freeze (was a 1 s blue flash); elites readable from afar — coloured contour/glow by affix (Swift/Commander/Catcher) + sparks at the feet.
- Not picked: wind-up glow on the body, spawn/death dissolve.
- Attacks: volumetric waves (cry, drum, shockwave, freeze, blast: thick ring with bright front + fading tail, own pattern each, dust/particles), rams & jumps (dust + speed lines for horse/RC car/Transformer, landing dust for frog and bosses, Fizruk smash cracks), Dendy beam (core, glow, muzzle flash, hit sparks), boss extras (Transformer siren red/blue glow on the ground, smoke from Babai's sack, carrot boomerang trail).

**Why:** user found the fire horse trail reads as a chalk line and wants enemies' VFX/shaders to match the new cartoon VFX pass.
**How to apply:** stay cartoon (see [[vfx-pass-2026-10]]), no new camera shake on routine events, hedgehog untouched; user playtests ([[user-does-playtests]]).
