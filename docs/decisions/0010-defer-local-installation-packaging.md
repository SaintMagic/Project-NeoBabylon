# NB-DEC-010 — Defer local installation and packaging

Status: **Accepted scope deferral — 2026-09-25.**

## Decision

Defer the Phase 5 local installation and packaging work for now. Do not build
or rehearse a local package/installer, install NeoBabylon, or qualify an update
and package-recovery path unless Martin requests that work again.

This deactivates P5-03 in the Phase 3–5 execution map. It does not mark P5-03
passed and does not change the current application-root decision, protected
data/migration requirements, notice inventory, or any earlier runtime/provider
evidence. Those remain subject to their own evidence and decisions.

## Rationale

Martin explicitly asked to defer installation and packaging while the rest of
the project continues. Keeping the rehearsal out of the active sequence avoids
making package, update, or install-root assumptions prematurely.

## Reopen condition

Resume P5-03 only after Martin asks to resume installation/packaging work and
the still-open application-root, runtime-provenance, WebView2 deployment, and
data-preservation prerequisites are reviewed. No signing or distribution is
authorized by reopening this local qualification alone.
