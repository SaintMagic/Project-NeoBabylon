# Decision records

Decision records are the canonical place for NeoBabylon choices that affect
scope, ownership, trust, compatibility, or future maintenance. They are
deliberately separate from the product brief and build plan.

## Status vocabulary

- **Accepted** — Martin has selected the direction and implementation may
  rely on it within scope.
- **Proposed** — a recommendation prepared for review; not an authorization
  to implement a materially different boundary.
- **Open** — evidence or a user choice is still required.
- **Superseded** — retained for history but replaced by a later record.

## Current records

| ID | Subject | Status |
| --- | --- | --- |
| NB-DEC-001 | Codex Phase 0 baseline | Accepted for Phase 0; proposed for Phase 1 |
| NB-DEC-002 | Runtime source ownership | Accepted boundary; implementation details open |
| NB-DEC-003 | Windows desktop shell | Accepted direction; qualification details open |
| NB-DEC-004 | Initial provider qualification targets | Accepted targets; exact tuples open |
| NB-DEC-005 | Application data and credential boundary | Accepted boundary; path/migration details open |
| NB-DEC-006 | Request-level OpenRouter endpoint pinning | Implemented for Phase 1B qualification |
| NB-DEC-007 | Windows legacy-command containment gate | Fail-closed mitigation; contained tool execution open |
| NB-DEC-008 | Phase 2 unrestricted tool authority | Accepted sequencing and authority; qualification in progress |
| NB-DEC-009 | Exact capability identity for saved tasks | Implemented stale-state guard; explicit switching amended by NB-DEC-013; not tamper-proof under unrestricted tools |
| NB-DEC-010 | Defer local installation and packaging | Accepted scope deferral; P5-03 inactive until requested |
| NB-DEC-011 | Normal output from unrestricted tools is visible and forwarded | Accepted requirement clarification; P3-02 qualification remains open |
| NB-DEC-012 | Separate explicit activation of a reviewed tool | Accepted manual act; callable route, qualification, and implementation open |
| NB-DEC-013 | Explicit model switching keeps the same chat | Accepted; implementation in progress |
| NB-DEC-014 | Public experimental product-source repository, initial commit and push | Accepted; private state/startup briefs and custom runtime payload remain excluded |

An implementation should link the applicable record and verification evidence
instead of restating an unqualified proposal as fact.
