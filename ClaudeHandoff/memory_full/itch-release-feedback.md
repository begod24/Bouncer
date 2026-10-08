---
name: itch-release-feedback
description: "It's our FIELD v1.0.0 is on itch.io (2026-09-26); player feedback, my balance diagnosis with numbers, and the 35-question list answered 2026-09-27 (see feedback-update-decisions)"
metadata:
  node_type: memory
  type: project
  originSessionId: 230fd0eb-5681-487b-84d9-8812682886f8
  modified: 2026-09-27T09:32:21.416Z
---

The game is public on itch.io: https://begod24.itch.io/its-our-field (v1.0.0, Windows + macOS, free, uploaded 2026-09-26). Read comments with the built-in browser (`get_page_text`).

**Feedback as of 2026-09-27** (players S4nch and sat0urn, plus the user's own list):
- Bugs: a ball gets stuck in props and doesn't come back even with «На резинке». Likely causes (from code, not confirmed in play): a popped «свечка» ball has no time limit, so it never turns Loose; auto-pickup and elastic ignore it, and only a catch (RMB) up close grabs it. Elastic fires only once per throw. Also, one enemy ended up outside the map.
- QoL: pick up lying balls at arm's length with RMB (auto-pickup reaches only 1.1 m; RMB = Catch takes only flying or popped balls). The Site is too dark: the player's light is a 5.5 m / 2.2-intensity point light, and «Фонарик» only multiplies it by 1.6 and adds 2 m. User's idea: a temporary aimed flashlight that burns shadows. The HUD hearts, balls and cooldowns get lost. Roly-poly heads look like balls. Card texts are unclear. Players asked for Kazakh: Neucha lacks Ә Ғ Қ Ң Ө Ұ Ү Һ, Caveat has them all. Also leaderboards/challenges and co-op/PvP.
- Balance: builds get too strong (user: after arena 2 you can buy everything). S4nch says «Дети» are too tanky and annoying; no enemy has that name, probably пупсы (unconfirmed).
- The user also asked to: limit cards (Cuphead-like, or «one golden combo»), rethink Подкат/Кувырок/Йо-йо, make perfect-catch timing stop trivialising bosses and ball-throwers, add new enemies and arenas.

**Diagnosis numbers** (upper bound; a strong build kills everything that spawns):
- Coins: Yard ≈350, Rink ≈480, Site ≈360. The average showcase gum costs ≈31, so ~11 buys after the Yard and ~13 after the Rink.
- Free cards: 3 portfolios per arena (6 on the Site) plus the boss card, so ~30 cards by the Site.
- Enemies need 1–5 hits and don't scale by arena. Tennis+Split+Hail+Hooligan gives up to 13 hits × 2 dmg per throw (twins inherit +damage and homing), and Yo-yo sends every ball back through walls.
- Perfect catch gives +1 heart and a ball. «Цепкие руки» III also widens the perfect window (0.1 → 0.2 s). «Кувырок» catches any ball without timing.
- «Скакалка» III cuts the dash cooldown to 0.36 s (cycle 0.52 s, i-frames 0.22 s ≈ 40% uptime); «Подкат» then hits everything within 1.1 m.

On 2026-09-27 the user asked for a numbered list of questions covering every point before any decision. I sent 35 questions (blocks: cards/kiosk, enemies, catch/bosses, Подкат/Кувырок/Йо-йо, QoL/bugs/HUD/Kazakh, arenas, future, order) with ★ recommendations. The user answered the same day; the decisions live in [[feedback-update-decisions]].

**Why:** the next sessions will implement from these answers, and the numbers show why the balance broke.
**How to apply:** check the answers here before touching balance or feedback fixes. See [[propose-before-changing-design]], [[solo-run-decisions]], [[phases-v-g-decisions]], [[game-design-decisions]].
