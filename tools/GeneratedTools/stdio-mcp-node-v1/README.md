# Inert `stdio-mcp-node-v1` candidate starter

This folder is a source template, not an installed tool, reviewed candidate, or
callable MCP server. It contains no dependency installer and no live handler.
Copy it into the product's candidate area, replace every `REPLACE_...`
placeholder, add bounded evidence files, and compute exact lowercase SHA-256
values for every listed file before creating a candidate. The `.template`
files are deliberately not accepted as candidate manifests.

The only route identifier currently described by Core is
`stdio-mcp-node-v1`: a `.mjs` entrypoint supplied to Codex's existing stdio MCP
path. This source track's route, locked-runtime, and selected provider/model
qualification switches are all closed. Reviewing this template cannot enable
it. Candidate-declared permissions remain informational under the product's
current `danger-full-access` / `never` authority.

Keep dependencies empty unless a future, separately reviewed route contract
accepts the exact package inventory format. Do not add installers, candidate
selected commands/arguments, subprocess runners, shell fallback, or additional
tool names. An implementation must declare one exact input and output JSON
Schema and return only that tool's documented output. The starter handler
throws by design and must never be treated as useful functionality.
