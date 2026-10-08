---
name: day-arenas-dusk-only-site-final
description: "2026-09-28: Rink and Bazaar are daytime; Site, Kindergarten (night returned the same day) and the final stay dark; KG shadows replaced by scarecrows — what was changed"
metadata:
  node_type: memory
  type: project
  originSessionId: c222a523-c78e-44d8-ab98-7c07f3c664f3
  modified: 2026-09-28T12:23:50.181Z
---

On 2026-09-28 the user said: «сделай все уровни дневными кроме стройки и финала, и там только могут быть shadows, а чучела могут быть и днём».

Done the same day (not playtested):
- `Arena_Rink`, `Arena_Bazaar`, `Arena_Kindergarten` timeOfDay = [TimeOfDay_Day] only (were Evening→Dusk / SiteDusk→SiteNight). Yard stays Morning→Day; Site (SiteDusk→SiteNight) and Final (YardNight) unchanged. Scene TimeOfDayController keys in Rink/Bazaar/Kindergarten set to Day too (edit-mode preview only).
- Kindergarten.unity: removed the `Darkness` object (Core/DarkArena), so no player light circle / flashlight there. Kg_Lamp, Kg_Veranda, Kg_Veranda_B: LampLight.alwaysOn off (lamps follow palette emission = off by day).
- Wave_Kindergarten: the «Тени» track → «Чучела» (Scarecrow, group 1→1, maxAlive 3; ~126 hits vs 132 before at ×2), Elite_Shadow variants → Elite_Scarecrow. Shadows now spawn only on Site (+ Babai's lights-out summons in the final). Bestiary «where» updates itself from the waves.
- Titles ru/en: «Хоккейная коробка · день», «Барахолка · день», «Детский сад · день» (+ The … · day).

**Why:** the user wants darkness to be special to the Site and the final.
**How to apply:** don't add Shadow/Elite_Shadow or DarkArena to day arenas; scarecrows are fine anywhere. My choice of putting scarecrows into KG in place of shadows is changeable. See [[phases-v-g-decisions]], [[stage3-content-decisions]], [[spawn-balance-arenas-2-3]].

**Update, same day (after the user committed «Tweaks to arenas»):** «верни так же детскому саду ночь». Kindergarten is dark again: Arena_Kindergarten SiteDusk→SiteNight, `Darkness` (DarkArena) back in Kindergarten.unity, Kg lamps alwaysOn again, title «Детский сад · сумерки» / «The Kindergarten · dusk» (restored from commit 933d847f). The wave was NOT reverted: KG still has scarecrows instead of shadows; I offered to bring the shadows back.
