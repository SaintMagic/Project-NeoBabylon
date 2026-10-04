# S007 — Long-running operations, safe retries and active-turn interaction

Priority: Important. Specification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Current behavior worth preserving

`RuntimeSupervisor` and `AppServerClient` already serialize operations, track client epochs, interrupt turns and reconcile command completion. `InterruptedCommandCompletionTracker`, `ManualCompactionTracker`, `command-stop.mjs`, `drafts.mjs`, and host-failure attribution distinguish several uncertain outcomes. Stop-command identity is stronger than a blanket process kill; preserve it. Compaction has explicit final-outcome handling and usage display; it is not absent.

Gaps: `RuntimeSupervisor` uses ten-minute budgets for turns and manual compaction; `bridge.ts` uses 650,000 ms for those requests and 30,000 ms for most others. `CodexConfigBuilder` fixes OpenRouter/NVIDIA request and stream retries to zero. The composer is disabled while busy, so the user cannot draft a follow-up, queue or steer. `AppServerNotificationProjection` drops command output deltas, leaving long-running tools comparatively opaque. `TurnDiagnostics.Extract` treats an unfamiliar completed-item status as failure while journal extraction already has a tri-state path. These are separate issues, not a reason to replace the execution engine.

## Operation contract

Add a host-owned operation snapshot keyed by operation ID, client epoch, workspace, thread, turn and selected capability/config revision. Suggested states: preparing, submitted, accepted, running, waiting-for-input, interrupt-requested, reconciling, completed, failed, interrupted, outcome-unknown. Transport timeout is an observation, not a terminal execution result. Terminal results must include source/attribution and exact identity; contradictory late events stay visible as reconciliation evidence rather than being dropped or relabeled success.

Separate short bridge acknowledgement from long-running observation where the exact runtime allows it. Proposed named `getOperationStatus(operationId)` and reconnect subscription recover progress after WebView reload without creating another model turn. Keep the authoritative runtime journal and existing pending-submission marker; do not store an independent fabricated conversation. On host restart, reconcile saved admission markers with journal/runtime state before allowing repeat send.

S001 stores configurable overall and idle-observation budgets with explicit inherited defaults. Initially preserve existing ten-minute behavior until the qualified lifecycle is implemented; then allow a user-selected longer/no-overall-limit profile with visible elapsed time. A heartbeat is not proof of work, and an idle warning must not automatically kill a process. Configuration of bridge timeout, host budget and cancellation/reconciliation grace must be coherent so the UI cannot declare failure while the host still owns an unobserved live turn.

## Retry and cancellation

Distinguish pre-admission transport failure, explicit rejection, accepted execution and acceptance-unknown. Retry connection establishment, capability discovery and other read-only requests with bounded exponential backoff/jitter and Retry-After handling where appropriate. Do not blindly increase stream retries or resend a whole turn after a lost response. An installer, patch, email or file mutation may already have happened. First inspect journal/process/result evidence; only retry a side-effecting step after non-execution is established or the operation itself is idempotent.

Turn interruption remains addressed to exact thread/turn. Command stop preserves process/item/start identity and the existing late-completion checks. 'Stop requested' differs from 'Stopped'; missing acknowledgement becomes Unknown. A provider timeout does not authorize killing unrelated applications or descendant PIDs merely because they resemble a previous command. Routine alternative approaches remain model initiative under the same task scope, not provider/model substitution.

Fix `TurnDiagnostics.Extract` to emit succeeded/failed/unknown consistently with its journal path: explicit error, unsuccessful result or nonzero exit is failure; known successful completion is success; an unfamiliar status without such evidence is unknown. Add regression tests through producer and React terminal reconciliation, including null success. Do not use `succeeded=false` as shorthand for 'not yet known'.

## Drafting, queue and steer

Split submitted input from editable next-draft state in `App.tsx`/`drafts.mjs`. Allow typing while busy without mutating the already sent text, attachment manifest or pending receipt. The primary action is explicit Queue next message when a turn is active. A queued item is bound to thread/workspace and draft revision; switching chats cannot accidentally drain it into the new chat. After restart show retained queue state and require reconciliation before dispatch, not automatic duplicate execution.

Steering is separate from queueing. Current official App Server docs require `expectedTurnId` and no turn-level overrides for `turn/steer`; generate/check the locked schema first. Add a named host `steerTurn` only when supported and tested. Reject stale expected turn, model/workspace changes and unsupported provider behavior. A successful steer returns the accepted turn ID and is rendered as user input in that same turn. Do not label queueing as steering or append a fake user message before acceptance.

## Observability and output

Expose elapsed time, last event time, pending approval/input, active tool identity, request/retry count and cancellation state without inventing percent progress. Bounded command output deltas need per-item budgets, backpressure, final-item replacement and restored source inspection. Do not remove current 40k/120k display bounds. Live activity ordering belongs to S004. Token/context usage distinguishes observed, estimated and unknown; manual compaction remains a separate operation with its own final receipt.

## Tests and acceptance

Extend existing `native-turn-interruption`, `native-command-stop`, pending-send/draft-recovery and compaction fixtures rather than creating only happy-path mocks. Test reply lost before/after admission; duplicate bridge replies; WebView reload mid-turn; stale epoch completion; long silence then success; cancel races; provider 429/401; unknown tool outcome; queued attachment followed by model switch; disk-full draft persistence; unsupported steer; exactly matching steer; and two commands with recycled-looking IDs.

Acceptance: a long deterministic task remains observable and cancellable across UI reload, a deliberately lost acknowledgement does not cause a second execution, next-draft text survives busy state, and unknown outcomes remain unknown until matching evidence arrives. Native and exact-provider tests are required. Implement tri-state correction and operation snapshot first, budgets/recovery second, drafting/queue third, steer and output-delta UX last. No broad runtime rewrite or automatic retry of irreversible work.
