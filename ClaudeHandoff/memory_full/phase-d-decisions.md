---
name: phase-d-decisions
description: "2026-09-25 user answers for phase Д: final «Мама зовёт домой» (moonlit night Yard, boss-only duel, run to the подъезд) and the boss «Тот, кто в сумерках» (8 abilities, Бабай look, windows+clock timer)"
metadata:
  node_type: memory
  type: project
  originSessionId: 1f16c920-64d1-45b9-9493-1278b51f03a9
  modified: 2026-09-25T07:37:31.711Z
---

Asked on 2026-09-25 when the user said «теперь что делаем» after phases В/Г were committed (В/Г playtest status unknown). I proposed phase Д and ran two rounds of questions.

**Final arena:** our Yard at night, ~3 min. Moonlit night (whole arena visible but dim, street lamps and panel-block windows lit); real darkness only when the boss puts the lights out. Only the boss is on the arena, plus what he summons himself (no enemy waves).

**Timer to mom's call:** windows in the panel blocks light up one after another, ours is the last; plus a small chalk clock on the HUD.

**Mom's call:** mom shouts from the window, the подъезд door lights up; the player must run to it through the enemies (~10 s), the door's light repels the dusk creatures. Reached → victory; knocked out on the way → the run is lost.

**Boss «Тот, кто в сумерках» — all 8 abilities the user picked** (my 4 first proposals, then «еще придумай варианты» → 4 of 8 more):
- «Ловец»: catches every ball from the front (charged too) and answers with a fan of strong balls; hit him from the sides/back, he turns slowly.
- «Мешок»: collects balls lying on the ground into the sack on his back; a hit in the sack (from behind) spills them out.
- «Прятки»: leaves into the dark, several scarecrows stand up around the yard; the real one gives itself away by glowing eyes, a fake one bursts into straw and a flock of crows when hit.
- «Гасит свет»: a wave of his arm puts out lamps and windows for a few seconds; shadows come out in the dark.
- «Прыжок на шесте»: vaults across the yard, the landing hits in a ring (dash out); after landing he stands ~1 s with the pole stuck in — hittable from any side.
- «Карусель»: spins on the pole and throws balls in a spiral; all catchable, a perfect catch heals (main heart source in the duel).
- «Считалочка»: counts «Раз, два, три, четыре, пять…» — hide behind a garage/bush/bench before the count ends, whoever is visible gets a volley of strong balls.
- «Вороньё»: a flock of crows dives along straight lines (their shadows show on the ground first); any ball knocks a crow down; a crow can steal a ball from the ground.
Not picked: «Длинные руки», «Простыни», «Длинная тень», «Ложный зов».

**Look:** «Бабай с мешком» — very tall and thin, long black coat, hat, burlap-sack head with a stitched smile, glowing eyes, a sack on the back, crows on the shoulders (replaces Boss_Dusk = scarecrow ×3).

**Implemented on 2026-09-25; the user playtested it the same day after the final was committed («я все проверил и работает»):**
- Run_Default = YardMorning → Rink → Site → Arena_Final (sceneName Yard, goal SurviveUntilCall, Wave_Final: 180 s, only the boss burst at 1.5 s, no kiosk/portfolio/weather, TimeOfDay_YardNight + its own TimeOfDay_YardNight_VolumeProfile). Site's exit now reads «Домой →».
- Boss: `Enemies/DuskBoss` + `DuskBossDefinition` (Enemy_DuskBoss.asset; 120 hits, phases by HP 70/35% or fight time 55/115 s; per-phase triples as Vector3/Vector3Int), `BossSack` (sack collider on Dusk_Sack/SackHit), `CrowEnemy`/`CrowDefinition`, `ScarecrowDecoy`. Prefabs Boss_Dusk, Decoy_Dusk (eyes off), Crow, DuskDebris; VFX StrawBurst, FeatherPuff, LandingDust, GroundMarker_Circle/Line (`Core/GroundMarker`).
- Core: `LightsOut` (lights-out event; DarkArena.Active includes it; TimeOfDayController dims moon/ambient/emission by Dark01), `CoverVolume` (LOS-only boxes for «Считалочка»), `LightZone.repelsDusk` + Repels/PushOutOfRepelling, `LightZone.Relight`, `GameEvents.MomCalled`, `Targetable.HiddenFromAim`, 13 new SoundCues (sfx.py `sfx_final()`).
- Final flow: `Run/HomeCall` on Yard's `NightFinale` (FinaleOnly children: window overlays + `Visuals/WindowClock`, 4 street-lamp lights with LightZone, CoverVolumes at both swings, HomeLight with door glow + repelling zone, GoalArrow = ArenaExit instance, PathHome points; hides Prop_Garage_C at x 7.6 to open the gap). ArenaDirector.Complete on the last SurviveUntilCall arena calls HomeCall.BeginCall instead of Win; reaching the goal → PlayerController.BeginScripted + walk to the door → Win.
- HUD: `UI/CallClock` in GameUI.prefab (HUD_Panelka + HUD_WindowLit/Dark from ui_art.py), GameHud timer counts down in the final. ArenaDefinition.introHint shows under the arena title.
- To test the final directly: set ArenaDirector.defaultArena = 3 in the Yard scene.

**Why:** settles phase Д so the code is right the first time.
**How to apply:** build to this; my own numbers (HP, phase split of the 8 abilities, timings) must be reported to the user as changeable. See [[phases-v-g-decisions]], [[solo-run-decisions]], [[game-design-decisions]].
