# Disabled generated-tool MCP adapter groundwork

This standalone stdio process answers MCP JSON-RPC `initialize`, `tools/list`,
and `tools/call` only. It advertises no tools and returns a typed disabled result
for every direct tool call. It has no activation flag, candidate executor,
shell, credential delivery, or host registration.

The adapter intentionally does not yet read application `Data` or a prepared
binding. The existing `reviewed`/`prepared-disabled` records are not callable
approval, and this isolated project cannot safely establish the host-owned
application root or reconcile its binding/config lifecycle. That integration
and the exact-route qualification remain separate work. Do not point a live
MCP registration at this project as evidence of an approved callable tool.

Each input JSON line is capped at 65,536 bytes and parsed to depth 32; each
output line is capped at 4,096 bytes. A final complete line at EOF is handled,
then the process exits. An oversized line stops the process without attempting
to read or execute it. Stderr never includes request content.
