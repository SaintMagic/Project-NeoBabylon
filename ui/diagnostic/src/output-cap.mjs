const INT32_MAX = 2_147_483_647;
const UNKNOWN_DEFAULT = 32_768;

export function completionLimit(observation) {
  if (observation?.state !== "Known") {
    return { state: "Unknown", defaultTokens: UNKNOWN_DEFAULT, maximumTokens: null };
  }
  const maximum = observation.value;
  if (!Number.isInteger(maximum) || maximum <= 0 || maximum > INT32_MAX) {
    return { state: "Invalid", defaultTokens: null, maximumTokens: null };
  }
  return { state: "Known", defaultTokens: maximum, maximumTokens: maximum };
}

export function parseOutputOverride(input, limit) {
  if (limit.state === "Invalid") {
    return { value: null, error: "The selected model's advertised completion maximum is invalid; the output cap cannot be confirmed." };
  }
  if (input === "") return { value: null, error: null };
  if (!/^[1-9][0-9]*$/.test(input)) {
    return { value: null, error: "Enter a positive whole number without spaces or leading zeros, or clear the field to use the default." };
  }
  const value = Number(input);
  if (!Number.isInteger(value) || value > INT32_MAX) {
    return { value: null, error: "The output cap must be at most 2,147,483,647 tokens." };
  }
  if (limit.maximumTokens !== null && value > limit.maximumTokens) {
    return { value: null, error: "The output cap exceeds this model's advertised maximum completion tokens." };
  }
  return { value, error: null };
}
