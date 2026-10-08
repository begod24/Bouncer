---
name: arena-balls-pockets-ui
description: "2026-09-28 answers: up to 7 enemy balls stay on the arena (replaces «enemy balls vanish in 1 s»), Babai's sack/crows take only foreign balls, discard only when pockets are full or at the kiosk (Tab = view only); card-UI rework plan"
metadata:
  node_type: memory
  type: project
  originSessionId: 763f0655-0333-4fc8-b173-87b3d4c72039
  modified: 2026-09-27T23:17:15.914Z
---

On 2026-09-28 the user said the card UI is unclear (how to discard, what cards you have) and that on the Babai boss their balls «просто исчезали». Diagnosis: Babai's «Мешок» swept every loose ball into the sack, the player's own too, and crows carried own balls lying ≥3 m from the player there. In the final there are no other balls (Babai's own vanished 1 s after landing), so he collected exactly the player's 3 balls, and the sack spills only on a hit from behind — with nothing left to throw. Also found: if `ThrowAt` bails (player within 0.5 m), the caught ball used for «Ловец»'s answer stayed Stuck forever in the air.

**User's answers (AskUserQuestion, my ★ picks except the last):**
- Balls: the player's own balls always stay; enemy balls no longer vanish after 1 s — up to 7 lie on the arena, the 8th makes the oldest melt. Anyone can pick any up. A picked-up enemy ball is still one-time (borrowed): after the throw it lies on the arena again as a foreign ball. This REPLACES the 2026-09-27 rule «only player balls stay» in [[feedback-update-decisions]].
- Babai: the sack and the crows take only foreign balls; the player's own stay on the ground. A hit in the sack from behind still spills what he collected.
- Discard: only when pockets are full (on a new pick) and at the kiosk (sell). NOT anytime: Tab only shows the pockets. (My ★ «anytime» was declined.)

**UI plan (user asked «сделай удобный UI»):** pocket strip on the HUD, a view-only pockets screen on Tab / gamepad Select / pause button, a clear «pockets full» replace screen (new card apart, select a pocket card → confirm, «не брать», back to the choice), pocket tags on offered and shop cards (new pocket / same pocket / no pocket / full), shop shows pockets and selling swaps into the pending purchase.

**Implemented 2026-09-28 (uncommitted, not playtested):**
- Balls: `BallDefinition.maxForeignLoose` = 7, `Ball.TrimForeign` / `Vanish` / `ForeignLooseCount`; foreign balls also run `WatchReachable` (unreachable → melt). DuskBoss `CanSack` (only `!IsOwn`), `StuffBall` refuses own balls, `Answer` drops the caught ball if `ThrowAt` bailed; `CrowEnemy.TryFindLooseBall` skips own balls.
- Cards logic: `PlayerCards.NeedFor` → `PocketNeed` (New/Stack/Free/Combo/Full), `GetFreeCards` (current ball type + money cards), the offer survives a full-pockets pick (`IsChoosing` = offer && no pending), `CancelDiscard` back to «1 из 3». `ShopStock.CanBuy/CanSell/SwapAffordable/SellAndBuy`; `Buy` returns PocketsFull only if selling one card can cover the price.
- UI: `UI/PocketCardMini` + `Prefabs/UI/PocketMini.prefab` (120×120, scaled by owners), `UI/PocketStrip` ×3 in GameUI (Hud/Pockets bottom-right with hideGroup; UpgradeScreen/Pockets with names; ShopScreen/Pockets under the pockets button). `UI/PocketsPanel` rewritten (modes View/Replace/Sell; layout built in the prefab: Row on top, Stage with GiveSide/TakeSide/Arrow, Button_Back/Confirm/Skip, Hint). `UpgradeCardView.ShowPocketNeed` pill (PocketTag under Body on all 8 card views). `RunScreens` Overlay.Pockets + Tab/Select in play (pauses, closing resumes) and «Карманы» button in pause. Sprite `Art/UI/Pocket_Slot.png` (`pockets()` in Tools/ui_art.py). 34 UI strings ru/en (`pockets.*`, `pause.pockets`), hud.help/shop.hint mention Tab/Select; removed pockets.discard, pockets.hint.discard, pockets.title, pockets.sell.price.
- Previews: `PocketsPanel` needs a live PlayerCards, so the edit-mode renders mirrored its layout by hand; choice/HUD/shop used the real `PocketStrip.Show` and `UpgradeCardView.ShowPocketNeed`.

**Why:** settles the ball economy and card UX for the next update.
**How to apply:** build to this; don't reintroduce own-ball stealing by bosses/crows. See [[phase-d-decisions]], [[propose-before-changing-design]].
