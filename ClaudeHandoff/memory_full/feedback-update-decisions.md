---
name: feedback-update-decisions
description: "User's 2026-09-27 answers to the 35-question list (patch 1.0.1 → balance → content): pockets, 1 gold, combo cap, economy, nerfs, only player balls stay, hedgehog/wet balls, catch heal cooldown, bosses, enemies, arenas, Kazakh"
metadata:
  node_type: memory
  type: project
  originSessionId: 230fd0eb-5681-487b-84d9-8812682886f8
  modified: 2026-09-27T15:24:40.233Z
---

Answers from the clickable form (2026-09-27). Work order: patch 1.0.1 → balance → content.

**Cards**
- 6 pockets. Stacks of one card take 1 pocket. The ball type and the money cards (Копилка, Шпаргалка, Счастливый фантик) take none.
- A combo merges its two cards into one. When pockets are full: discard a card (it goes back to the deck) or sell it at the kiosk for half price.
- Max 1 gold card per run (the user's notes override the form's «1–2»). Combos: the form said one, the notes said «1–2, not more». My proposal: combos become their own rarity with a cap of 2, so the gold cap doesn't block them (two combos have a gold ingredient); asked in the follow-up form. Счастливый фантик moves from gold to rare.
- Target: 10–12 cards by the final.

**Economy**
- Regular enemies drop coins with ~35% chance.
- Prices 25/50/90, +20% per arena, +10% per purchase in the same kiosk, reroll 5 (+5). Lemonade 15, sandwich 40 (+20% per arena).
- Only one of the two elites drops a portfolio; the found portfolio becomes coins or lemonade.
- My estimate after this is still ~14 cards, so I plan to cut elite coins 15 → 10 and the Yard boss 60 → 30, then check in playtest.

**Nerfs and enemy growth**
- Twins get no Хулиганство bonus and no homing; Глаз-алмаз 240 → 120°/s. Ещё мяч max 2. Цепкие руки don't widen the perfect window. Скакалка max 2, dash cooldown ≥ 0.6 s.
- Enemies need ×1.5 hits on the Rink and ×2 on the Site (only those with 2+ hits).
- Danger levels go in THIS update (my 1–5 table was sent for approval).
- Elite affixes: Шустрый, Командир, Ловкач (no helmet/armor).

**Pupsiks («Дети»)**
They already die in 1 hit. The real bug: the ball flies at 1.1–1.34 m, but a pupsik's capsule top is at 0.84 m (ball radius 0.22). A tap throw passes over a standing pupsik within ~16 m (a charged one within ~26 m); only hopping pupsiks get hit. Chicks (0.9 m) and mini tops (0.64 m) have the same problem. Fix: column hitboxes up to ball height.

