---
name: phases-v-g-decisions
description: "2026-09-24 user answers for phases В (Rink + Fizruk) and Г (Site + dusk elites), the 13 new cards and weather (rain+thunder, fog as random arena events)"
metadata:
  node_type: memory
  type: project
  originSessionId: 5902e1f4-01b2-489d-ba70-0c7a64580b84
  modified: 2026-09-24T19:53:12.010Z
---

Asked on 2026-09-24 before building phases В and Г (the user said «начни делать эти этапы»):

**Fizruk-mannequin (Rink boss):** whistles every ~12 s, a rule of the round lasts ~8 s (chalk banner + whistle). The user took all four rules: «Замри!» (everyone freezes 3 s, enemies too; whoever runs gets a strong ball from Fizruk; throwing is allowed), «Штрафной!» (fan of 5 catchable balls), «Мяч в игре!» (giant ball ricochets off the boards and knocks everyone, enemies too), «Замена!» (subs run out from the bench: tin soldier line or a pair of mannequins). Fizruk himself ignores the gaze rule («Нет, он босс»): he always walks, hits up close and throws strong balls in between.

**Site end:** new dusk elites with their own look and ability (user picked this over reusing the toy elites) — elite mannequin, scarecrow and shadow need new models in Bouncer.blend.

**Shadow:** the player's small light circle is NOT light; the shadow becomes solid during its attack wind-up (~0.5 s) anywhere, and is always solid under a lamp.

**Cards:** in the concept only «длина рывка» had no card → «Скакалка» (common, dash +30% length, −20% cooldown, ×3). The user then took all four packs I proposed: Против сумерек (Зеркальце — mannequins also freeze in a narrow cone behind you; Фонарик — bigger light circle, shadows in it are solid; Свисток — perfect catch freezes enemies within 6 m for 1.5 s), Выживание (Второе дыхание — once per run stay with 1 heart instead of being knocked out; Бабушкины пирожки — +1 heart at the start of each arena; Резиновые сапоги — sand/gum/puddles don't slow the player), Комбо (Горячая картошка+Попрыгунчик → Прыгающая бомба; Теннисные+Раздвоение → Град; Набивной+Хулиганство → Гиря, area hit stuns; Подкат+Скакалка → Кувырок, dashing through an enemy ball catches it), Ларёк (Шпаргалка — first reroll at each kiosk free; Счастливый фантик — portfolio/boss offers 4 cards instead of 3).

**Weather:** rain + thunderstorm (wet palette, puddles stop balls and make enemies slip, lightning lights the whole arena for a moment) and fog (~12 m visibility). Snow and wind were not picked. Weather is a random arena event (~30% chance, announced at the start), like the concept's random yard events.

**Implemented on 2026-09-25; the user playtested it the same day after the final was committed («я все проверил и работает»):**
- Scenes `Rink.unity` and `Site.unity` are copies of Yard with their own NavMesh assets (`Scenes/Rink|Site/NavMesh-Arena.asset`); Run_Default = Arena_YardMorning → Arena_Rink → Arena_Site (Site is the last arena until phase Д; Arena_YardEvening/Wave_YardEvening are out of the run, kept for the final).
- Rink: `Arena/Boards` = 36 box colliders along the ba_rink outline (40×26, R6) with `Balls/RicochetSurface` (speed 1.0, free ricochet, keeps height, 12° aim assist for player balls); `ArenaSpot` Bench/BallGate at both gates; floodlight prefabs with `Visuals/LampLight` (turn on with the palette emission).
- Site: `Core/DarkArena` + TimeOfDay_SiteDusk/SiteNight; lamps (`Prefabs/Site/*`) carry `Core/LightZone` (shadow is solid inside) + `LampLight`; player's `PlayerLantern` circle is visual only.
- Enemies: `MannequinEnemy`, `ScarecrowEnemy`, `ShadowEnemy`, `FizrukBoss` (+ `GiantBall`), freeze via `Targetable.Freeze/FreezeEnemies/FreezeAround` (all older enemies pause when `IsFrozen`), subs through `GameEvents.RequestSpawn` → `WaveSpawner.QueueGroupAt`, rule banner via `GameEvents.Announce` → `UI/RuleBanner`. Dark-only spawns: `Core/SpawnPreference`.
- My defaults for the dusk elites (user only picked "new dusk elites"): Elite_Mannequin «из универмага» (red dress, hat, pearls; throws a strong ball at your back when unwatched), Elite_Scarecrow «в ушанке» (vatnik, glowing eyes; catches charged balls, holds 2, throws strong), Elite_Shadow «в шляпе» (puts out lamps for 6 s). Models from Blender text block `ba_dusk_elites.py`.
- Cards: StatCard fields mirrorAngle/lanternRadius/whistleFreeze/secondWind/arenaHeal/ignoreGround/dashCatch/freeRerolls/extraChoices, BallPerks bounceBlast*/twinSplit/areaStun; `RunState.SecondWindUsed`; UpgradeScreen has a 4th card view. Rarity split is in the concept doc (12/13/7 + 5 combos, deck 37).
- Weather: `Visuals/WeatherController` (Weather prefab in all 3 scenes) + `Puddle`; fog is player-centred in the palette shader (`MixRadialFog`, globals `_Bouncer_Fog*`), rolled by `ArenaDirector` from `ArenaDefinition.weatherChance/weathers`.
- New synthesized SFX (Tools/sfx.py `sfx_dusk()`): Whistle, Thunder, MannequinPose, ShadowHiss, ScarecrowCatch, LampOut, SecondWind, RainLoop.

**Why:** these settle the open design points of phases В/Г so the code is right the first time.
**How to apply:** build to this spec; my own defaults (elite dusk looks/abilities, numbers) should be reported to the user as changeable. See [[solo-run-decisions]], [[game-design-decisions]], [[assets-status]].
