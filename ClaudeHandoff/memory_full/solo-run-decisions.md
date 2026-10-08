---
name: solo-run-decisions
description: "2026-09-24 second design Q&A for the full solo run: kiosk showcase on every arena, coins, healing, elites, portfolio/boss cards, toy and dusk enemy behaviour, bosses per arena, Site darkness, work order"
metadata:
  node_type: memory
  type: project
  originSessionId: 7b1860f5-4d59-4799-bdab-2b83f9b69635
  modified: 2026-09-24T15:51:03.632Z
---

On 2026-09-24 (after the stage-1 assets) the user answered 5 rounds of questions for the full solo run. My phases: A — run loop without XP on the Yard; Б — yard toys + 7 elites; В — hockey box (evening) + mannequin + Fizruk; Г — construction site (dusk) + scarecrow + shadow + smoke VFX; Д — final «Мама зовёт домой» + «Тот, кто в сумерках». The user said: do A and Б together, first playtest after both, then one phase at a time; start right away; update the GDD and the dev plan now.

**Kiosk «Союзпечать»**
- Showcase like Brotato: 4 gums on the counter, each card visible, price by rarity (~15 / 35 / 70). Buy what you can afford, the rest carries over.
- Extras (all picked): paid reroll that gets pricier each time; lock a gum so it waits in the next kiosk; card «Копилка» reworked for coins (+1 coin per 10 in the pocket at the kiosk, like Brotato interest).
- The kiosk stands at the edge of every arena (Yard, Rink, Site), closed during the fight. Arena cleared → remaining coins fly to the player, the window lights up; walk up + press a button → showcase; then walk to a chalk arrow («В коробку →») to go to the next arena.
- Healing only at the kiosk: lemonade +1 heart (cheap), sandwich = full heal (pricier). Hearts carry over between arenas as they are. The «Бутерброд» card stays as the deck filler.

**Coins:** drop on the ground and fly to the player inside the pickup radius («Длинные руки» increases it); when the arena is cleared the rest fly in. My defaults: 1 тиын = 1, 5 тиын = 5, 1 тенге = 20; HUD shows the number next to ₸.

**Cards:** rarity common / rare / gold. My split: common — Новые кеды, Цепкие руки, Длинные руки, Ещё мяч, Попрыгунчик, Жвачка, Теннисные мячи, Волейбольный мяч; rare — Бумеранг, На резинке, Подкат, Хулиганство, Набивной мяч, Сдутый мяч; gold — Замри!, Раздвоение, Горячая картошка. Portfolio = 1 of 3 of any rarity (common more often); boss = 1 of 3 rare or gold; one found portfolio per arena, glinting near an edge. The remaining cards (Стеночка, Рогатка, Глаз-алмаз, Домино, combo Йо-йо, Копилка, and the block card «Портфель» renamed, e.g. «Крышка от кастрюли») go into phase A too.

**Elites:** 1–2 per arena on a schedule (set wave seconds, marker + sound). ×3 HP, bigger, each with its own ability: gold матрёшка opens into 2 regular ones; pupsik in a bonnet leads the swarm and cries to call more; officer commands volleys and throws a strong ball; sailor bear spits stuck balls back at the player (catchable); rooster runs zigzag with a bigger blast; sputnik top splits into 2 mini tops; конь-огонь leaves a fire trail on its charge. Each drops a portfolio + 5 тиын.

**Toys:** bear — slow tank (~6 hits), paw swipe up close; the first ball sticks in its belly (damage counts, the player has one ball less); a hit with another ball knocks the stuck one onto the floor; a dead bear drops it too. Chick — winds up (key spins, sparks, ~0.8 s), runs straight to where the player stood, explodes on a wall / the player / after 3 s; the blast hits everyone, enemies too; shot by a ball, it explodes on the spot. Top — billiard: rolls toward the player in an arc, knocks on contact; a hit launches it along the ball's direction, it bounces off walls and knocks enemies on its way; 2 hits. Rocking horse — rocks harder and harder, then charges in a straight line; after the charge it rocks in place (window for throws); a hit during the wind-up breaks the charge; ~3 hits.

