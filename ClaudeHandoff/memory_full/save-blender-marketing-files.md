---
name: save-blender-marketing-files
description: "Always save Blender work for content (renders, posters, animations) into separate, organized .blend files that link assets from Bouncer.blend — never leave it as throwaway CLI runs"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 439cdfd0-59f0-4093-bb08-e34a3e4655a6
  modified: 2026-10-07T21:43:39.027Z
---

From 2026-10-07 the user wants every Blender setup made for content (trailer shots, social renders, posters, intros) SAVED and split so future content is easy to make: «всегда сохраняй блендер и дели так, чтобы в будущем легче было работать для будущих контентов».

**Why:** the trailer and social batch 1 were rendered with `blender -b Bouncer.blend --python … ` and never saved, so nothing could be reopened or reused.
**How to apply:** keep Bouncer.blend itself untouched (it is the asset source + FBX export). Put content setups in `~/Desktop/projects/Bouncer_Marketing/` — one .blend per series (key art, kids, bosses, trailer…), one scene per shot with keyed camera, frame range, resolution and output path; assets come in by library link from `../Bouncer.blend` (local objects on linked mesh data, like ba_branding.place), so asset edits flow in. Keep the build script next to them and update its README. New shots go into the matching series file (or a new one), never only into a script. See [[social-content-2026-10]], [[trailer-2026-10]], [[art-pipeline-blender]].
