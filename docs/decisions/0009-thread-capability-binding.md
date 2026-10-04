# NB-DEC-009 — Bind saved tasks to the exact capability record

Status: **Implemented enforcement of the accepted no-silent-substitution
requirement; tamper resistance is not established. Explicit same-chat model
switching is amended by NB-DEC-013; its implementation is in progress.**

Martin explicitly requested same-chat model switching on 2026-10-04. The
implementation description below records the earlier immutable-binding guard,
not a requirement to create a new task after an explicit model selection.
See [NB-DEC-013](0013-same-chat-model-switching.md) for the accepted amendment.

## Accepted requirement

A saved task must not silently resume or accept a new turn under a different
provider/model capability record merely because the provider and model names
still match. NeoBabylon must preserve readable history when that execution
identity cannot be verified, and must not guess or substitute a capability.

## Current implementation choice

NeoBabylon stores one versioned binding per App Server thread under
`<application root>\Data\NeoBabylon\TaskBindings\`. The sidecar records the
thread ID, normalized workspace, provider and model identifiers, creation time,
and a versioned SHA-256 identity of the complete serialized
`ModelCapabilityRecord`. It does not store the raw provider endpoint,
credentials, prompts, or conversation content.

- A new or forked thread is bound only after App Server returns the exact
  provider/model/workspace identity, before NeoBabylon exposes it as executable.
- Resume verifies the binding before `thread/resume`. A changed, missing,
  corrupt, or unsupported binding leaves the transcript available as
  history-only; it is not rewritten or backfilled from the current selection.
- The host rejects new turns and forks from history-only state. The UI disables
  the composer and fork action while keeping “New task” available.
- The sidecar is separate from the source checkout, ordinary Codex home, and
  provider configuration. Clearing the current-task navigation hint does not
  delete these records.

## Limits and evidence

This is a continuity/stale-state guard, not an adversarial integrity boundary.
The hash is unkeyed and the sidecar is a local file; the explicitly unrestricted
tool policy can modify local files, including this sidecar. Tamper-resistant
storage, authentication, and rollback protection are not claimed or added here.

The isolated host harness verifies same-provider/model changed-endpoint
rejection across host restart, preserved transcript, blocked turns, no provider
inference, and missing/corrupt binding behavior without sidecar backfill or
overwrite. It also verifies filesystem-root workspace normalization. These are
mock-provider tests; they do not qualify live provider budgets, model switching
or reloads, or hostile local modification.
