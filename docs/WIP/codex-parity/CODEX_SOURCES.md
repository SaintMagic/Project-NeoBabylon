# Current Codex comparison sources

Accessed 2026-10-04. These are official public references, not observations of Martin's installed desktop. The public product checkpoint's pinned runtime is older/separate; newer methods require schema generation and qualification against that exact executable before implementation.

| Official source | Bounded fact used | NeoBabylon disposition |
|---|---|---|
| [Codex App Server](https://developers.openai.com/codex/app-server/) (redirected to [ChatGPT Learn App Server](https://learn.chatgpt.com/docs/app-server)) | Documents typed text/image/localImage inputs; structured item/lifecycle notifications; reasoning summary deltas; thread/turn operations; steering with an expected active turn identity | S002/S003/S004/S007 use these as behavioral targets, not placeholder calls into an unverified protocol |
| [Codex app settings](https://developers.openai.com/codex/app/settings/) (redirected to [Settings reference](https://learn.chatgpt.com/docs/reference/settings)) | Documents configurable preferences and custom instructions through personal AGENTS.md, including appearance/personality concepts | S001 provides an explicit effective-instruction and settings pipeline while retaining NeoBabylon's separate data/home boundaries |
| [OpenAI reasoning guide](https://developers.openai.com/api/docs/guides/reasoning) | Readable summaries, hidden reasoning tokens and opaque continuation are different things; readable reasoning is not universally exposed | S003 distinguishes summary/provider-readable/metadata/none/restricted/opaque channels; S006 never reconstructs exact replay from displayed text |

No large passages are copied from these sources. Proposed stores, state machines, limits and UI behavior in the specifications are engineering recommendations grounded in NeoBabylon source and the user's intent, not requirements attributed wholesale to Codex documentation.

## Comparison boundaries

The settings/features documentation currently redirects into broader ChatGPT Learn pages. That does not establish identical controls in every Codex desktop platform/version. This review did not run the current closed-source Codex app and does not invent a precise release/version parity claim. Repo source confirms NeoBabylon's missing paths independently of those naming changes.

The durable source anchors are `runtime/runtime-lock.json`, `protocol/README.md`, Core `AppServerProtocol.cs`, `CodexConfigBuilder.cs`, `CodexModelCatalogBuilder.cs`, `AppServerNotificationProjection.cs`, the Host supervisor/bridge, and the NVIDIA adapter. Preserve generated schema ownership: obtain it from the exact locked runtime, not by copying examples from a newer webpage.

For general settings, attachments, rich conversation and autonomous workflow, the user's explicit 'Codex, but mine' product intent is itself a requirement. The review does not need to invent undocumented Codex internals to justify making those capabilities usable. Existing NeoBabylon strengths—exact heterogeneous provider identity, explicit fail-closed behavior and app-owned data—should be retained even when a reference product differs.
