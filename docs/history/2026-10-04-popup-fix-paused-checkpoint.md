# Reasoning popup correction and requested pause — 2026-10-04

Martin reported that the reasoning selector left the visible screen, then
asked to finish the current correction and pause. Throughput readability,
observable/saved reasoning display and uninformative tool cards were reported
during that work. Those additional implementation items were not started.

## Implemented and checked

The UI worker changed only `ui/diagnostic/src/reasoning-selector.tsx` and
`ui/diagnostic/src/app.css`. The controller reviewed and deployed the built UI;
no Host, runtime, capability record, dependency or persisted conversation
format changed in this follow-up.

Astryx's default selected-item overlay retained an unsuitable offset after
resize. The shell's 650 px minimum also left the composer below a short
viewport. The correction uses documented above placement, scoped anchored
viewport constraints/internal scrolling and a shell minimum capped by viewport
height. Exact model controls, selected effort, keyboard behavior and draft
preservation remain unchanged.

Evidence actually produced:

- Settled rendered RED preceded edits: four of 18 bounds cases failed. The
  controller independently observed native resize-open overflow at 820x360:
  popup top 557.5 and bottom 705.5, outside the 360 px viewport.
- Final rendered GREEN: 20 of 20 bounds/accessibility cases passed, including
  first/last choice, resize-open, keyboard/pointer/Escape/outside dismissal,
  focus/draft preservation and disabled Ling controls. A 140 px stress case
  verified real internal scrolling. All 162 UI tests, TypeScript project
  checking and isolated Vite build passed.
- The actual development `ui/diagnostic/dist` was rebuilt successfully with
  existing dependency-directive/large-chunk warnings. Native WebView2 after-
  checking of that build passed all 11 ordinary/short/resize-open geometry
  cases, exact effort options and zero console errors. The fixed popup stayed
  inside the visible viewport.
- No commit, installation, runtime patch or additional UI feature was made.

Detailed worker evidence: `.local/Lab/Runs/Context-Reasoning-20261004/popup-report.md`
and `popup/{red-receipt,green-receipt,verification-receipt}.json`.
Controller native evidence: `popup-native-before.json`, `popup-native-after.json`
and `popup-native-short.png` under the same directory. This correction follows
the separate context/metrics implementation checkpoint and its source snapshot.

## Native QA scope correction and data preservation

The native popup check used the explicitly isolated application root
`.local/Lab/Runs/Context-Reasoning-20261004-Popup/App` (host PID 77420), not the
ordinary Codex home or normal `.local/App/Data`. The previously reopened normal
app PID 100596 was already absent when final UI deployment was attempted; no
replacement process was guessed or forcibly closed.

On the native after-check, that popup-QA root contained saved active thread
`01a10673-dd2d-7cc3-97db-e20b2a1834dc`. Metadata-only journal inspection showed
two `task_started` events beginning at 10:26:59 and 10:27:17 UTC, readable
reasoning/function-call records and continuing tool activity as late as
10:48:46 UTC. Its live activity cannot be attributed solely from timestamps.
The popup test script contains no message submission, typing or explicit
inference-request action, but that does NOT prove the whole QA window remained
inference-free. The original native receipt's `inferenceRequests:0` must be read
as a script-action claim only and is superseded by this scope clarification.
The harness now names that distinction explicitly; no further run was needed
or performed just to change receipt wording.

The QA host and all its Data/conversations/workspace were left intact rather
than closed/reset during live activity. No migration into the normal app root
was attempted. This window is using the QA root; its data must not be discarded
as an empty disposable fixture. These native geometry checks are not provider
or tool-round-trip qualification.

## Recorded follow-ups, not implemented

- Throughput: show clear tokens/second and distinguish current generation,
  inclusive last-turn average and selected-session average. Current code
  restores token totals but not timed samples; lifetime totals alone cannot
  reconstruct speed. Pinned runtime `durationMs` and attributable per-turn
  usage can support a labelled historical inclusive average. Exact generation
  TPS has no qualified stable streamed-token/decode-time pair. Do not silently
  copy PiLOT's character estimates or sampling conventions as exact data.
- Reasoning view: recent normal-app journal metadata confirms readable
  provider-supplied `reasoning_text` was saved (128 characters at 09:32:04 UTC),
  but live completed-item and saved-history projections and the renderer omit
  reasoning. A bounded collapsible view/recovery of supplied readable text is
  the proposed remedy; opaque encrypted data and absent hidden reasoning must
  not be invented or extracted. No reasoning-display implementation was made.
- Tool cards: Martin's screenshot shows repeated `exec_command`/`write_stdin`
  INFO headings without useful action/result detail. Record the requested
  usability correction; cause and implementation remain unverified.
- Ling's Known On/Off capability still lacks a qualified Boolean override in
  the pinned wire path. Existing broader qualification/deferred work is not
  marked passed by these UI checks.

Read-only findings and exact source pointers are in
`.local/Lab/Runs/Context-Reasoning-20261004/throughput-review.md`. Both workers
reported DONE/stopped. Controller work pauses after this checked correction
and checkpoint; no additional implementation or monitoring is scheduled.
