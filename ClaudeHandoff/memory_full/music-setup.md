---
name: music-setup
description: "In-game music (2026-09-28): random track per arena from SFX_Music2/3/4, final with Babai keeps SFX_InGame; where it is configured, loop lengths and how they were measured"
metadata:
  node_type: memory
  type: project
  originSessionId: c222a523-c78e-44d8-ab98-7c07f3c664f3
  modified: 2026-09-28T12:57:24.871Z
---

On 2026-09-28 the user added SFX_Music2/3/4 (Audio/Music) and asked: random music on every level except the final vs Babai, which keeps SFX_InGame.

Implemented the same day (not playtested):
- `Audio/MusicPlayer`: `gameThemes[]` (pool) + `finalTheme` (renamed from `gameTheme` via FormerlySerializedAs = SFX_InGame). Each arena (= new GameSession/scene) picks once: final theme if `Core/ArenaMusic.FinalTheme`, else a random pool track that is not the previous arena's. Tutorial counts as a normal level (random).
- `ArenaDefinition.finalMusic` (true only on Arena_Final) → `ArenaDirector.Apply` sets `ArenaMusic.FinalTheme`.
- MusicPlayer is a plain object in all 5 scenes (Yard, Rink, Bazaar, Site, Kindergarten), not a prefab — change all 5. I patched the scene YAML as text, because re-saving scenes through Unity re-serializes unrelated UI overrides (Rink did).
- Loop lengths (beat grid measured with numpy/scipy onset autocorrelation; the method reproduced SFX_InGame's 76.19 = 40 bars @126): Music2 62.4 (150 BPM, 39 bars; the 0.8 s noise riser at the start overlaps the previous loop's end), Music3 59.625 (106.67 BPM, 106 beats; file has 0.4 s silence + a 2-beat tonal pickup, so the next loop starts exactly when the last hit rings), Music4 72.0 (32 bars @106.67; ~7 s silence after the end). Volumes matched to InGame's loudness at 0.4: 0.26 / 0.25 / 0.2. Import settings copied from SFX_InGame (CompressedInMemory, q 0.7, preload).

**Why:** the user wants variety across arenas but the final keeps its own track.
**How to apply:** if a loop sounds off, adjust `loopLength` in all 5 scenes' MusicPlayer. See [[user-does-playtests]], [[unity-mcp-quirks]].
