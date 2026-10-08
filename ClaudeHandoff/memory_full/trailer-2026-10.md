---
name: trailer-2026-10
description: "35–38 s marketing trailer «Мама зовёт домой» — user decisions 2026-10-07 + how it is produced (Assets/_Trailer puppet/recorder, Blender kid intros + end card, edit.py on the SFX_Music4 beat grid)"
metadata:
  node_type: memory
  type: project
  originSessionId: f820830a-a4f1-413d-9c37-928eb1812c7d
  modified: 2026-10-07T20:07:01.229Z
---

On 2026-10-07 the user asked for a 30–40 s ad video to start marketing. Approved concept «Мама зовёт домой», 16:9 (9:16 cut-downs later):
- Intro (added by the user mid-task): Neon Yurt studio splash from `Assets/_Project/Art/UI/Branding/NeonYurtBanner.png` (neon flicker + synthesized hum). End logo `Splash_Logo_EN.png` + chalk «СКОРО / COMING SOON» (no links).
- Sunset yard, ball rolls, Хулиган picks it up — chalk «— Саша, домой!»; four kids from behind vs spawning toys — «— Ещё 5 минуточек!»; DROP → four Blender kid intros with names (RU+EN from the localization: Brainiac/Chubs/Shorty/Hooligan); КИДАЙ/ЛОВИ/ВЫЖИВАЙ/ВМЕСТЕ 1–4; НОВЫЕ ДВОРЫ (rink, bazaar, site fire horse, KG); bosses (roly-poly split, Transformer ram, Бабай «Тот, кто в сумерках», slow-mo catch); final «МАМА ЗОВЁТ ДОМОЙ!» run to the подъезд; Blender end card.
- Titles RU + EN together. NO voice (music + game SFX only). The user also asked to use Blender renders/animations, not only Unity.
- **Play Mode exception:** the user allowed Play Mode ONLY to record trailer footage, not to test the game.

How it is built (2026-10-07):
- Unity tooling in `Assets/_Trailer` (asmdef Bouncer.Trailer, defineConstraints UNITY_EDITOR → not in builds; user decides whether to commit or delete): `TrailerRecorder` (Camera.main → RT → AsyncGPUReadback → ffmpeg pipe, Time.captureFramerate 60; AudioRenderer returns 0 samples in this editor, so sound events are logged to `<clip>.sounds.tsv` from GameEvents.SoundRequested and re-mixed in the edit), `TrailerPuppet` (IPlayerIntentSource swapped in via inactive-parent instantiate: bot, auto perfect catch, scripted throws), `TrailerStage`, `TrailerDirector`/`TrailerShots` (run from Play Mode: `TrailerDirector.Run("open,throw,...")`).
- Gotchas: never edit _Trailer scripts during Play Mode (recompile kills the take); Time.unscaledTime follows real time, so shot clocks count frames and slow-mo sets Time.timeScale with GameFeel disabled; enemies must spawn via PoolService (OnSpawned); final arena needs `RunState.BeginAt(i-1)+AdvanceArena` before loading, else GameSession restarts the run at arena 0; Error Pause is auto-unpaused by the director; Random.InitState per shot for repeatable takes.
- Blender (Bouncer.blend opened with `-b`, never saved): kid intros on the Splash set (`kids_shot.py`), end card (`endcard.py`). Background Blender competes for the GPU with Unity — pause it with kill -STOP/-CONT.
- Edit: `edit.py` (beat grid of SFX_Music4: drop at 5.995 s, 106.67 BPM), `chalk.py` (Neucha/Caveat chalk titles), `splash.py`. Copies of all scripts + the result go to `~/Desktop/projects/ItsOurField_Trailer/`.

- v2 (same day, user asked for CC0 music from the internet): «Space Cadet Training Montage» by Zane Little Music, CC0, opengameart.org/node/138918 (FreePD.com closed in 2025; OpenGameArt shows the license per track). edit.py `--music=spacecadet`: 130 BPM, plan drop phrase → last half-phrase → ending, final hit cut to the logo, 35.8 s. Also downloaded but unused: Raspberry Jam (NJS funk), synth_3, Happy Vibes — all CC0.
- The user asked to REMOVE the game's mom-call SFX (MomCall) in the finale — both versions are without it.

**Why:** user approved this after a Q&A; avoids re-asking and documents the pipeline for re-cuts.
**How to apply:** for new cuts reuse the tooling; check takes with contact sheets before final. See [[marketing-screenshots]], [[user-does-playtests]], [[music-setup]], [[unity-mcp-quirks]].
