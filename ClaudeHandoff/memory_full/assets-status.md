---
name: assets-status
description: "Which 3D assets exist for the solo-first plan (as of 2026-09-24) and what still has to be done with them in Unity (prefabs, code, arena scenes)"
metadata:
  node_type: memory
  type: project
  originSessionId: 8fc451fd-58fa-4436-a80f-0a593fd56e9f
  modified: 2026-09-24T15:14:58.210Z
---

Made on 2026-09-24 (models exported to Unity, nothing wired up yet — no prefabs, no gameplay code):
- Stage 1: Prop_Kiosk («СОЮЗПЕЧАТЬ»), Pickup_Coin_1/5 (тиын) + Pickup_Coin_Tenge, Pickup_Portfel, Item_Gum_Common/Rare/Gold, Item_Lemonade, Item_Sandwich; Enemy_Bear (Bear_BallSocket empty for the stuck ball), Enemy_Chick (Chick_Key spins), Enemy_Top, Enemy_RockingHorse; Elite_RolyPoly/Pupsik/TinSoldier/Bear/Chick/Top/RockingHorse; palettes + TimeOfDay_Evening/Dusk/Night profiles; UI: HUD_Coin (₸ chalk doodle in Tools/ui_art.py, seed 10), Icon_Item_Gum_*, Icon_Item_Lemonade, Icon_Item_Sandwich, Icon_Portfel.
- In advance: Enemy_Mannequin, Enemy_Scarecrow, Enemy_Shadow, Boss_Fizruk, Boss_Dusk; hockey box (Env_RinkGround/Boards 40x26 R6, Rink_Goal, Rink_Floodlight, Rink_TeamBench, Rink_Board_2m(_Graffiti)); construction site (Env_SiteGround + 21 Site_* props incl. ПО-2 fence, cabin with lit window, fire barrel, crane, frame).

Wired up on 2026-09-24 (phases A+Б): prefabs with behaviour for bear, chick, top (+ Top_Mini), rocking horse and all 7 elites, coin/portfolio/kiosk/exit prefabs (`Prefabs/Run`), kiosk placed in the Yard by the north wall (x -6, z 13.85, facing the camera), exit arrow at the east wall. Still not done: Rink and Site scenes (NavMesh, spawn points, waves), behaviour of mannequin/scarecrow/shadow and both bosses, shadow smoke VFX, kids' models (capsule by the user's decision).

Wired up on 2026-09-25 (phases В+Г): prefabs Mannequin/Elite_Mannequin, Scarecrow/Elite_Scarecrow, Shadow/Elite_Shadow, Boss_Fizruk, GiantBall (+ debris prefabs), VFX ShadowSmoke/ShadowPoof/Weather/Puddle, lamp prefabs in `Prefabs/Rink` and `Prefabs/Site`, scenes Rink and Site. New models: Elite_Mannequin, Elite_Scarecrow, Elite_Shadow.

Made on 2026-09-25 (phase Д): Boss_Dusk rebuilt as Бабай with a sack (text block `ba_final.py`, parts Dusk_Body/Head/Eyes/ArmL/ArmR/Staff/Sack/CrowL/CrowR; the old scarecrow ×3 builder was removed from ba_bosses, `_crow` stays there for ba_dusk_elites) and Enemy_Crow (Crow_Body, Crow_WingL/R, origin at the body centre). World textures T_BlobShadow and T_MomSilhouette come from `Tools/vfx_art.py` (Art/VFX). Still not done: kids' models.

Made on 2026-09-25: the four playable kids + skeleton, clips and select-screen props — see [[kids-concept-sheet]]. The player is no longer a capsule.

**Why:** the user asked for all stage-1 assets plus the hockey box, dusk enemies, bosses and the construction site made ahead of the code.
**How to apply:** check this before modelling anything new, and reuse these assets when writing the stage-1 gameplay. See [[game-design-decisions]], [[art-pipeline-blender]], [[inscriptions-as-textures]].
