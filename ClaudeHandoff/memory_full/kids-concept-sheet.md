---
name: kids-concept-sheet
description: "The 4 playable kids: concept sheet «Дети двора. 1990-е» (copy at projects/Bouncer_Kids_Sheet.webp), what each wears, and the user's 2026-09-25 decisions (identical stats, names, self-made Blender animations, select screen, shorts, one ранец, same height, props in select poses)"
metadata:
  node_type: memory
  type: project
  originSessionId: 60568610-4680-40d0-ac62-bcc6d8de635e
  modified: 2026-09-25T11:48:18.281Z
---

The AI-generated sheet «Дети двора. 1990-е» (the user re-sent it on 2026-09-25) is saved next to Bouncer.blend as `/Users/bekbolataldiyarov/Desktop/projects/Bouncer_Kids_Sheet.webp` (outside the repo). What each kid wears:
- Отличник (sheet: Умник): brown side-parted hair, thick black glasses, plaid shirt + olive/brown knitted vest, navy shorts, white socks, brown shoes, backpack; prop: book «Физика».
- Толстяк: round, sandy-brown hair, rosy cheeks, blue/white striped T-shirt, dark navy shorts, white socks, dark sneakers with white toes, backpack; props: chips, cola.
- Мелкая (sheet: Девочка): brown pigtails with red bows, white T-shirt with a bunny, red skirt (shorts in the T-pose), scraped knees, white knee socks, red sneakers; props: jump rope, plush bunny, hopscotch.
- Хулиган: navy cap with a red patch, navy tracksuit with white stripes, red T-shirt, sneakers, hands in pockets; props: football, slingshot.
Menu poses on the sheet: думает (Отличник) / радуется (Толстяк) / бежит (Мелкая) / готов к драке (Хулиган, arms crossed).

**Decisions of 2026-09-25** (the user chose «Дети вместо капсулы» as the next step after the playtested final; then two rounds of questions):
- Gameplay: kids differ ONLY by look. Stats and collider are the same for everyone (the user: «уникальности от карточек, которые берут в игре»). This overrides «stats + starting ball» from 24.09. My note: starting balls can become a separate pre-run choice unlocked with фантики later.
- Names: Отличник, Толстяк, Мелкая, Хулиган.
- Animations: I key them myself in Blender by script (the user picked this over Mixamo and over procedural code).
- Selection: a screen before the run, all 4 open; meta unlocks decided later.
- Look: Мелкая wears shorts (not the skirt); only Отличник has a (rigid Soviet) ранец, Толстяк has no backpack; props in the select poses (book «Физика», chips, jump rope, football). NOT picked: зелёнка, different heights (so all kids are the same height), a separate run cycle per kid (one shared run).
- Order: all four at once (not Хулиган first).

**Implemented on 2026-09-25 (not playtested yet):**
- Blender (Bouncer.blend, saved via CLI; backup of the pre-kids file was in the session scratchpad): text blocks `ba_kids.py` (4 kids + props + `export_kids()`, `export_rig()`) and `ba_kids_anim.py` (reference rig `Anim_Kids`, 19 actions, `export()`), both called from build_all.py. Collections `Kids` (rigs Kid_Otlichnik/Tolstyak/Melkaya/Huligan, Anim_Kids) and `KidProps` (Prop_Book «ФИЗИКА», Prop_Chips «ЧИПСЫ», Prop_JumpRope, Prop_Football; decals label_fizika/label_chipsy appended to Tools/decal_art.py).
- Rig: Hips, Spine (no Chest — Unity's auto-mapper skips it), Neck, Head, Left/Right Shoulder/UpperArm/LowerArm/Hand/UpperLeg/LowerLeg/Foot/Toes; T-pose; heights ankle .075, knee .33, hip .6, shoulder .965, head 1.06–1.43. One mesh per kid, each part 100% on one bone (shoulder balls sit on the Shoulder bones so Unity maps them). Poses are rotations in the rig's rest axes (`delta_quat`), so one clip set fits every kid; arms above ~100° need a shoulder shrug (Humanoid muscle limit).
- Clips (30 fps): Idle, Run_F/B/L/R (L/R = side gallop), Charge_Start/Full, Throw, Catch, Caught, Hurt, Dash, Slide, Down, Cheer, Pose_Think/Cheer/Ready/Tough.
- Unity: `Art/Models/Kids/*.fbx` import as Humanoid (ArtImportPostprocessor resets the avatar mapping every import; clips only from `Anim_*`, root motion baked into pose, loops by name), props in `Art/Models/KidProps`. Menu **Bouncer → Build Kid Animators** (Editor/KidAnimatorBuilder) rebuilds `Art/Animation/AC_Kid` (body layer: Locomotion 2D blend MoveX/MoveZ with speed param RunSpeed, Dash, Slide, Down, Cheer; UpperBody layer with mask AM_KidUpperBody: Empty, Charge 1D, Throw, Catch, Caught, Hurt) and `AC_KidSelect` in place.
- Code: `Player/KidDefinition` + `KidRoster` (Data/Characters/Kid_*.asset, KidRoster.asset), `Player/PlayerKid` (swaps the model in the Player's `Kid` slot from `GameSettings.Kid`, hand ball on RightHand, gives renderers to PlayerVisuals/HitFlash), `Player/KidAnimator` (parameters; dash turns the model), `UI/KidStage` (prefab Prefabs/UI/KidStage, spawned at y −500, RT camera), `UI/KidSelectScreen` + `KidSelectButton` (GameUI → Screens → KidSelectScreen, overlay `Kids` in RunScreens: «Играть» opens it, choosing starts the run). Capsule Body/FacingMarker removed from Player.prefab; placeholder Kid_Huligan tagged EditorOnly. Localization: Content kid.*.name/tagline (EN Brainiac/Chubs/Shorty/Hooligan — my defaults), UI kids.title/kids.hint.
- Concept doc and dev plan (stage 2.6) updated the same day.

**Why:** the user asked me to ask and clarify before building the kids.
**How to apply:** build to this; report my own defaults (proportions, colours, animation set) as changeable. See [[game-design-decisions]], [[art-pipeline-blender]], [[inscriptions-as-textures]].
