---
name: game-design-decisions
description: "User's answers from the 2026-09-24 design Q&A: run structure, meta, cards without XP/levels, co-op revive and loot, PvP modes/rules, hub, business model; where they override the concept doc; open questions"
metadata:
  node_type: memory
  type: project
  originSessionId: 929c9165-0ead-4cb0-be5e-c963f7b54336
  modified: 2026-09-24T13:48:57.706Z
---

On 2026-09-24 I ran a 6-round design Q&A. The same day I rewrote the concept doc (`Game Projects/Vyshibaly_Concept.docx`, outside the repo) and the dev plan (`~/.claude/plans/users-bekbolataldiyarov-desktop-project-jaunty-puppy.md`) to match. If the doc and this file ever disagree, trust the newer one and ask.

**General**
- Roadmap change (2026-09-24, after branding + catch rework): the user postponed PvP and co-op; first a fully playable solo game. I recommended starting with the run loop on the existing Yard (coins → «Союзпечать» kiosk, start card, elites → портфель, boss card, rarity, HP carried between rounds), then arenas/enemies and the remaining cards per milestone, then meta/save. The user started with the assets for it (same day).
- Asset answers (2026-09-24): every elite enemy gets its own look (gold матрёшка in a кокошник, pupsik in a bonnet with a pacifier, officer soldier with a sabre, etc.); coins come in 3 denominations in Kazakh money, not kopecks/rubles (user's wish): 1 тиын (brass, regular enemies), 5 тиын (brass, elites), 1 тенге (silver, boss); the coin says «ТЕНГЕ» as the user wrote it (real coins say «ТЕҢГЕ»); stage 1 also gets gum packs of 3 rarities, healing items sold at the kiosk (lemonade, sandwich) and extra yard toys beyond the GDD (юла, лошадка-качалка). Made in advance: hockey box kit, dusk enemies (манекен, чучело, тень), bosses (Физрук-манекен, «Тот, кто в сумерках»), construction site kit.
- Run = one «прогулка»: 3 arenas in a row (Двор → Коробка → Стройка → финал «Мама зовёт домой»), ~20–25 min, cards kept for the whole run, morning → night.
- Difficulty: danger levels, the next one unlocks after a win (like Brotato's Danger).
- Night tone: light creepiness without jump scares (12+).
- Фантики (meta currency, kept after death) buy only new content: characters, cards added to the pool, starting balls, cosmetics. No permanent stat upgrades.
- Characters: on 2026-09-25 the user decided they differ ONLY by look (same stats and collider; uniqueness comes from cards) — see [[kids-concept-sheet]]. Earlier (24.09) it was «stats + starting ball»; my proposal of unique abilities was declined.
- Hub: a yard hub (kiosk with cosmetics, character board, bench to pick a mode) that doubles as the co-op/PvP lobby. Plain menus until Early Access.
- Business: paid game on Steam (~$5–10), no microtransactions or cosmetic DLC, free demo.

**Cards and progression: the same in solo and co-op (user: «везде как в коопе»)**
- No XP and no level-ups anywhere, so the current XP → level-up → UpgradeScreen flow (PlayerProgression / ProgressionDefinition) has to be reworked.
- Enemies drop монетки (in-run currency) instead of XP. Spend them between arenas at the ларёк «Союзпечать» on gum with wrapper cards (like Brotato's shop).
- Cards also drop as loot: a портфель from elite enemies or a find on the arena goes to whoever picks it up (choose 1 of 3); after a boss everyone picks their own.
- At run start everyone picks 1 of 3 random common cards.
- Combo cards (two specific cards → a special one, e.g. Бумеранг + На резинке → Йо-йо): yes.
- Variety: random yard events (rain, power cut, дворник со шлангом, бабушки с тапками) + elite enemies. Route-choice map, run conditions and reroll/banish were not picked.

**Co-op (online first, local couch co-op later)**
- Balls are shared: anyone picks up any ball, and a ball takes the type and perks of whoever throws it.
- A downed player lies on the ground and an ally revives them by holding a button. If nobody does within ~20 s, they are out until the next arena (they come back with 1 heart and keep their cards). The run ends when everyone is down. The concept's «аут» is dropped for co-op.
- A pass gives nothing by itself. Only co-op-only cards boost passes (the concept's pass combo multiplier is dropped).
- More players → tougher enemies (more HP/damage), not more enemies.
- Finding players: room by code now, Steam invites (stage 4), public matchmaking. No bots.

**PvP**
- ~~Build PvP «Классика» first~~ — overridden 2026-10-06: co-op first, then PvP (see [[coop-pvp-decisions]]).
- No cards in PvP (pure skill; the concept's ROUNDS-style cards are dropped). All characters have equal stats in PvP.
- Modes: Классика 1×1, 2×2, 3×3, 4×4; Каждый за себя; Круг (дворовые вышибалы).
- 1×1 has its own rule: a hit scores a point for the thrower, and the thrower throws again (no outs). A catch scores nothing: the ball just stays with the catcher, who can throw straight back. 2×2 and up use the classic rule: a hit sends you out behind the opponents; catching an enemy ball knocks the thrower out and brings your teammate back.
- Chaos: the user picked everything (my reading: lobby toggles, with pure PvP as all toggles off). Физрук's whistle with random rules, toys (pupsiks) on the field, arena obstacles (swings, sandbox, puddles), themed arenas such as lava («пол — это лава»), rain, blizzard/snow.

**Catching (user asked for timing/skill instead of spamming; implemented 2026-09-24, not playtested yet)**
All four of my proposals were accepted. The numbers live in `PlayerStats_Default`:
- Perfect catch: the ball is caught within `perfectCatchWindow` (0.1 s) of the press, inside a `catchWindow` of 0.25 s. The ring is gold during the perfect part; a perfect catch plays the CatchCandle cue and a gold flash.
- Only a perfect catch of an enemy ball heals (`catchHeal` 1, `earlyCatchHeal` 0). Any catch still gives the ball.
- Balls are caught only from the front (`catchHalfAngle` 90°, drawn as a front arc); a candle falling from above is always catchable.
- Spam penalty: the miss cooldown is 0.45 s, plus 0.3 s for each miss in a row, capped after 3. The streak resets on any catch or after 1 s without pressing once catch is ready.
- A strong ball (HitFlags.Charged: a charged throw or a ball sent back by the swings) needs a perfect catch; otherwise it is knocked out of the hands (bounces forward onto the floor, no damage, `Fumbled` event).

**My defaults (not asked; the user may object)**
- Players can join only at run start.
- A solo card choice pauses the game; in co-op it is a mini-menu without a pause.
- «Замри!» in co-op freezes the enemies for everyone instead of changing global time scale.
- Hit-stop is a local visual only.
- Esc online opens the menu without pausing.

**Open questions**
- 1×1 match length: how many points or how much time?
- Are danger levels per character?

**Solo run details (second Q&A, same day):** kiosk, coins, elites, enemy behaviour, bosses per arena, phases — see [[solo-run-decisions]].

**Why:** the user asked me to remember these answers so future design builds on them.
**How to apply:** check here (and in the concept doc) before designing stage 3–5 features. See [[stage2-status-decisions]], [[kids-concept-sheet]].
