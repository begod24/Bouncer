---
name: docx-editing-setup
description: "How to edit and check .docx (the concept doc) on this Mac: Python 3.9 only, no LibreOffice/pandoc; working venv + validator workaround"
metadata:
  node_type: memory
  type: reference
  originSessionId: 929c9165-0ead-4cb0-be5e-c963f7b54336
  modified: 2026-09-24T12:41:38.402Z
---

The docx skill's scripts assume Python 3.10+, LibreOffice and pandoc. This Mac has only `/usr/bin/python3` 3.9, and neither LibreOffice nor pandoc. What worked on 2026-09-24:
- Create a venv in the scratchpad and `pip install defusedxml lxml`.
- Copy the skill's `scripts/office` to the scratchpad and prepend `from __future__ import annotations` to every .py. Then import `validators.DOCXSchemaValidator(unpacked_dir, original_docx).validate()` directly; `validate.py` itself uses `match`.
- Edit by unzipping → string-replacing whole `<w:p>`/`<w:tr>` elements in `word/document.xml` → `zip -qXr`. The concept doc's runs are clean, so merge_runs isn't needed.
- Check content with `textutil -convert txt -stdout old.docx` vs new (diff). For a visual check of page 1, use `qlmanage -t -s 1400 -o outdir file.docx`.
- The concept doc is `Game Projects/Vyshibaly_Concept.docx`, outside git. Copy it to the scratchpad before overwriting.

See [[game-design-decisions]].
