# Validation addendum — F20: contrast gate remains open

Companion to the [review report](../CODEX_PRODUCT_PARITY_REVIEW.md). Discovered after completing the browser interaction fixture on 2026-10-04. Severity: Moderate. Evidence: measured synthetic Windows browser output on the exact base plus eight proposed patches; not a native accessibility certification.

Run 37205696436 reports 43 active below-threshold readings across nine states, with 15 distinct text/class combinations. Repetition across audits is not a count of distinct product bugs. Representative active readings are 3.81:1 for light telemetry labels on white, 3.33:1 for the enabled compaction label, and 1.99:1 dark / 3.15:1 light for the reasoning field label. Nine disabled-control exceptions and zero indeterminate readings are separately recorded. See [VERIFICATION.md](VERIFICATION.md) for exact evidence identity and scope.

## Bounded follow-up implementation

Inspect `ui/diagnostic/src/app.css` theme variables, `.telemetry-source`, `.telemetry-grid`, telemetry history captions, `.reasoning-select-wrap`, and the existing Astryx `Select`/`Button` integration in `App.tsx` and telemetry components. The complete existing audit is in `tests/qa/ui-accessibility.mjs` and its imported contrast helper. Do not replace Astryx or override every label/button globally.

First reproduce a baseline using only the corrected fixture contracts (007) against the original product source. Compare the same computed styles and audit states with the eight-patch series. Check whether each reasoning field label is actually visible or visually hidden and whether the audit classifies that correctly. Keep genuine visible-label failures separate from any proven audit visibility defect. Preserve the current minimums and disabled-control handling; a lower threshold is not a fix.

For real visible failures, use scoped semantic text/theme tokens with sufficient contrast on the measured background, preserving dark/light appearance and active/disabled distinctions. Do not mark an active control disabled just to exempt it. Re-run all nine states, interactions, CSS/TS build, new width/copy checks and native DPI/keyboard checks. Include assertions for enabled and disabled compaction separately and for unknown reasoning labels.

Acceptance: the existing contrast gate has zero active failures and zero unknown results in the supported states, interaction tests still pass, the original/new series comparison is retained, and native checks are labeled separately. This is a narrow extension of S004's presentation work; it does not block constructing S001 settings storage or S006 replay persistence. It does block claiming the whole UI is accessibility-qualified.

No patch was fabricated for this finding without reproducing the theme/audit distinction. No new product decision is required.
