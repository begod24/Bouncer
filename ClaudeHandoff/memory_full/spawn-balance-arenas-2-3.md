---
name: spawn-balance-arenas-2-3
description: "2026-09-28: arenas 2–3 spawned far too much after the balance pass + stage 3 (hit budgets vs the Yard, three causes, sim results) and the user's form answers: budget + phases ×1.5/×1.8, elites half multiplier, rhythm; next direction = onboarding + run save"
metadata:
  node_type: memory
  type: project
  originSessionId: 696bbbb4-2cdb-4bb9-8b03-5adc8448661a
  modified: 2026-09-27T22:21:38.390Z
---

On 2026-09-28 the user said the game is playable, but arenas 2 and 3 spawn «ужасно много» since the balance pass. The Yard is fine and stays as the reference.

**Diagnosis (from the wave assets, hits needed to clear everything spawned before the boss):** Yard 295 enemies / 328 hits; Rink 485 / 873 (×2.7); Bazaar 460 / 764 (×2.3); Site 141 / 802 (×2.4); Kindergarten 272 / 606 (×1.8). Before the patch: Rink 561, Site 335. In a rough sim at the player rate that keeps ~3 enemies alive on the Yard, the Rink holds ~24 alive (at the global cap ~40% of the time), Bazaar ~22, KG ~15, Site ~13.

**Causes:** (1) `EnemyScaling.Hits` rounds up, so ×1.5 turns 3 hits into 5 and the bear's 5 into 8; elites scale too (elite bear 23, elite mannequin 18). (2) Stage 3 added enemy tracks on top of the old ones: Rink 8 → 11 tracks (+190 hits), Site +2 (+132); Bazaar (10) and KG (8) were built from the same templates. (3) The global caps were not lowered (Rink/Bazaar 18→40 vs Yard 14→36). The player is also weaker now (6 pockets, 1 gold, 10–12 cards). Site mannequins are half the Site's load (72 × 6 hits).

**Sim:** only lower caps → Rink 24 → 19 alive; softer multipliers ×1.25/×1.5 → Rink −6% only; intervals ×1.9 + caps 14→30 → ~10 alive. So the waves themselves must be cut.

**My ★ proposals in the form:** arena 2 ≈ ×1.5 of the Yard in hits, arena 3 ≈ ×1.8; enemy types in phases (≤5–6 at once), new enemies replace part of the old tracks, lower caps, fewer Site mannequins; round .5 down; elites get half the arena multiplier; a 15 s lull before the boss; while an elite is alive, regular spawns slow ×1.5. Data-only except the last two.

The same form asked about the next steps: Kazakh CSV re-export (235 rows, none translated, 10 stage-3 keys missing), a playtest checklist, an itch changelog, music per arena and boss (only menu + one game track exist), and the next direction (solo retention: onboarding, run save, achievements, album/фантики; content: yard events, Спортзал; network; an early Steam page).

**User's answers (2026-09-28):**
- Spawn: «бюджет + фазы», targets arena 2 ≈ ×1.5 of the Yard, arena 3 ≈ ×1.8; do it now. The Yard stays untouched.
- Toughness: elites get half the arena multiplier. Rounding down was NOT picked: regular enemies keep ×1.5/×2 rounded up.
- Rhythm, all three picked: a 15 s lull before the boss, «элитка — событие» (regular spawns ×1.5 rarer while an elite is alive), «паузы-волны» (10 s without spawns every minute). «Лимит по весу» not picked.
- Before the 1.1 upload: only the RU/EN changelog for itch. Not picked: Kazakh CSV re-export, playtest checklist, music per arena.
- Next big direction: solo retention, namely onboarding (first-run tutorial) and saving the run. Not picked: achievements, album, фантики, daily run.
- Content later: Спортзал + Скелет, Паровозик + Кукла-санитарка, new arenas (Пионерлагерь, Луна-парк, Бахча). Yard events not picked.
- Steam page after the network, as in the plan.

**Why:** the next session implements from the user's answers; the numbers show where the load comes from.
**How to apply:** build the spawn fix to these answers; onboarding and run save need a design Q&A first ([[propose-before-changing-design]]). See [[feedback-update-decisions]], [[stage3-content-decisions]], [[propose-before-changing-design]].
