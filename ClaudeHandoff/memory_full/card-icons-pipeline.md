---
name: card-icons-pipeline
description: How Bouncer card icons are made — chalk doodles from Tools/ui_art.py and 3D ball icons rendered in Unity (recipe that matches the existing ball icons)
metadata:
  node_type: memory
  type: reference
  originSessionId: 9282c0ec-bacc-45fd-adc0-92824848241a
  modified: 2026-09-24T09:07:56.902Z
---

Doodle icons (dark INK on transparent, 256×256) come from `icons()` in `Tools/ui_art.py`, each with its own fixed Canvas seed. Before writing into the repo, run the module with `OUT` pointed at the scratchpad and `cmp` against `Art/UI/Icons` — old PNGs must stay byte-identical; view new ones on a light background (single icons at 128 px too). Redirect OUT by loading it as a module (`importlib.util.spec_from_file_location` → `exec_module` → `mod.OUT = ...`): `runpy.run_path` returns a COPY of the globals, so setting OUT there silently writes into the repo (happened on 2026-09-24; harmless only because the output was byte-identical). Shop item icons (`Icon_Item_*`, `Icon_Portfel`) use the ball recipe below with the camera at yaw 200° (fronts face +Z in Unity), tilt 22° (48° for the flat sandwich); gain 0.8 reproduced the old tennis icon within ~2 levels.

Ball icons (`Icon_Ball_*.png`) are rendered from the FBX in Unity via Unity_RunCommand, no script in the repo: preview scene, mesh + `M_Palette`, global palette left as the Yard edit-time one (Morning), directional light color (1, 0.93, 0.82), intensity 1, Euler(30, 60, 0), orthographic camera size 0.6 at Euler(22, 0, 0), 1024² sRGB RT without MSAA/HDR, transparent clear. Then in Python: luminance gain ≈0.8 (calibrated against the existing tennis icon), crop to alpha bbox, fit into 256² with a 17 px margin (Lanczos). Deflated ball was tilted Euler(-12, 25, 8) to show the dent.

Card previews without Play Mode: instantiate GameUI in a preview scene, hide Hud/Screens, Canvas → Screen Space Camera, set UpgradeScreen group alpha 1, call `UpgradeCardView.Show(card, 0, n, -1f)` after setting `LocalizationSettings.SelectedLocale`, render to PNG. See [[unity-mcp-quirks]], [[stage2-status-decisions]].
