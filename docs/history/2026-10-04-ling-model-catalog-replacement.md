# Ling model catalog replacement — 2026-10-04

## Accepted scope

Martin requested: replace NEX with `inclusionai/ling-3.1-flash`.
This replaces a new-selection choice, not the identity of existing chats.
Keep the old NEX record byte-for-byte in place for historical evidence and
binding resolution; do not transfer NEX qualification to Ling or silently
retarget a selected/saved task. Other choices, including Space Bunny and
NVIDIA, remain unchanged. No runtime patch, credential/configuration change,
commit, push, installation or broad test campaign is included.

## Observed discovery

Public OpenRouter catalog and endpoint queries on 2026-10-04 returned:

- exact identifier `inclusionai/ling-3.1-flash`, canonical slug
  `inclusionai/ling-3.1-flash-20261002`;
- one available endpoint, Novita, tag `novita`, prompt/completion price `0`;
- text input/output, advertised context `262144`, max completion `32768`;
- `tools` / `tool_choice` support, with none/auto/function true and required
  false; generic function support is not a Codex tool qualification;
- reasoning/include_reasoning support, mandatory false and default-enabled
  true, but no exact effort levels/default effort or Codex mapping;
- quantization `unknown`; no response_format/schema enforcement advertised.

The authoritative normalized record is
`docs/release/MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH.json`. Effective context,
reasoning-effort mapping, apply_patch format, key-level entitlement and live
selected-tuple behavior remain unqualified. No GPT-family setting is invented.

Primary sources: [OpenRouter model catalog](https://openrouter.ai/api/v1/models),
[exact model endpoints](https://openrouter.ai/api/v1/models/inclusionai/ling-3.1-flash/endpoints),
[official model page](https://openrouter.ai/inclusionai/ling-3.1-flash).
Raw discovery and unchanged-evidence hashes are retained under
`.local/Lab/Runs/Ling-20261004/discovery-and-baseline.json`.

## Verification scope

The bounded implementation retires NEX from new picker/selection operations
while leaving historical binding/resume resolution unchanged; the OpenRouter
qualification runner's default target becomes Ling. Focused checks cover that
catalog contract and historical NEX identity preservation. No inference is
performed by discovery, compilation or catalog inspection, and no live tool
success is claimed for Ling.

The worker's actual checks and the controller's integration review are retained
in `.local/Lab/Runs/Ling-20261004/worker-report.md`; final build/native findings
are recorded below when performed.

## Added user request: center the composer

Martin subsequently asked for the composer to be centered. The bounded fix
adds `margin-inline: auto` to `ui/diagnostic/src/app.css`'s `.composer-area`:
its new full-width fieldset wrapper is not a flex container, so the previous
`align-self: center` declaration no longer positioned it. The composer and
keyboard-hint group now center within the conversation pane, excluding the
sidebar and any visible details panel. Existing responsive widths, colors,
draft/history/state behavior and internal control alignment are unchanged.

## Actual verification

- Delegated Host and qualification-runner Release builds passed with zero
  errors/warnings. The focused `--probe-ling-active-selection` check passed
  after its expected red failure, including exact-tuple retirement, unrelated
  choices and preserved historical NEX binding/bytes.
- Controller UI test run: 131 passed, zero failures. Typecheck and production
  UI build passed. Existing Astryx module-directive and large-chunk build
  warnings remain; no dependency changes were made.
- Isolated WPF/WebView2 fixture loaded `https://neobabylon.local/index.html`
  with the updated source-built Host and production UI. Actual native picker
  contained Ling and all six unrelated selectable records, but not NEX.
  Selecting Ling succeeded; completion options displayed the sourced 32,768
  maximum. An explicit retired-NEX selection was rejected without changing
  the active Ling selection.
- Composer center error was zero pixels in all four measured states:
  1464px and 820px viewport widths, with details visible and hidden. No
  horizontal document overflow, framework overlay, page errors or console
  errors/warnings were observed. Screenshots were visually inspected.
- The initial credential-free selection attempt was rejected explicitly and
  retained Space Bunny; it did not fall back. The successful fixture used the
  existing app-private current-user-protected OpenRouter credential, read
  without printing or copying it into sources/reports. No turn or inference
  request was made. Real provider/route/tool behavior remains unqualified.

Native script, result and screenshots are retained under
`.local/Lab/Runs/Ling-20261004/`. These ignored local artifacts remain inside
the NeoBabylon folder per Martin's workspace-layout decision. Browser plugin
was not available; rendered checks used Playwright through WebView2 CDP.
