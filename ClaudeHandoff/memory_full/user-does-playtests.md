---
name: user-does-playtests
description: "Don't enter Unity Play Mode to playtest; user playtests themselves. Always end work with a summary of what was done"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbaed47c-97e6-4cc2-86be-34f626a8403b
  modified: 2026-09-23T09:53:09.720Z
---

Do not run playtests in Unity Play Mode (no ManageEditor Play, no runtime simulation). Compile checks, console error checks and editor-time scene/prefab setup are fine. After finishing a chunk of work, always write a summary of what was done.

**Why:** User said "плейтесты в плеймоде не надо ... я всегда сам их буду делать" and "по окончанию своих работ, пиши что ты сделал".
**How to apply:** Verify via compilation + console only; hand off with a clear "what I did / how to test" summary. Related: [[user-commits-themselves]].
