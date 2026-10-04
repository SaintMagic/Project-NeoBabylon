# Tooling boundary

This directory is for maintained, explicitly reviewed NeoBabylon tools. It is
separate from Codex's runtime tool catalog and from generated helper tools.
Tool candidates must record their permissions, inputs, outputs, failure modes,
and promotion evidence before they are made available to an agent.

Phase 1A also contains `Phase1AQualification`, a bounded diagnostic runner for
LM Studio discovery, deterministic mock-first ordering, capability recording,
and the pinned App Server live attempt. Pass
`--windows-sandbox-unelevated` only when explicitly qualifying Codex's
restricted-token Windows backend; omission deliberately exposes the default
read-only projection. It is qualification tooling, not a promoted NeoBabylon
tool or a substitute for the runtime's ordinary tool authority.
