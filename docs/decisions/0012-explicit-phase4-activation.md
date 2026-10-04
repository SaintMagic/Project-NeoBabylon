# NB-DEC-012 — Separate explicit activation of a reviewed tool

Status: **Accepted manual-act requirement; callable route, qualification, and activation implementation open.**  
Date: 2026-09-27.

## Accepted decision

Martin selected a separate, explicit **NeoBabylon Activate** confirmation as
the manual act required before a generated-tool candidate can become
callable. The confirmation must show the exact currently reviewed candidate
content/authority identity and the requested permissions. A prior **Review**
decision or **Prepare disabled** binding does not grant callable access, and
neither action may silently become activation. Changed content or requested
permissions require a fresh exact review before any activation confirmation.

Activation is a distinct host-owned transition, not a candidate-authored
instruction, App Server approval, or generic renderer RPC. The UI has **no
Activate action today**. Revoke and logical Cleanup remain separate explicit
disabled-state transitions; Cleanup does not erase candidate or review
evidence.

## Authority and open gates

The selected execution authority remains the logged-in Windows user's full
access under [NB-DEC-008](0008-phase2-unrestricted-tool-authority.md).
Containment is deferred. An eventual confirmation must not describe the tool
as workspace-contained or imply that a displayed permission declaration
technically confines full-access same-user commands.

This decision settles **the separate local manual act and what its
confirmation must display**; it does not establish independent reviewer
identity or tamper-proof evidence. It does not select or qualify the provisional
app-private stdio MCP route, implement activation, expose an Activate button,
or accept Phase 4. Task 7 still requires final-current-lock deterministic
registration/call/disabled-revoked reload/restart qualification and the exact
selected real provider/model tuple. Until the callable route is accepted and
both gates pass, activation remains disabled and candidates remain
non-callable. See the open [Task 7 and Task 9 plan](../WIP/FINISHING_THE_PRODUCT.md).
