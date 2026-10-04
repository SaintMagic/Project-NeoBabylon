# Implementation specifications

Review base: `f126612a00231a58626d72108aac6ee912431ee3` (2026-10-04).

These are proposed implementation contracts, not claims that the features exist. Existing decisions remain authoritative. Names introduced as proposed types/operations are new work, not APIs that can already be called.

| Spec | Deliverable | Primary dependencies |
|---|---|---|
| [S001](S001-settings-and-instructions.md) | Host-owned settings and editable effective instructions | Existing protected records and exact runtime schema |
| [S002](S002-attachments.md) | Native selection/paste, durable attachments, capability-aware send | S001 store conventions; current draft/send reconciliation |
| [S003](S003-reasoning.md) | Provider-native reasoning/summaries/metadata display | S001 preferences; exact protocol evidence |
| [S004](S004-conversation-timeline.md) | Chronological rich conversation, paging and navigation | Patches 001/003/008; S002/S003 only for their item types |
| [S005](S005-provider-profiles.md) | Editable provider/model profiles without substitution | S001; existing capability identities and credential store |
| [S006](S006-nvidia-replay.md) | Restart-safe exact NVIDIA continuation | Existing adapter/bootstrap and protected storage; independent of Settings UI |
| [S007](S007-operation-lifecycle.md) | Long operations, safe retry, drafting/steering and recovery | S001 budgets; existing epoch/turn/command identities |
| [S008](S008-autonomous-workflows.md) | Task scope, safe routine prerequisites and result verification | Existing unrestricted execution; S001 instruction composition |
| [S009](S009-tool-orchestration.md) | Measure tool overhead before bounded discovery changes | Existing request/adapter boundary; no new execution authority |

## Shared implementation rules

Keep Core as the home of pure validation/projection and app-owned record logic; Host owns native file/clipboard access, credentials, process lifecycle and named bridge operations; React renders state and sends validated intentions. Codex App Server remains execution and conversation-history authority. Provider-native replay is not a replacement history database.

Persistent app data belongs below `<application root>\Data\NeoBabylon`; reuse `ApplicationRootLayout`, `RuntimeDataLease` and `ProtectedDataRecordFile`. Do not write to ordinary `~/.codex`, arbitrary user folders, or mutable records under `docs/release`. Add explicit schema versions, bounded fields, atomic replacement/readback and recovery states. Unknown future versions must not be silently reset. Add new fixed keys to `ProtectedDataRecordRecoveryService` deliberately; it currently knows only Projects and ForkBookmarks. Content-addressed immutable payloads need reachability-based cleanup, not deletion based on filename age alone.

Every asynchronous action carries the identity it started with: host/client epoch, canonical workspace identity, thread/turn/item where relevant, capability identity and configuration revision. Recheck before dispatch and before applying the result. Late replies may update retained evidence for their original identity; they must not overwrite another chat's current state. Durable admission receipts and runtime journal reconciliation precede retries of possibly accepted work. No promise of exactly-once execution across an ambiguous transport failure.

Use the exact locked runtime to generate schemas as required by `protocol/README.md`. The public checkpoint omits that executable and generated schema cache. Current online documentation is a behavioral target, not permission to invoke unverified methods. Unsupported operations remain visibly unavailable; no fallback to a different provider/model or a guessed field name.

All UI controls require backing state, validation, save/apply feedback, keyboard operation and failure handling. Preserve Astryx contracts and existing visual language. Defaults proposed here are engineering defaults for implementation, not historical requirements. Do not add approval prompts for ordinary task prerequisites.

## Common acceptance gates

For each slice: pure Core/reducer tests; hostile/malformed and stale-identity bridge tests; persistence crash/backup/restart tests when state is durable; synthetic UI keyboard/focus/error tests; production TS build and host build; then native WPF/WebView2 tests against the exact locked runtime. Provider transport assertions use deterministic loopback fixtures before exact selected-model live qualification. Report each layer separately. Neither successful compilation nor a browser mock qualifies a provider or generated-tool activation.

Source evidence is in the review report and source files named in each spec. The execution graph and patch commands are in `../CODEX_PARITY_IMPLEMENTATION_ORDER.md`; actual results belong in `VERIFICATION.md`.
