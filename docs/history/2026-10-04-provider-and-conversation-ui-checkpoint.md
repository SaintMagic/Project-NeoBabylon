# Provider and conversation UI checkpoint — 2026-10-04

Status: **Implemented; focused fixture and native follow-up, not full-product acceptance.**

## Accepted scope

Martin requested NVIDIA key integration and application launch, exact NVIDIA
DS 4.1 / GLM 5.3 / Kimi K3 selections and OpenRouter Space Bunny, approved an
explicit Responses-to-Chat-Completions adapter, and requested a less cluttered
composer, right-click Rename and model switching after a turn. His subsequent
clarification requires the **same chat** across a model change (NB-DEC-013),
not the previously implemented new-task behavior. Original history, draft,
navigation and past attribution are preserved; the explicit new selection
applies only to subsequent turns. No commits, remote changes or shared
provider/Codex configuration are authorized by this checkpoint.

## Implemented changes

- Secure interactive credential import and application-private Windows
  current-user DPAPI records; launcher/build child credential exclusion;
  opaque host-only capture and selected-provider delivery.
- Three evidence-backed NVIDIA capability records and a fixed-tuple private
  adapter. The adapter translates bounded text/function Responses requests to
  NVIDIA Chat Completions; Codex executes tools. Unsupported features, model
  mismatch, missing terminal events and reasoning-history loss fail visibly.
- Explicit same-thread capability transitions preserve the original binding
  and append selection history. Active operations/continuing commands block
  conflicting switches. Missing credentials or unknown identity do not cause
  a new task or fallback.
- Workspace/thread-bound Rename via stable App Server APIs with runtime
  readback. Sidebar context menu and keyboard menu, accessible dialog, and
  preserved transcript; compact Options hides the completion-cap editor until
  requested, retaining the accepted provider-max/32768-fallback/override rule.

## Actual focused evidence

Reports/receipts are under `.local/Lab/Runs/Nvidia-20261004`:

- `credentials/report.md`: synthetic DPAPI import, invalid/missing-record
  handling, build/process credential exclusion, NVIDIA configuration, Rename
  identity checks, pinned-runtime same-thread switch fixture, reasoning-effort
  clearing, and the native-discovered legacy turn-start credential gate fix.
- `adapter/report.md` and `adapter/locked-appserver-roundtrip.json`: production
  publish and deterministic protocol/authentication/error/stream/tool fixtures;
  **actual locked App Server** made two mock Chat Completions requests and
  completed an ordinary command and final reply with zero stderr. This is
  mock-provider evidence, not NVIDIA inference.
- `ui/report.md`: focused history/drafts/switch/Rename checks, typecheck and
  production build. The controller's final six-file UI rerun exited 0 (41 tests).
  Existing dependency module-directive and bundle-size advisories remain.

Fresh native WPF/WebView2 evidence used application root
`D:\CODING\NeoBabylon\.local\App`, isolated `Data\CodexHome`, ordinary Codex
root use `false`, and App Server **0.155.1**, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.

The tested persisted chat is `01a10463-0d37-7291-acf2-212a9ba73f5b`:

1. OpenRouter `https://openrouter.ai/api/v1`, exact model
   `stealth/space-bunny-alpha`, requested Stealth route, completion override
   2048, explicit `danger-full-access` / `never`.
2. The model called ordinary `exec_command` with
   `cmd.exe /d /c echo NB_NATIVE_TOOL_OK`. The isolated runtime journal records
   the function call at `2026-10-04T00:49:28.541Z` and matching call-ID result
   at `00:49:30.499Z`, exit 0 and output `NB_NATIVE_TOOL_OK`. The UI showed a
   successful App Server command and final reply. The model's prose incorrectly
   called this a human host command; the actual function-call journal, not
   that prose, establishes the ordinary model-directed path.
3. Right-click Rename confirmed `NeoBabylon smoke check` in sidebar/header.
4. OpenRouter → NVIDIA `z-ai/glm-5.3` → OpenRouter retained the exact full
   thread ID, transcript, renamed title and draft text. NVIDIA startup exposed
   a selected-model private loopback adapter, composite bundle hash, no
   fallback, and host-held credential presence only. **No NVIDIA inference was
   made during this selection test.**
