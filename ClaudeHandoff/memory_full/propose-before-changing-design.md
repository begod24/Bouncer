---
name: propose-before-changing-design
description: "When the user raises a gameplay/design problem, propose options and let them pick before touching code"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 929c9165-0ead-4cb0-be5e-c963f7b54336
  modified: 2026-09-24T12:41:19.525Z
---

When the user brings up a gameplay or design problem («надо подумать над…», «надо как-то сделать…»), first offer concrete options with my recommendation, then wait for their choice before editing code or assets.

**Why:** on 2026-09-24 I started implementing a catch rework straight away, and the user stopped me mid-way: «стой, предложи что можно сделать а не меняй прям щас». Once they had picked from the options (via AskUserQuestion), they asked me to implement right after the choice.

Broad reviews (2026-09-27): when the user brings many points at once (player feedback + balance + content), they want a full numbered list of questions covering every point, each with short context, options and my ★ pick. They answer by number, then we decide what to do and how. Don't jump to a finished proposal.

**How to apply:** a short diagnosis, then 3–5 options (AskUserQuestion works well, with multi-select for combinable options), plus a question about when to implement. Clear bug fixes or explicit instructions ("сделай X") don't need this. See [[game-design-decisions]], [[user-does-playtests]].
