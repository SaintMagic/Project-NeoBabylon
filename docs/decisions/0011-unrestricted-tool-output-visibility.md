# NB-DEC-011 — Normal output from unrestricted tools is visible and forwarded

Status: **Accepted requirement clarification; P3-02 qualification remains open.**  
Date: 2026-09-25.

## Decision

Martin selected the behavior “Show/forward normal tool output” under the
unrestricted same-user authority accepted in [NB-DEC-008](0008-phase2-unrestricted-tool-authority.md).
Ordinary output returned by a model-directed App Server tool is displayed in
the conversation and included in the normal provider continuation. NeoBabylon
does not redact or block that output based on its content.

Because those tools run with the logged-in Windows user's full authority, they
may read user-accessible files and return sensitive content as ordinary tool
output. Such content may therefore be visible in NeoBabylon and sent to the
selected provider. The product does not promise confidentiality for data that
the unrestricted tool can access. This accepts a consequence of the existing
authority decision; it does not grant a new tool, filesystem RPC, or broader
authority.

This does not authorize the host to directly serialize host-managed
credential/configuration values into renderer state, diagnostics, or bridge
responses. The bridge remains a set of named validated operations, not generic
arbitrary RPC. Output must continue to render as inert text through the
existing safe UI path; visible output is not executable markup.

## Evidence and limits

The 2026-09-25 deterministic WPF/WebView2 fixture emitted a synthetic
credential-shaped canary from an isolated command. It appeared in the native
activity card and was received by the loopback provider continuation. No real
credential, real user data, or live provider was used. This verifies that
ordinary output is not redacted on that tested path; it does not qualify every
operation, provider, or output size. See
[`P3-02_EXPOSED_SURFACE_EVIDENCE.md`](../release/P3-02_EXPOSED_SURFACE_EVIDENCE.md)
and [`VERIFICATION.md`](../release/VERIFICATION.md).

P3-02 remains open for the operation-specific denial, timeout/cancellation,
partial-output, and attribution matrix. Existing checks that host-managed
credentials are not directly injected or serialized remain separate; they do
not prove that unrestricted tools cannot independently read user-accessible
secret files.
