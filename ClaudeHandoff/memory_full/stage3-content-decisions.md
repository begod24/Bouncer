---
name: stage3-content-decisions
description: "Stage 3 (content) of the post-1.0.1 update: 2026-09-27 form answers for the new arenas Барахолка/Детсад, their enemies, features and bosses (Трансформер = police Zhiguli, plush hare); 8 new enemies total"
metadata:
  node_type: memory
  type: project
  originSessionId: fc434d48-58b6-4d09-853a-5049fb14e684
  modified: 2026-09-27T11:09:37.484Z
---

Stage 3 = content, the last step of patch 1.0.1 → balance → content (see [[feedback-update-decisions]]). On 2026-09-27 the user started it in a separate session while another session did stages 1+2 in parallel (same Unity editor and the same GUI Blender).

**Form answers (2026-09-27, my ★ defaults were preselected):**
- Барахолка (evening, fork alternative to Коробка): enemies = yard toys + the new Rink ones (shield soldier, drummer, wind-up frog) + two MORE new enemies moved up from «later»: Машинка на пульте (fast ram with a skid) and Пистолет от «Денди» (laser line, then a shot). Features: rows of stalls (cover, balls bounce), carts with goods (a ball pushes them, they roll and knock enemies), piles of boxes (a strong ball topples them, the pile stuns). NOT carpets on ropes.
- Трансформер из ларька (Барахолка boss): looks like police «Жигули» (not the red sports car). Rams as a car, shoots as a robot, plus: ramming smashes stalls, battery mines, summons RC cars.
- Детсад (dusk, fork alternative to Стройка): enemies = toys (pupsiks, roly-polys, bears, rocking horses) + ballerina and lunokhod + shadows + a new Кукла-плакса (cries: calls pupsiks, the cry slows). Semi-dark like the Site after the patch. Features: spinning carousel (carries the player, enemies and balls), rocket slide (tall cover), verandas (roof against candles, a lamp inside). NOT the sandbox.
- Большой плюшевый заяц (Детсад boss): jump slams + stuffing clouds that slow, plus: the seam rips at half HP, a carrot boomerang, an ear whip up close. NOT riding the carousel.

So stage 3 = 8 new enemies (Rink: shield soldier, drummer, frog; Site: ballerina, lunokhod; Барахолка: RC car, Dendy gun; Детсад: crying doll) + 2 arenas + 2 bosses + the fork. Спортзал with the skeleton boss and the other «later» ideas stay later.

**Progress (2026-09-27, evening): stage 3 implemented in Unity, not playtested (user playtests).**
- Blender: `ba_toys2.py`, `ba_bosses2.py`, `ba_bazaar.py`, `ba_kindergarten.py` in build_all.py; FBX in Art/Models/Enemies, /Bazaar, /Kindergarten. Transformer = robot FBX + `Boss_Transformer_Car.fbx` (same part names, rotated nodes); `TransformerRig` stores both poses (captured by part name) and lerps each part in its own window.
- Enemies (Scripts/Enemies, prefabs built by the editor helper `Editor/EnemyPrefabKit.cs`): ShieldSoldier, Drummer, Frog, Ballerina, Lunokhod (added to Wave_Rink / Wave_Site), RCCar, DendyGun, CryDoll. Bosses: `TransformerBoss` (+ TransformerRig, BatteryMine, RC-car summons, rams break `IBreakable` stalls/box piles and fling carts; announces «Он ещё и робот!» on the first transform) and `HareBoss` (+ SlowCloud, CarrotBoomerang, StuffingBurst; rip at 50% HP with «Шов разошёлся!»). Both use BossArmor + BossSplit (bossName Content boss.transformer / boss.hare) and enter from `ArenaSpot` kind `BossEntrance` (new enum value).
- Arena props: Scripts/Arena BreakableProp, PushCart, TopplePile, SpinningCarousel; Core/IBreakable. Scenes Bazaar.unity and Kindergarten.unity (copies of Site; own NavMesh assets) are in Build Settings.
- Fork: `RunDefinition.forks` (stage → other arena; variant 0 = main), `RunState.ArenaVariant/NextVariant`, `ArenaDirector.forkExit` + `LeaveArena(variant)`, `ArenaExit.Show(text, variant)`. Second chalk arrow «ArenaExit_Fork» in Yard, Rink and Bazaar. Run: Yard → Rink|Bazaar → Site|Kindergarten → Final.
- Data: Arena_Bazaar (evening→dusk, DefeatBoss, hits ×1.5), Arena_Kindergarten (SiteDusk→SiteNight, DefeatBoss, hits ×2), Wave_Bazaar, Wave_Kindergarten (bosses at 270 s). UI/Content strings added for ru+en only (kk falls back to ru; session A's Tools/Localization CSV flow).
- SFX: `sfx_content()` in Tools/sfx.py (own RNG seed) → Siren, TransformClank, PlushThump, SeamRip, DollCry, DrumBeat, WindUp, ZapperShot, EngineRev; SoundCue values 57–65 appended at the end + SoundBank entries.

**Why:** these settle the stage-3 design so the models and code are right the first time.
**How to apply:** build to this; report my own defaults (looks, numbers, layouts) as changeable. See [[feedback-update-decisions]], [[art-pipeline-blender]], [[propose-before-changing-design]].

**Verandas closed to balls (2026-09-28):** the user asked «чтобы в веранды не залетали мячи» and picked «стенка только для мячей» (over closing the veranda for everyone or a one-way bunker). New layer 11 `BallWall` (collides only with Ball in the physics matrix; NavMeshSurface bakes only Environment, so no rebake); child `BallWall` BoxCollider across the whole open front of Kg_Veranda / Kg_Veranda_B (center 0,1.45,1.52, size 6.2×2.9×0.2). Code: `Layers.BallWallMask`, `Layers.BallSolidMask` (Environment|BallWall), `Layers.IsBallSolid`; LiveBallMask, Ball sweeps/area/graze masks, PlayerBallHandler.SafeOrigin and AimIndicator use BallSolidMask. Player and enemies walk in; balls bounce off both ways (a player inside is safe from balls but can't throw out). Enemy LOS checks still use EnvironmentMask (they may throw at a player inside and hit the wall).
