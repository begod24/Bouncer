---
name: marketing-screenshots
description: "How itch/devlog screenshots are rendered from the Unity editor without Play Mode (preview scene + arena lighting + posed kid), and where the 1.1 set lives"
metadata:
  node_type: memory
  type: reference
  originSessionId: 855fc8ee-b653-4b00-8b3f-62601ff6f02c
  modified: 2026-09-28T08:50:35.282Z
---

Devlog screenshots for 1.1 (2026-09-28) are in `~/Desktop/projects/ItsOurField_Screenshots/devlog_1.1/` (8 PNGs, 1920×1080: Bazaar fight, Transformer robot + car, Kindergarten fight, Hare, a lineup of the 8 new enemies in the Yard at day, and overviews of both arenas). The 1.0 itch-page shots are in `~/Desktop/projects/ItsOurField_itch/preview/`. No HUD in the new set.

Recipe (Unity_RunCommand, no repo files):
- `EditorSceneManager.OpenPreviewScene(scenePath)` + `Unsupported.SetOverrideLightingSettings(scene)`. Then `TimeOfDayController.Configure(arena.timeOfDay, 0)`, set `Progress` and call `Apply()`. Volumes and fog apply. In `finally`: `RestoreOverrideLightingSettings`, `ClosePreviewScene`, and `Apply()` on the open scene's controller, because closing resets the palette globals. `FindObjectsByType` doesn't see preview-scene objects: walk `scene.GetRootGameObjects()`.
- Game camera: FOV 40, pitch 55°, offset (0, 16.38, −11.47) from the player +1 m. ×0.65–0.7 of that distance reads better. Render at 3840×2160 (1 sample, sRGB ARGB32), then Lanczos down to 1080p in PIL.
- Enemies: `PrefabUtility.InstantiatePrefab(prefab, scene)`. They have no Animators: pose them by rotating the serialized `legL/legR/armL/armR` transforms. Transformer: `rig.SetMode(car)`, `AimCannon`, then `SendMessage("LateUpdate")`. Dendy laser, CryWave ring and GroundMarker line are set by hand on their LineRenderers. For particles (SlowCloud, StuffingBurst, ShadowSmoke) call `ps.Simulate(t, true, true)`.
- Kid: hide Player/Kid, HandBall, CatchRing, AimLine and DashTrail. Instantiate `Art/Models/Kids/Kid_*.fbx` under an offset parent and sample an Anim_Kids clip (Throw 0.1, Charge_Full, Dash). Then BakeMesh it (see [[unity-mcp-quirks]]).
- Dark arenas (KG, Site): enable Player/Lantern. Add the Q-flashlight as a spot light: intensity 90, range 11, angle 52/31, 1.2 m up, pointed at the subject. Without it, KG shots are unreadable. The Bazaar at evening has half the arena in a panel-block shadow, so compose on the west (lit) side, progress 0–0.1.
