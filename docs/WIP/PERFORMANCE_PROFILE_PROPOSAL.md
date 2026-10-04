# P5-04 bounded WPF/App Server performance profile — proposal

Status: harness prepared, never executed or qualified. This profile proposes a
reproducible local baseline; it sets workload bounds, not pass thresholds.

Run `tests/qa/performance-soak.mjs` later with an existing **ApprovalQA** WPF
host executable, a Playwright Core entry module, and the exact
`<source>/.local/Lab/Runs` parent. The harness is self-contained and does not
import or modify other QA tests. ApprovalQA permits its synthetic LM Studio
capability record inside a fresh run's `App/Data`; the production Release host
does not accept that path. Record this build flavor when interpreting results;
it cannot by itself establish Release-host performance.

`tests/qa/performance-profile.json` fixes a 180-second workload target,
300-second workload ceiling, 12-second operation cadence, and five-second
process sampling. Code-level caps reject a larger profile: 12 steps, 30 seconds
per step, 90 seconds for setup, 30 seconds for final teardown, and 420 seconds
total. The loopback fixture caps a body at 64 KiB, one in-flight request, eight
accepted Responses requests, and two connections. Its 12 ordered UI steps
comprise six completed deterministic turns, two interrupted streams, two
saved-task reopenings, a diagnostics open/close, and a full WPF/App Server
restart with task resume. The fixture binds only
`127.0.0.1` and returns fixed Responses events; no model weights, key, or
external provider is involved. A run that exceeds a safety bound is recorded
as incomplete, not as a performance failure against an invented target.

Capture prelaunch host/locked-runtime hashes separately from observed launched
WPF and App Server PID, creation time, executable path, and postlaunch on-disk
hash. The in-memory image hash remains **Unknown**; a prelaunch hash is never
proof of the launched image. Also capture built UI dist file hashes, runtime
revision and source-patch hash, Windows/Node/WebView2 identity, CPU model/core
count and installed RAM. For each operation record monotonic elapsed time,
outcome, and fixture request count. Sample host and its observed descendant
App Server/WebView2 processes by PID and creation time, recording working-set
and private bytes. Preserve raw samples and p50/p95/max summaries, launch and
restart times, cancellation latency, and cleanup observations in a unique QA
run root. Only the directly spawned WPF process handle may be terminated.
Descendant PID relationships are observational, never termination authority;
possible App Server/WebView2 orphans remain untouched, visible in evidence,
and make the run incomplete. Late children never observed under the host may
still escape this inventory. Retain Data and evidence for inspection.
Sampling/automation overhead is part of this profile and must be characterized
before interpreting latency.

After implementation work is finished, execute repeat runs under one pinned
hardware/build/profile tuple and inspect incomplete cases and measurement
overhead. Propose numeric latency, memory, and stability thresholds from those
baselines with tradeoffs for Martin's explicit approval. Until then there is
no measurement, threshold, pass verdict, arbitrary-size claim, or service-level
claim. This local fixture does not qualify live-provider performance.
