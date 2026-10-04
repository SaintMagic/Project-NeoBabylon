# Tools and self-scaffolding

Status: **Accepted toolbox goals plus proposed candidate workflow.** The name
“Bricks” is intentionally not used as a schema.

## Toolbox categories

The catalog should make these categories discoverable without implying that
every installation has every capability:

- workspace and file inspection;
- search, navigation, and code intelligence;
- patching, formatting, and test/build execution;
- Git/review and evidence capture;
- documentation and project artifacts;
- MCP-backed external services;
- skills and instruction packages;
- hooks and lifecycle automation;
- plugins that bundle reviewed skills and/or MCP servers;
- experimental generated tools.

Each maintained entry needs a stable identifier, description, input/output
schema, required permissions, data destinations, timeout/cancellation
behavior, failure contract, version/source, and verification evidence.

## Upstream dynamic-tool evidence

The pinned App Server exposes `dynamicTools` on `thread/start` as an
experimental field. The official docs say dynamic tools persist in thread
rollout metadata and are restored on `thread/resume` when not replaced. A
model-driven call is delivered through the experimental `item/tool/call`
server request/response flow. The pinned tests cover normal calls, structured
content, invalid inputs, missing namespaces, and invalid remote media.

This proves a callback and persistence path, not arbitrary hot reload, safe
code generation, or promotion into a trusted toolbox. Dynamic native
registration remains outside the normal Phase 1 capability set.

## Candidate location and template

The proposed durable candidate root is:

```text
<NeoBabylon root>\Data\GeneratedTools\<tool-id>\
├── tool.json
├── README.md
├── src\
├── tests\
└── evidence\
```

The names are implementation proposals; the fixed durable location and
machine-readable candidate contract are the requirements. Candidates must not
be scattered through arbitrary repositories, temporary folders, or global
plugin locations.

Suggested manifest coverage:

| Group | Information |
| --- | --- |
| Identity | Schema version, stable tool ID, name, purpose, candidate revision |
| Origin | Missing capability, originating task/session, timestamp, creator/provider metadata when observable |
| Execution | Relative entry point, invocation/input/output contract, dependencies |
| Authority | Required permissions, filesystem scope, network/data destinations |
| Evidence | Source hashes, tests actually run, outcomes, limitations, execution traces |
| Lifecycle | Unapproved/review/integrated/rejected status, reviewer decision, replacement/revocation links |

“Signature JSON” means a recognizable structured record. A manifest or hash is
not a cryptographic signature or evidence of quality. Descriptive fields do
not grant execution authority; candidate paths and invocation are validated
by the selected host/runtime integration.

## Normal candidate lifecycle

```text
capability need
  → suitable existing tool or reasonable composition, when available
  → genuine gap remains
  → generated candidate from the standard template
  → validate/test under the task's existing permissions
  → use for the authorized task when those permissions allow
  → retain as unapproved in Data\GeneratedTools
  → show in Unapproved Tools drawer
  → Martin manually reviews when he chooses
  → improve, integrate/promote, retain, reject, or discard deliberately
```

An unapproved helper may be created, tested, and used during the current task
through the ordinary authorized model shell/tool path. It does not need a
permanent promotion or independent Codex review before every temporary use.
This is not permission to broaden the task's authority, install dependencies
silently, or access unrelated Data content.

If a candidate requests a new permission, dependency, network destination, or
data scope, normal authority handling applies. A denied permission is not a
missing tool and cannot be bypassed by changing the manifest.

## Explicit prohibitions and promotion boundary

- Model-controlled candidate execution must never use App Server
  `thread/shellCommand`, which upstream documents as outside the thread
  sandbox with full host access and for explicit user commands only.
- The model cannot promote, silently register, or grant its own candidate new
  authority.
- Candidate creation/use, manual review, integration/activation, and Git
  commit authorization are separate decisions.
- Editing the reviewed content invalidates approval for that changed version;
  the historical review remains recorded.
- The integrated maintained tool may differ from the original candidate;
  provenance must link the actual artifacts reviewed and activated.
- Codex review is independent and initiated by Martin; it is not a runtime or
  paid-provider dependency.
- No silent installation, package download, hook activation, MCP connection,
  provider change, or permission-system rewrite occurs as scaffolding.

## Unapproved Tools drawer

The future UI should expose retained candidates in an Unapproved Tools drawer
or equivalent discoverable surface. Martin must be able to inspect purpose,
origin, source, permissions, dependencies, recorded results, and actual file
location. Buttons, sorting, export, and review handoff remain implementation
details. A manual handoff to real Codex is sufficient initially; automatic
integration is not required.
