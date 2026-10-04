# 2026-09-26 workspace cleanup record

The approved cleanup used the simpler actual workspace layout. Fifty-three
probe source directories are under
`.local/Obsolete/ProbeSources`; `NBRT-RouteControl` and `NeoBabylon-Data` are
under `.local/Obsolete`. The active Codex runtime Git checkout is at
`.local/Runtime/NeoBabylon-Runtime`, and the pinned App Server binary is at
`.local/Runtime/LockedBuild/codex-app-server.exe`. The product runtime lock
points to both runtime paths. The 53-directory count, current paths, runtime
checkout revision, and binary SHA-256 were read back on this date. The binary
hash matches the runtime lock.

The supplied cleanup handoff reports Git worktree repair and
`RuntimeIdentity.LoadVerified` passed. They were not rerun for this
documentation-only update. Subsequent focused QA passed both
`MockProviderError.Tests.ps1` and `MockProbeQualification.Tests.ps1` (exit code
0 each) after their active binary fixture path was updated. An initial attempt
with a stale 2026-09-25 Release host rejected the isolated QA application root.
The current-source Release host was then built on 2026-09-26 with exit code 0,
zero warnings, and zero errors. It opened a WPF window titled `NeoBabylon` and
closed cleanly using
`.local/Lab/Runs/post-cleanup-current-host-20260926T195646Z-92120d3805c2/App`,
with isolated `Data` and `CodexHome` paths.

The native brand-mark fixture passed on a single rerun after a bounded wait was
added to the test fixture for CDP page lookup. It observed the `NeoBabylon`
title, a loaded 29x29 tower mark, diagnostics drawer open/close, and no page or
console issues. Evidence is under
`.local/Lab/Runs/native-brand-mark-fixed-20260926T200323Z-d40f8a8bc63c/`.
No model request or inference occurred. The host smoke checked the locked
App Server binary path and matching expected/observed SHA-256 before launch,
but observed no App Server child process. The executable path actually used
for a host-spawned App Server therefore remains unverified; this is a current-
layout UI smoke, not runtime launch qualification. The default
`%LOCALAPPDATA%\NeoBabylon` application root was left untouched.

This record supersedes the earlier 56-root Lab/ProbeSources/full-inventory
cutover proposal. It records the cleanup and focused post-cleanup QA; it does
not qualify the host-to-App-Server child launch or Phase 2.