**Balls (user notes)**
- Only the player's balls stay on the arena; enemy balls vanish ~1 s after landing. (Changed 2026-09-28: up to 7 enemy balls now stay — see [[arena-balls-pockets-ui]].)
- The player should start with all own balls (now 2 of max 3; the 3rd came from enemies). A caught enemy ball is a one-time throw (asked).
- Player balls taken by enemies (scarecrow, bear, Babai's sack, crows) stay the player's.
- Hedgehog ball: red and glowing; catching it hurts. Regular soldiers throw one with 20% chance; the officer and the scarecrow always do.
- Wet ball: slips out of the hands and doesn't heal (its source was asked).
- Catch heal: +1 heart, no more than once per 10 s. A caught strong ball pushes the player back 1.5 m.

**Bosses**
- Vulnerability windows: ×0.5 damage outside, ×1.5 inside.
- Big roly-poly throws small roly-polys.
- Fizruk: part of «Штрафной» are hedgehogs, plus a medicine ball that knocks the player down when caught.
- Babai: «Карусель» stays but also throws hedgehogs; dark balls from the sack slow the player for 2 s on hit.
- All bosses: feint and curve ball.

**Tackle, roll, yo-yo**
- Подкат: numbers only (stuns without damage, no more than once per 0.8 s).
- Кувырок: last-moment dodge (slow-mo, then a candle-strength throw, no catching).
- Йо-йо: only one ball on the string.

**Patch**
- Pickup: auto 1.5 m plus an RMB grab up to 2.5 m. Bug fixes (stuck ball, enemy off the map) the user wants to discuss first (options asked).
- Site: semi-dark only (player light unchanged). The aimed flashlight goes on Q; its form (card or base ability) was asked.
- HUD: bigger icons on a backing, cooldown rings near the player, blinking last heart (no ball dots).
- Change the roly-poly look, not the ball. Card texts with numbers and «1/3».
- Kazakh: Caveat font. The user translates himself; I export a RU/EN/KK table. The logo gets translated.
- Timer and local records. Multiplayer stays after solo.

**Content**
- New enemies: shield soldier, music-box ballerina, pioneer drummer, lunokhod, wind-up frog, placed on the Rink and Site. The user asked for more ideas.
- Arenas: Барахолка, Детсад с каруселью, Школьный спортзал, plus more ideas wanted. Fork with two chalk arrows; two new arenas in the next update.

**Follow-up answers (same day):**
- Stuck ball: only «roll it out to the nearest walkable spot» (no candle timeout, no auto-return to hands, no elastic re-trigger).
- Enemy off the map: put it back inside.
- Flashlight on Q: a base ability on dark arenas; the «Фонарик» card upgrades it.
- Combo cap 2. Gold cap is hard: after the first gold, no more golds are offered.
- A caught enemy ball is one-time: it disappears after the throw.
- Wet balls: some enemy balls in the rain. Babai's dark balls only slow the player (catchable as usual).
- New enemies split: Rink gets the shield soldier, pioneer drummer and wind-up frog; Site gets the ballerina and lunokhod.
- First two new arenas: Барахолка (fork: Коробка or Барахолка) and Детсад (fork: Стройка or Детсад at dusk). Bosses: Барахолка — «Трансформер из ларька» (rams as a car, shoots as a robot); Детсад — «Большой плюшевый заяц» (jump slams, stuffing clouds slow); Спортзал (later) — «Скелет из кабинета биологии» (falls apart into bones and reassembles).
- Extra enemies liked (later): Кукла-плакса, Паровозик на рельсах, Машинка на пульте, Пистолет от «Денди» (laser line, then shot), Кукла-санитарка. Extra arenas liked (later): Пионерлагерь, Луна-парк (shooting gallery for coins, bumper cars), Бахча.
- Danger table approved: 1 = the game after the balance pass; 2 = +1 elite per arena and elites always get an affix; 3 = enemies +10% speed and soldier hedgehog chance 20→35%; 4 = −25% coins, kiosk healing +50%; 5 = bosses +30% hits and more feints, mom calls 30 s later. Levels are shared by all kids (not per kid); the next one opens after a win.


**Phases 1+2 are DONE (2026-09-27, uncommitted, not playtested yet):** all code, data, prefab wiring (GameUI HUD/pockets/rings/affix labels/danger stepper/records screen, Player flashlight, ball spikes/glow, Lemonade, BossLobber, Fizruk medicine ball), localization (RU/EN strings, card texts with numbers, kk locale + `Tools/Localization/kk_translation.csv`), Site semi-dark, and the roly-poly kerchief (see [[art-pipeline-blender]]). Concept doc and dev plan (stage 2.7) updated the same day. My defaults the user didn't pick: throw order yo-yo string → borrowed → own; Счастливый фантик moved to rare; elite coins 10; boss parts 12/5/2; arena finds 12 coins or a lemonade; Site portfolios 2.
- Phase 3 (content) is done by a PARALLEL session «Контент этап 3» in the same repo and Unity instance (its decisions: [[stage3-content-decisions]]). Don't open or save scenes from other work, don't save Bouncer.blend concurrently, don't touch its files. The dirty TMP font assets come from AssetDatabase.SaveAssets.

**Why:** these settle the 1.0.1/balance/content work so the code is right the first time.
**How to apply:** build to this; after each stage update the concept doc and dev plan. See [[itch-release-feedback]], [[propose-before-changing-design]], [[solo-run-decisions]], [[phases-v-g-decisions]], [[phase-d-decisions]].
