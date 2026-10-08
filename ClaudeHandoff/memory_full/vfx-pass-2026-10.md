---
name: vfx-pass-2026-10
description: "2026-10-07 VFX/shader pass: user decisions (cartoon, rim by mask, particles atlas, impact frames, filling telegraphs, chalk trail, better rain; hedgehog untouched, no hitstop) + what was built and where it lives"
metadata:
  node_type: memory
  type: project
  originSessionId: 7627ffad-73dd-40e0-bc7e-284416782b35
  modified: 2026-10-07T13:25:34.337Z
---

User's picks after my VFX/shader review (2026-10-07):
- Style: stay **cartoon** («оставим мультяшное») — do NOT push PS1 further (no dithered particle alpha, no pixel outline, no toon bands).
- Light rim light in PaletteLit by mask, only characters + balls.
- A particles atlas, B impact frame + perfect-catch effect (user dropped the micro-hitstop — don't add hitstop), C filling telegraphs + stepped trail, better rain.
- **Hedgehog VFX stays as is** (Ball Glow/SpikySparks/Shards, SpikeGlow, spikyWarning untouched; rim is switched off on a spiky body in BallVisuals). The new ball trail material applies to every ball, hedgehog's trail included.
- Not picked: wind, pixel outline, toon ramp. (Wet asphalt got added as part of «rain».)

Built same day (uncommitted, not playtested):
- Textures: `Tools/vfx_art.py fx` → `Art/VFX/T_FxAtlas.png` (8×8 cells of 128 px; rows: puff 8 frames, stars, paper+squares, feathers, straw, fluff, splinters 0–3 + drops 4–7, misc = streak V/H, splash 4 frames, soft disc, ring), `T_Impact.png` (4 frames), `T_TrailChalk.png`. Row indices are duplicated in `Scripts/Editor/VfxPassBuilder.cs`.
- Shaders `Art/Shaders`: `Particle.shader` (+`BouncerFx.hlsl`: fog, radial fog, global `_Bouncer_FxTint`; ground fade; additive toggle; `_ZTest`), `Telegraph.shader` (circle/line fill by `_Progress`), `Trail.shader` (stepped alpha, white core), `Puddle.shader` (procedural ripples, soft edge via mesh UV.x). PaletteLit: rim when the renderer has rendering layer bit 7 «Rim», wet darkening + sun glint by `_Bouncer_Wet` on non-rim up-facing surfaces.
- Code: `Core/FxGlobals.cs` (defaults, MarkRim/SetRim), TimeOfDayController.ApplyFx (tint, rim colour/strength, wet), `Player/ImpactFrames.cs` on Player.prefab (Health.AnyDamaged static event, PlayerFxKind.PerfectCatch from PlayerController.OnCaught), GroundMarker `fill` renderer, Puddle mesh UVs, PlayerKid/KidStage mark kid models.
- Menu **Bouncer → Art → Apply VFX Pass** (`VfxPassBuilder`, rerunnable): materials `Art/Materials/Fx/*`, particle styles table (by «Prefab/System»), prefabs `Prefabs/VFX/ImpactStar(_Player)/PerfectCatch/PerfectRing`, telegraph Fill children, Weather rain (collision with a GroundPlane child → splash crowns + ground ripples as Collision sub-emitters, because death positions overshoot under the ground by up to 0.4 m), ball trails, rim bit on Enemies (not *Debris) + balls, TagManager layer 7 = Rim. CardContentBuilder.BuildAll calls `VfxPassBuilder.StyleAllParticles()` at the end.
- Preview gotcha: in edit-mode `Simulate`, sub-emitter children keep zero renderer bounds and get culled — call `child.Simulate(0.02f, false, false)` before rendering. Don't touch `RenderSettings` in preview scripts (it's the open scene's; I did once and restored it via TimeOfDayController.Apply()).

**Why:** user wants a cartoon look, readability, and juicier feedback without changing timing.
**How to apply:** new particle systems → add a line to `Styles` in VfxPassBuilder and rerun; see [[art-pipeline-blender]], [[cards-2026-10]], [[user-does-playtests]], [[unity-mcp-quirks]].

Follow-up after the user's playtest (2026-10-07, «всё играется отлично»):
- Camera shake REMOVED from the throw loop (user: throwing is constant, shake would annoy): ball hits on enemies (every ApplyHit), regular enemy deaths, catches (PlayerController.OnCaught), Ball Blast/BounceBlast/LobLand, SwingSet. Kept: player hurt/second wind/tamagotchi/lid, boss deaths + BossSplit, boss/enemy attack impacts (slams, rams, explosions), thunder, props breaking, cap gun. Don't add shake to routine hit/catch events again.
- Puddles: fewer/weaker drop ripples (`_RippleDensity` = share of cells with drops, cell 0.95 m, bend 0.4, rings alpha 0.14), outline = smooth blob (36 points, sum of sines), edge «breathes» in the shader (`_EdgeWobble`, lobes seeded by world position), whole puddle grows/shrinks by Perlin noise in `Puddle.cs` (breathe 0.3, ~8 s; GroundZone scales with it).
- Open: fire horse VFX (FireSpot is a flat Sprites/Default doodle → reads as chalk) and enemy VFX/shaders overall — proposals asked, see the next memory once answered.
