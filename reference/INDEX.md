# Reference index

Status: Phase 0 evidence snapshot, retrieved 2026-09-22. This index records
what was acquired and inspected; it does not claim that NeoBabylon has a
working runtime or provider connection.

## Baseline

| Field | Recorded value |
| --- | --- |
| Upstream | `https://github.com/openai/codex.git` |
| Release | `rust-v0.155.1` / `0.155.1` |
| Release publication | 2026-09-18T20:03:04Z |
| Annotated tag object | `4e21628f9ec9ee656650cd2b62ef92225725b5ac` |
| Resolved commit | `be2951ea34f0d295ed0becf97079f92fa5f6950e` |
| Platform artifact | Windows x86_64, MSVC |
| App Server binary | `codex-app-server 0.155.1` |
| CLI binary | `codex-cli 0.155.1` |
| Source archive SHA-256 | `2DC56C1DB2CC3FB44FC8F132E1CB7064EE3188EE8F811580264BBC672AF0D442` |
| App Server SHA-256 | `253C6D8424EA45BA9D36D1F665D9B7B4C917782D4CF5D56E61CD0664CC52BF0D` |
| CLI SHA-256 | `EBA0F32C976667CB9298EFAFD98513E823EEDA7B576A03EC658BB8BE8D336316` |
| Retrieval policy | Stable release selected; prerelease and unpinned `main` are comparison-only |

The release and tag metadata are retained in the cache metadata directory.
The tag object was not marked cryptographically verified by the GitHub API;
the archive and binaries are therefore recorded by URL, commit, version, and
hash rather than treated as a signed provenance claim.

## Acquired layout

```text
reference/
  INDEX.md
  README.md
  sources.json
  cache/
    codex/rust-v0.155.1/
      source/                         # extracted upstream source
      metadata/                       # release, tag, archive, and hash records
      binary-x86_64-pc-windows-msvc/  # matching App Server executable
      binary-cli-x86_64-pc-windows-msvc/ # matching CLI and helpers
      protocol-generated/             # generated stable/experimental contracts
    docs/2026-09-22/                  # official Markdown snapshot
    supplemental/                     # comparison captures and unavailable record
```

The source extraction contains 7,688 files, 918 directories, and 76,382,873
bytes. It retains upstream `AGENTS.md`, tests, build metadata, `LICENSE`,
`NOTICE`, SDKs, protocol schema trees, and source layout. The source tree was
not built or modified.

## Generated protocol outputs

The matching `codex-cli 0.155.1` generated all four outputs in an isolated
project-local data directory:

| Output | Files | Bytes | Command mode |
| --- | ---: | ---: | --- |
| `stable-typescript` | 721 | 418,679 | `app-server generate-ts --out <dir>` |
| `experimental-typescript` | 861 | 506,538 | `app-server generate-ts --out <dir> --experimental` |
| `stable-json-schema` | 312 | 3,518,717 | `app-server generate-json-schema --out <dir>` |
| `experimental-json-schema` | 437 | 4,246,627 | `app-server generate-json-schema --out <dir> --experimental` |

These are reference outputs. A future product build should regenerate or
vendor bindings only after the product-side runtime manifest/lock and
protocol version policy are accepted.

The generated files are indexed separately at
`cache/codex/rust-v0.155.1/metadata/protocol-generated.sha256.json`; the
acquisition archive index predates these derived outputs.

## Official documentation coverage

The official `llms.txt` index and 18 linked Markdown pages were captured from
`https://learn.chatgpt.com/docs/` on 2026-09-22. The set covers the App Server,
manual, configuration, providers/models, permissions/sandboxing, hooks,
skills, plugins, MCP, SDK, feature maturity, and open-source notices. Each
local file's SHA-256 is in [sources.json](sources.json).

The optional OpenAI Codex harness article could not be captured by the local
PowerShell downloader because the site returned HTTP 403 with a Cloudflare
challenge. It remains explicitly unavailable in the supplemental status
record; no local placeholder was used as evidence.

## Inspection map

The following paths are the primary upstream implementation evidence:

| Concern | Pinned source or docs |
| --- | --- |
| App Server CLI and transport selection | `source/codex-rs/app-server/src/main.rs`, `source/codex-rs/app-server-transport/src/transport/` |
| JSONL stdio reader/writer | `source/codex-rs/app-server-transport/src/transport/stdio.rs` |
| Protocol generation | `source/codex-rs/app-server-protocol/src/precomputed_exports.rs`, `source/codex-rs/cli/src/main.rs` |
| Lifecycle, queueing, and recovery | `source/codex-rs/app-server/src/message_processor.rs`, `source/codex-rs/app-server/src/daemon_thread_recovery.rs` |
| Durable thread and rollout history | `source/codex-rs/thread-store/`, `source/codex-rs/rollout/`, `source/codex-rs/history/` |
| Provider wire API and OSS defaults | `source/codex-rs/model-provider-info/src/lib.rs`, `source/codex-rs/ollama/src/lib.rs` |
| Dynamic tools | `source/codex-rs/app-server/src/dynamic_tools.rs`, `source/codex-rs/app-server/tests/suite/v2/dynamic_tools.rs` |
| Full-host shell surface | `docs/app-server.md` lines 315, 847-853; `thread/shellCommand` is explicit-human-use and outside the thread sandbox |
| Permissions and Windows sandbox | `source/codex-rs/sandboxing/`, `source/codex-rs/execpolicy/`, `source/codex-rs/app-server/src/command_exec.rs` |
| Skills, hooks, MCP, and plugins | `docs/build-skills.md`, `docs/hooks.md`, `docs/mcp.md`, `docs/build-plugins.md`, corresponding source crates |

## Search examples

Use `rg` against the pinned source and documentation snapshot rather than the
unversioned network. For example:

```powershell
rg -n "dynamicTools|item/tool/call" reference/cache/codex/rust-v0.155.1/source/codex-rs reference/cache/docs/2026-09-22
rg -n "WireApi|Responses|wire_api" reference/cache/codex/rust-v0.155.1/source/codex-rs/model-provider-info
rg -n "thread/start|thread/resume|turn/start|turn/interrupt" reference/cache/docs/2026-09-22/app-server.md
```

## Limitations

- No live Codex App Server session was started in Phase 0.
- No hosted or local provider was contacted; model generation, tool-call
  reliability, continuation, cancellation, and latency remain unverified.
- No Codex source build or full upstream test suite was run.
- Generated TypeScript and JSON Schema outputs are indexed reference inputs,
  not a substitute for runtime compatibility tests; stable and experimental
  outputs must remain separate when a product client is built.
- The baseline is an unsigned annotated Git tag as reported by the GitHub API;
  the recorded commit and hashes make the local snapshot reproducible but do
  not replace a future supply-chain policy.
- The current-main provider source and Astryx captures are unpinned
  comparison material, not implementation baselines.
