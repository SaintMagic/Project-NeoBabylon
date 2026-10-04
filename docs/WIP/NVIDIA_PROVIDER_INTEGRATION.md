# NVIDIA provider integration — 2026-10-04

## Accepted scope

Martin requested integration of his NVIDIA key and application launch, retained
OpenRouter `stealth/space-bunny-alpha`, and selected NVIDIA DS 4.1, GLM 5.3 and
Kimi K3. He explicitly approved an app-private Responses-to-Chat-Completions
adapter after the protocol mismatch was reported. Codex continues to own tools;
the adapter is transport only, rejects unsupported features and never selects
a replacement provider/model. Existing full-access authority and generated-tool
activation restrictions remain unchanged. No runtime-source edit is planned.

## Observed evidence

- Authenticated NVIDIA model discovery returned HTTP 200 and exact identifiers
  `deepseek-ai/deepseek-v4.1-flash`, `z-ai/glm-5.3`, `moonshotai/kimi-k3`.
- Authenticated POST `/v1/responses` for the exact DS target returned HTTP 404.
  GET returned 405, which by itself was not proof of Responses support.
- The pinned Codex provider source permits `responses` and rejects `chat`.
- Official NVIDIA hosted examples use `/v1/chat/completions`.
- A first 256-token DS Chat Completions probe did not return a result within
  the 45-second request deadline. It is not a success or capability qualification.
- A second DS probe with documented numeric effort 1 and 512 tokens, and a
  GLM 5.3 probe with documented low effort and 512 tokens, each timed out before
  response headers at the 55-second deadline. No NVIDIA inference success or
  key-level inference entitlement is established by catalog discovery.
- OpenRouter's current public endpoint metadata still lists the exact Space
  Bunny / Stealth route with 1,000,000 context and 524,288 completion tokens;
  reasoning levels/default remain Unknown in the authoritative record.

The three `MODEL_CAPABILITY_NVIDIA_*.json` records own normalized capabilities.
Evidence sources and gaps are attached to observations. Provider-published
metadata, adapter exposure and live qualification are separate claims.

## Bounded implementation and review

1. Credential worker: secure import into app-local current-user Windows DPAPI
   storage; selected provider capture/delivery and exclusion; launcher/build.
2. Adapter worker: private loopback, identity-bound fixed tuple, text/function
   mapping, reasoning-history preservation, errors/truncation/cancellation,
   deterministic synthetic-key checks. No tool execution in the adapter.
3. Controller: inspect both changes against the fresh pre-change full source
   snapshot; build/read back actual desktop and adapter artifacts; import keys
   without printing them; launch and exercise a bounded exact-tuple path.

Do not silently drop Kimi's required assistant reasoning history, map DS numeric
reasoning effort to invented string levels, claim general Responses equivalence,
or classify completion-token defaults/examples as advertised maximums.

Current pre-change snapshot:
`.local/Lab/Runs/Finishing-20261003/data/captures/20261003T223148Z-b328c56fead94f2390e96553d6ce6ee4`.
Focused evidence goes in `.local/Lab/Runs/Nvidia-20261004`; broad deferred test,
containment and packaging work is not reopened by this integration.

## State

Martin's subsequent native-UI feedback is also in scope: simplify the composer
and move token-cap configuration behind a compact accessible control; add chat
context-menu Rename via the stable `thread/name/set` route; allow exact model
selection once a turn is idle. Martin subsequently clarified that model changes
must keep the **same chat**, like switching Codex models. NB-DEC-013 supersedes
the earlier new-task implementation choice: preserve thread ID, transcript and
draft, apply the exact newly selected capability to the next turn, and retain
historical attribution. No silent substitution is permitted. Rename is a local
metadata operation and must preserve thread ID, transcript and execution
identity.

The implementation is assembled and running, with focused verification in
the [provider/conversation checkpoint](../history/2026-10-04-provider-and-conversation-ui-checkpoint.md).
Synthetic-key adapter checks and an actual locked App Server → mock Chat
Completions → ordinary command → final reply path passed. Protected credential
loading, exact thread Rename and explicit same-chat switching have focused
host/UI evidence. The native host completed a live OpenRouter Space Bunny
command, switched that persisted chat to NVIDIA GLM 5.3 and back without
clearing history/draft, and continued successfully on OpenRouter. Native
NVIDIA adapter startup/resume is observed, not live NVIDIA inference success.
The earlier NVIDIA timeouts remain unresolved. The user-supplied keys never
belong in this document, capability records, source snapshots or diagnostic
output. Broad qualification remains deferred rather than silently passed.
