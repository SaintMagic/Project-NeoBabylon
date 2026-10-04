# NB-DEC-013 — Explicit model switching keeps the same chat

Status: **Accepted by Martin on 2026-10-04; implemented with focused fixture and native evidence. Full cross-model continuation qualification remains open.**

## Accepted requirement

An explicit model selection after a turn finishes keeps the same App Server
thread, conversation history, draft and navigation. The next turn uses the
exact selected provider/model and its normalized capability record. It must
not create a new task, clear the transcript, or retroactively attribute old
replies to the newly selected model.

This supersedes the previous implementation choice that changing the model
always starts a new task. NB-DEC-009's prohibition on **silent** substitution
remains: an explicit trusted selection is required to change the next-turn
identity; missing, corrupt or unknown records do not authorize a fallback.

## Implemented direction

- Use the stable pinned App Server APIs. `thread/resume` has explicit `model`
  and `modelProvider` overrides; `turn/start` has a model override. Do not
  replace the thread ID or inject fabricated conversation history.
- Where provider/configuration changes require restarting the private client
  or adapter, resume the same persisted thread and verify the returned exact
  identity before enabling the next turn.
- Preserve past capability/turn attribution. Record explicit selection
  transitions separately rather than overwriting the original binding as if
  it had always described the whole conversation.
- Disable switching during an active turn or other conflicting operation.
  Failed selection preserves the conversation and reports the failure; it
  must not silently execute on either a replacement or stale selected tuple.

## Evidence and limits

The pinned `rust-v0.155.1` source exposes the overrides in
`codex-rs/app-server-protocol/src/protocol/v2/thread.rs` and `turn.rs`.
Source support is not an end-to-end qualification. Focused checks must cover
same thread/history preservation, exact next-turn state, failures and reopen.
NVIDIA's transport-specific reasoning-history limits remain separate; an
unqualified continuation must report its limitation rather than drop required
history. Containment, packaging and broad deferred qualification stay deferred.

On 2026-10-04, focused pinned-App-Server fixtures verified exact same-thread
selection transitions, history preservation, missing-credential and unknown
identity rejection, and clearing a previously selected reasoning effort when
the next model has no known default. The native WPF host switched a persisted
chat from OpenRouter `stealth/space-bunny-alpha` to NVIDIA `z-ai/glm-5.3` and
back, preserving the exact thread ID, transcript, renamed title and unsent
draft. A subsequent live OpenRouter turn recalled the preceding command's
marker from that same conversation. NVIDIA selection/resume is not NVIDIA
live-inference qualification. See the
[provider/conversation checkpoint](../history/2026-10-04-provider-and-conversation-ui-checkpoint.md).
