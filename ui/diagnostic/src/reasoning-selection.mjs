// Exact strings from the host's pinned Codex effort enum. No provider-native numeric mapping.
const pinnedEfforts = new Set(["none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra", "persistent"]);
const field = (value, name) => value && typeof value === "object" && !Array.isArray(value)
  ? (Object.hasOwn(value, name) ? value[name] : value[`${name[0].toUpperCase()}${name.slice(1)}`]) : undefined;

export function reasoningOptions(capability) {
  const observation = field(capability, "reasoningControls");
  const controls = field(observation, "value");
  const efforts = field(controls, "supported_efforts");
  const defaultEffort = field(controls, "default_effort") ?? null;
  const enabledControl = field(observation, "state") === "Known" && field(controls, "control_type") === "enabled"
    && typeof field(controls, "mandatory") === "boolean" && typeof field(controls, "default_enabled") === "boolean"
    && efforts === undefined && field(controls, "default_effort") === undefined;
  const valid = field(observation, "state") === "Known" && field(controls, "control_type") !== "enabled" && Array.isArray(efforts) && efforts.length > 0
    && efforts.every((effort) => typeof effort === "string" && pinnedEfforts.has(effort))
    && new Set(efforts).size === efforts.length
    && (defaultEffort === null || efforts.includes(defaultEffort));
  return {
    key: JSON.stringify([field(capability, "providerId"), field(capability, "modelIdentifier"), observation]),
    known: valid || enabledControl,
    controlType: enabledControl ? "enabled" : valid ? "effort" : null,
    canOverride: valid,
    defaultEnabled: enabledControl ? field(controls, "default_enabled") : null,
    efforts: valid ? [...efforts] : [],
    defaultEffort: valid ? defaultEffort : null,
    evidenceSource: typeof field(observation, "evidenceSource") === "string" ? field(observation, "evidenceSource") : null,
    reason: enabledControl ? "Provider supports On/Off; pinned Codex runtime has no Boolean reasoning override" : valid ? (defaultEffort === null
      ? "Provider default is Unknown. Selecting an effort requests that exact level; clearing a sticky turn effort is not offered."
      : "Advertised capability levels; requested effort is separate from runtime-observed reasoning.")
      : "Reasoning: Unknown. No verified supported Codex string levels are available; provider default is used without an override.",
  };
}

export function initialReasoningSelection(capability) {
  const options = reasoningOptions(capability);
  return { key: options.key, effort: options.defaultEffort };
}

export function chooseReasoningEffort(capability, effort) {
  const options = reasoningOptions(capability);
  if (!options.canOverride || !options.efforts.includes(effort)) throw new Error("The requested reasoning effort is not supported by this exact capability record.");
  return { key: options.key, effort };
}

export function parseReasoningSelection(capability, selection) {
  const options = reasoningOptions(capability);
  const effort = selection?.key === options.key ? selection.effort : options.defaultEffort;
  if (!options.canOverride) return { effort: null, error: null, wire: {} };
  if (effort === null || effort === undefined) return { effort: null, error: null, wire: {} };
  if (!options.efforts.includes(effort)) return { effort: null, error: "The requested reasoning effort is not supported by this exact capability record.", wire: {} };
  return { effort, error: null, wire: { reasoningEffort: effort } };
}
