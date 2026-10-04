import { Selector } from "@astryxdesign/core/Selector";
import { chooseReasoningEffort, parseReasoningSelection, reasoningOptions, type ReasoningSelection } from "./reasoning-selection.mjs";

export function ReasoningSelector({ capability, selection, busy, readOnly = false, onChange }: {
  capability: unknown; selection: ReasoningSelection | null; busy: boolean; readOnly?: boolean; onChange: (selection: ReasoningSelection) => void;
}) {
  const policy = reasoningOptions(capability);
  const parsed = parseReasoningSelection(capability, selection);
  // Labels belong to the verified capability/wire mapping, not a hardcoded model-name heuristic.
  const options = [
    ...(policy.canOverride && policy.defaultEffort === null ? [{ value: "__provider-default__", label: "Provider default (Unknown)", disabled: true }] : []),
    ...policy.efforts.map((effort) => ({ value: effort, label: effort })),
  ];
  return <span className="reasoning-select-wrap" title={!policy.canOverride ? policy.reason : undefined}>
    <Selector label="Reasoning for next turn" isLabelHidden size="sm" variant="ghost" placement="above" options={options}
      value={policy.canOverride ? parsed.effort ?? "__provider-default__" : undefined}
      placeholder={policy.controlType === "enabled" ? "Reasoning: On/Off" : policy.known ? "Reasoning: provider default" : "Reasoning: Unknown"}
      renderValue={(option) => <span>Reasoning: {option.value === "__provider-default__" ? "provider default" : option.label}</span>}
      isDisabled={busy || readOnly || !policy.canOverride} disabledMessage={busy ? "Wait for the current operation before changing reasoning." : readOnly ? "This chat is read-only; verify its capability binding before changing reasoning." : policy.reason}
      hasClear={false} onChange={(effort) => { if (!busy && !readOnly) onChange(chooseReasoningEffort(capability, effort)); }} />
  </span>;
}
