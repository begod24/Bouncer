---
name: inscriptions-as-textures
description: "Text, signs, numerals and graffiti on 3D models must be 2D textures (decal atlas), never font geometry"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 8fc451fd-58fa-4436-a80f-0a593fd56e9f
  modified: 2026-09-24T15:14:39.021Z
---

Never turn text into 3D geometry (font → mesh) for Bouncer models: signs, numerals on coins, graffiti, stars/doodles on wrappers are 2D. Use the decal atlas: `Tools/decal_art.py` draws masks into `Assets/_Project/Art/Decals/T_Decals.png` (+ `Tools/decal_atlas.json`); in Blender `Builder.decal(name, frame, colour, height|width)` makes a 2-triangle quad with material M_Decals (UV0 = palette cell, UV1 "UVDecal" = atlas rect); Unity shader `Bouncer/PaletteDecal` clips by the mask and takes the colour from the palette, so the time of day works.

**Why:** on 2026-09-24 I built signs and graffiti from font geometry (a coin was ~1k triangles, the rink boards 30k); the user objected: «графити и в целом все надписи можно же на 2д делать… через sprite или материалы и текстуры… это очень неэкономно». After switching, a coin went to ~200 triangles, the boards to 8k.

**How to apply:** new inscription → add an entry at the END of `ENTRIES` in `Tools/decal_art.py` (placement is in list order, so old UVs stay put), run it, then `ba_lib.reload_decals()` in Blender. Applies to any future art (UI text in 3D, posters, labels). See [[art-pipeline-blender]].