5. A subsequent live OpenRouter turn at `2026-10-04T00:54:33.767Z` recalled
   the exact earlier marker from the same conversation. Its journal effort was
   absent/Unknown, not an inherited GLM default or invented GPT setting.

The user's original `hey` conversation was retained. The first empty QA task
exposed the old credential gate and could not resume because no turn had
persisted; the failure was shown, not silently replaced. A fresh QA task was
used after fixing that gate. No user conversation or durable data was deleted.

## Limits and remaining work

Authenticated NVIDIA catalog discovery returned the three exact IDs, but
Responses POST returned 404 and bounded direct DS/GLM Chat Completions probes
timed out before headers. Key-level inference entitlement, actual serving
identity, real NVIDIA tool behavior and provider-effective context remain
unqualified. The adapter's warm Kimi reasoning replay passed a mock tool loop;
required replay is in-memory/session-bound, not qualified across cold restart
or cache eviction. Images, native hosted search, structured output and other
unmapped Responses features are not silently enabled.

This is not a full credential/security/recovery/compaction/performance or
accessibility verdict. Generated-tool activation remains closed; packaging,
containment and the remaining deferred Phase 1B work remain deferred.

DPAPI/private delivery prevents incidental direct host/renderer disclosure;
it is not a confidentiality boundary against authorized unrestricted commands
running as the same Windows user. NB-DEC-011's normal-tool-output contract is
unchanged.

## Final native follow-up

The first native switch exposed an upstream-default history filter: an omitted
`modelProviders` lists only the current provider. The reviewed product fix now
uses explicit `modelProviders: []` for all providers within the exact selected
workspace. Cross-provider bookmark supplementation still requires exact
runtime identity. The focused mixed-provider/foreign-workspace pinned-runtime
fixture passed; no runtime source was changed.

Advertised provider context, effective provider context, observed Codex task
context, advertised tools and selected-tuple qualification are now separate
UI fields. Known GLM advertised context is 1,048,576; effective provider context
and real NVIDIA tool qualification remain Unknown.

After those fixes, the actual `.local/App/Build` Host was rebuilt in Release
with **0 warnings / 0 errors** and restarted. Native checks confirmed:

- exact chat `01a10463-0d37-7291-acf2-212a9ba73f5b`, renamed title, both live
  turns and unsent draft restored without a new turn/replay;
- another explicit GLM → OpenRouter round trip preserved that ID/history/draft;
- the original OpenRouter `hey` chat stayed visible even with NVIDIA selected;
- the advertised/effective/qualification fields rendered separately;
- the completion-cap editor was hidden until Options was opened; the neutral
  dark composer and compact sidebar were visually inspected.

Only the authored QA draft was cleared afterward. Native WebView screenshots
are `.local/Lab/Runs/Nvidia-20261004/native-final.png` and
`native-glm-final.png`; these are WebView pixels, not an OS-title-bar or broad
accessibility/visual acceptance test.

The actual rebuilt artifacts were read back and hashed (apphost alone is not
the source implementation):

| Artifact | SHA-256 |
| --- | --- |
| `NeoBabylon.Host.dll` | `a548ac9c2fa1cdcb204a1b32b1d2c40d2d3f6c8b9df60ab8585bb5251e97150e` |
| deployed `NeoBabylon.Core.dll` | `d6313422961f92b0b0e6043cc6993df179a7a0250c462f66684ace9511a9ee8a` |
| NVIDIA apphost/DLL/deps/runtimeconfig composite | `7517d6b514e8bf3664257a00b6135fcfbdac788d06aeab587b34f37e73741da3` |
| UI `index-DiooddbJ.js` | `6b0e1192a38708396f87e4eafec4a4d79295c9e4825c714538085920f2ae5cd4` |
| UI `index-BSRpW1oo.css` | `7078220d8b641383a950cc57250deef9b15d1274564feee17611f9ec675a3e08` |

The App Server lock/hash is unchanged from the exact value above. Final
source/manifest evidence uses the full pre-change capture
`.local/Lab/Runs/Finishing-20261003/data/captures/20261003T223148Z-b328c56fead94f2390e96553d6ce6ee4`.
The after-source receipt and complete path/hash comparison are stored in
`.local/Lab/Runs/Nvidia-20261004/after-source-receipt.json` and
`changed-files.json`. Generated builds, credentials, app state and QA outputs
are excluded from source snapshots and remain under ignored `.local`.
