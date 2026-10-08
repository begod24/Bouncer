---
name: user-commits-themselves
description: Never git commit or push in this repo — the user always makes commits themselves
metadata:
  node_type: memory
  type: feedback
  originSessionId: fbaed47c-97e6-4cc2-86be-34f626a8403b
  modified: 2026-09-23T09:14:32.072Z
---

Never run `git commit` / `git push` in the Bouncer repo. Make changes, leave them uncommitted, then report what was done; the user reviews and commits.

**Why:** User explicitly said "коммит я ВСЕГДА сам делаю" after I committed .gitignore/.gitattributes and had to reset it.
**How to apply:** Stop after edits; optionally suggest a commit message. Checking staged files / reviewing their commits on request is welcome.
