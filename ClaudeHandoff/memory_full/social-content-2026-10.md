---
name: social-content-2026-10
description: "Instagram/TikTok content batch 1 (2026-10-07): «all lies on the yard asphalt» look (chalk, gum-wrapper cards, notebook, film photos), RU+EN, where the photos/reels/tools live and how to rebuild"
metadata:
  node_type: memory
  type: project
  originSessionId: 439cdfd0-59f0-4093-bb08-e34a3e4655a6
  modified: 2026-10-07T21:40:12.438Z
---

On 2026-10-07 the user asked for Instagram/TikTok reels, photos and renders to start marketing, plus a list of what to tease and of possible Blender renders. Mid-task asks: renders «стилем самой игры» with an eye-catching unique presentation; an English format too; «сохрани картинки, потом блендер»; an EN caption for the first Instagram post (key art) — given in chat.

Built (all outside the repo, in `~/Desktop/projects/ItsOurField_Social/`):
- Look: everything lies on the yard asphalt — chalk titles (Neucha/Caveat) + the HUD's yellow wavy underline; kids as gum-wrapper cards (вкладыши, pinked paper, halftone, №1–4); bosses as squared-notebook dossier pages with a taped photo, blue ballpoint + red pen «Как победить», cover = green Soviet notebook «для боссов двора»; arenas as film prints with the orange 7-segment date '97 6 1 … '97 8 31.
- `photos/ru|en` (20 each, 4:5 + key art story 9:16), `reels/` R1 vertical trailer, R2 «Кто ты из двора?», R3 bosses, R4 POV 21:00 — each `_ru/_en` and `_sfx_only` (no music, for trending sounds). Music = Space Cadet (CC0) only; the other CC0 tracks from the trailer session have no recorded license, so not used.
- Tools: `tools/blender/social.py` (Blender -b, never saves: kids_card, kids_anim 9:16, key_still, key_anim, boss posters with rim/fill), `tools/social_art.py` (asphalt, chalk, wrappers, notebook, film print, date stamp), `tools/photos.py`, `tools/reels.py` (imports the trailer's `edit.py` for SFX/music; gameplay in a 5:6 chalk-framed window, titles in the top band because TikTok UI covers the bottom). Sources in `src/renders`, `src/frames`. `PLAN.md` = what's done, what to tease, 16 Blender render ideas, captions/hashtags.
- The dark rectangles on the splash-set asphalt are the yard's asphalt patches, not render bugs.

**Why:** next marketing batches should reuse this look and tooling instead of inventing a new style.
**How to apply:** new posts → add to photos.py/reels.py; Blender jobs → social.py; open questions (audience/language, CTA itch vs Steam, RU name «Вышибалы», face/voice devlog, Halloween Babai series, Play Mode for native 9:16 footage) were asked in chat — check for answers. See [[trailer-2026-10]], [[marketing-screenshots]], [[art-pipeline-blender]].
