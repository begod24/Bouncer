---
name: bestiary-tutorial-decisions
description: "2026-09-28 bestiary + first-run tutorial: user answers (main menu, bosses open after a win, elites as separate pages, training level asked on first «Играть») and what was implemented where"
metadata:
  node_type: memory
  type: project
  originSessionId: 5aac2c5d-4104-4eac-a4c4-91e1218f6e42
  modified: 2026-09-28T08:04:23.265Z
---

On 2026-09-28 the user asked «сделай бестиарий и обучение; в бестиарий все открыто кроме боссов», then «в главное меню можно» (both reachable from the title menu). AskUserQuestion answers (my ★ picks except elites):
- Tutorial: «Тренировка во дворе» — a separate short run of the Yard without waves, step-by-step chalk tasks; Esc = pause (restart/quit). Not picked: contextual hints in the first run, both, static «Как играть» pages.
- First launch: the first «Играть» asks «Пройти обучение?» once; afterwards only the menu button.
- Bosses: closed «???» + silhouette until defeated (Бабай — after winning the final, knocked out or survived). Not «after the first meeting».
- Elites: a separate «Элитки» tab, not a toggle inside the regular enemy's page.

**Implemented the same day (uncommitted, not playtested):**
- Tutorial: `Core/Tutorial` (Request/Begin/Active, PlayerPrefs offered/done, OpenKidsOnTitle), `GameSession.StartTutorial` + Restart keeps the tutorial, `RunState.BeginTutorial` (danger 1, no start card), `ArenaDirector` skips waves/finds/weather, `Health.Floor` (player can't drop below 1), `Danger` hedgehog/affix chance 0 in the tutorial. `Run/TutorialDirector` (TutorialStep enum, 11 steps, spawns through WaveSpawner.QueueGroupAt toward the arena centre; object `TutorialDirector` in Yard.unity). `UI/TutorialPanel` under GameUI/Tutorial (task card at top-left under the HUD board + «ГОТОВ ГУЛЯТЬ!» screen). Strings `tutorial.*` in UI.
- Bestiary: `Run/Bestiary` SO (`Data/Bestiary/Bestiary.asset`, 34 entries, arenas computed from RunDefinition waves, crow alsoIn Arena_Final, hits read from definitions), `Core/BestiaryProgress` (opened by ArenaDirector on BossDefeated and on a won run), `UI/BestiaryScreen` + `BestiaryEntryButton` + `BestiaryStage` (prefab `Prefabs/UI/BestiaryStage`, stage at y −600, copies the prefab's `Visual` child with scripts/physics/lights stripped). Strings `bestiary.*` in UI, entry texts `bestiary.<prefab lowercased>.name/desc/tip` in Content (bosses reuse `boss.*` names). ru/en only.
- `Screens/TutorialAskScreen` is a RunScreens overlay (first «Играть»).
- Old wins didn't backfill boss pages (they open from the next boss win).

**Why:** settles the design; the next session knows where things live.
**How to apply:** report my defaults (step list, counts, texts, layout) as changeable. See [[propose-before-changing-design]], [[stage2-status-decisions]], [[unity-mcp-quirks]].

**Main menu redesign (2026-09-28, second Q&A):** the user said the 7-button menu is overloaded. Picked «Тетрадка» (my ★): centre = big ИГРАТЬ, Тетрадка, Настройки, Выход; Тетрадка = one screen with tabs Бестиарий · Рекорды · Как играть (with a «Пройти тренировку» button); Авторы = a tab inside Настройки. Extras picked: «Играть» bigger with a permanent underline; a chalk «новое!» mark when a boss page opened or a record was set, until seen. Not picked: brighter yard behind the menu; hiding Обучение (moot — it's inside Тетрадка now). Rejected layouts: centre + corner icons, bottom row, yard hub (hub stays for after the network).
Implemented the same day: title = Играть (fs 100, `ChalkButton.alwaysUnderlined`), Тетрадка, Настройки, Выход. `Screens/NotebookScreen` (`UI/NotebookScreen`: pages Page_Bestiary with `BestiaryScreen` on it, Page_Records, Page_HowTo with «Пройти тренировку»; Q/E, LB/RB switch pages; records moved here from RunScreens). CreditsScreen/RecordsScreen deleted; «Авторы» = 5th settings tab Page_Credits. «новое!» = `UI/NewMark` objects: on the title button (RunScreens.notebookNew), notebook tabs, the Боссы section tab and in boss list labels; flags `BestiaryProgress.IsNew/MarkSeen` (seen when the page is shown) and `RunRecords.HasUnseen/MarkSeen` (set on a new best time, seen when the records page opens). Strings `menu.notebook`, `notebook.*`; `menu.tutorial` removed.