**Arenas and bosses:** Yard → Большая неваляшка; Rink → Физрук-манекен; Site → no boss, an elite pack at the end; Final = our Yard at night → «Тот, кто в сумерках», survive until mom calls or knock the boss out earlier. Arenas ~5 min, final ~3 (my default).

**Dusk enemies:** mannequin freezes while inside any player's view cone (~70°, facing/aim), sneaks fast otherwise, new pose at each stop. Scarecrow catches balls flying close in front of it and throws them back after ~1 s (catchable, a perfect catch heals); charged balls break through, back/side hits work normally. Shadow (my default): balls pass through it in darkness, it is solid in light, and it has to step into the light to attack a player standing under a lamp. Site darkness: the user picked «видно только в свете» over my «полутьма» — almost nothing visible outside the lamps, a small light circle around the player.

**Implemented on 2026-09-24 (phases A+Б, not playtested yet):**
- Code: `Core/RunState` (arena index, coins, hearts, totals across scenes), `GameSession` states Title/Playing/Upgrade/Cleared/Shop/GameOver/Victory, `Core/ScreenFade` (IMGUI fade between arenas). XP is gone: `PlayerProgression`→`Upgrades/PlayerCards` (queue of «1 из 3»: Start/Portfolio/Boss; re-applies `RunCards.Taken` on a new arena with `UpgradeCard.Replaying`), `ProgressionDefinition`→`CardDeck` (rarity weights per source, shop prices). New assembly `Scripts/Run`: `RunDefinition`/`ArenaDefinition` (goal DefeatBoss/SurviveAndClear/SurviveUntilCall), `ArenaDirector` (applies the arena to the scene, clears it, boss card, opens kiosk and exit, places the found portfolio), `LootDropper`, `CoinPickup`, `PortfolioPickup`, `Kiosk`, `ShopStock`, `ArenaExit`. UI: `ShopScreen`, `ArenaBanner`, `KioskPrompt`, coins in `GameHud`, rarity frame in `UpgradeCardView`. Interact = F / gamepad X.
- Run_Default = 2 arenas for now, both in the Yard scene: Arena_YardMorning (Morning→Day) and Arena_YardEvening (Day→Evening, stand-in for the hockey box); winning arena 2 ends the run. Arenas 300 s, boss at 270 s.
- Numbers: coins 1/5/20; RolyPoly 1, Pupsik 1 @35%, TinSoldier 2, bear 3, chick 1, top 2, horse 2, elite 15 + portfolio, boss parts 20/10/5; prices 20/40/75 (+15% per arena), reroll 5 (+3), lemonade 10, sandwich 25.
- Enemies: `BearEnemy`, `ChickEnemy`, `TopEnemy` (kinematic, SphereCast moves), `RockingHorseEnemy` + definitions; elites are prefab variants with their own definitions (×3 HP) plus `SpawnOnDeath` (matryoshka, sputnik top → `Top_Mini`), `SwarmLeader` (pupsik), `OfficerEscort` + `SoldierSquad.Leader` + `strongThrow` (officer), bear `spitDelay`/`maxStuckBalls`, horse `FireSpot` trail. Ball got `BallState.Stuck`. Wave elite slots use `SpawnBurst.variants` (random elite per slot, 2 per arena).
- 7 cards added (Стеночка, Рогатка, Глаз-алмаз, Домино, Копилка, Крышка от кастрюли, Йо-йо combo) — deck 24.
- VFX: the first particle effects in the project — `Core/ParticleBurst` (pooled burst), `Prefabs/VFX/ChickBlast` (flash, fire, smoke, sparks; scaled by blast radius for the rooster), key sparks on the chicks, material `M_Particle` (Sprites/Default + Default-Particle). Added after the user asked «а взрыв курицы vfx делал?».

**Why:** the user asked to settle these so the stage A/Б code is right the first time.
**How to apply:** build the run loop and enemies to this spec; ask again before phases В–Д about boss details (Fizruk's whistle rules etc.). See [[game-design-decisions]], [[assets-status]], [[propose-before-changing-design]].
