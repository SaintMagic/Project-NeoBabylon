const QUALIFICATION_STATES = new Set([
  "qualified",
  "advertised",
  "known",
  "unsupported",
  "blocked",
  "unknown",
]);

const asRecord = (value) => value && typeof value === "object" && !Array.isArray(value)
  ? value
  : null;

function unknownQualification(evidenceSource = "") {
  return { state: "unknown", evidenceSource };
}

function resolveQualification(specification, capability) {
  const spec = asRecord(specification);
  if (!spec) return unknownQualification();

  if (spec.kind === "not-applicable") {
    return {
      state: "not-applicable",
      evidenceSource: typeof spec.evidenceSource === "string" ? spec.evidenceSource : "",
    };
  }

  if (spec.kind === "unknown") {
    return unknownQualification(typeof spec.evidenceSource === "string" ? spec.evidenceSource : "");
  }

  const selected = asRecord(capability);
  if (!selected) return unknownQualification();

  const operationId = typeof spec.operationId === "string" ? spec.operationId : null;
  if (operationId) {
    const matches = Array.isArray(selected.toolQualifications)
      ? selected.toolQualifications.filter((entry) => asRecord(entry)?.operationId === operationId)
      : [];
    if (matches.length === 1) {
      const evidence = asRecord(matches[0]);
      const state = typeof evidence.state === "string" ? evidence.state.toLowerCase() : "unknown";
      return {
        state: QUALIFICATION_STATES.has(state) ? state : "unknown",
        observedOn: typeof evidence.observedOn === "string" ? evidence.observedOn : "",
        evidenceSource: typeof evidence.evidenceSource === "string" ? evidence.evidenceSource : "",
        scope: typeof evidence.scope === "string" ? evidence.scope : "",
      };
    }
    if (matches.length > 1) return unknownQualification("Multiple conflicting records exist for this operation.");
  }

  const capabilityField = typeof spec.capabilityField === "string" ? spec.capabilityField : null;
  const observation = capabilityField ? asRecord(selected[capabilityField]) : null;
  if (!observation) return unknownQualification();

  const recordState = typeof observation.state === "string" ? observation.state.toLowerCase() : "unknown";
  const source = typeof observation.evidenceSource === "string" ? observation.evidenceSource : "";
  if (recordState === "known" && observation.value !== null && observation.value !== undefined) {
    return {
      state: typeof spec.whenKnown === "string" && QUALIFICATION_STATES.has(spec.whenKnown)
        ? spec.whenKnown
        : "advertised",
      evidenceSource: source,
      note: typeof observation.note === "string" ? observation.note : "",
    };
  }
  if (recordState === "unsupported" || recordState === "blocked") {
    return { state: recordState, evidenceSource: source };
  }
  return unknownQualification(source);
}

/** Recorded operation evidence only; advertised tool support never supplies qualification. */
export function selectedToolQualifications(capability) {
  const entries = asRecord(capability)?.toolQualifications;
  if (!Array.isArray(entries)) return [];
  const operationIds = entries.map((entry) => asRecord(entry)?.operationId)
    .filter((operationId) => typeof operationId === "string" && operationId.trim());
  return [...new Set(operationIds)].map((operationId) => ({
    operationId,
    ...resolveQualification({ operationId }, capability),
  }));
}

/** Build read-only UI rows from the versioned catalog and exactly one selected model record. */
export function buildCapabilityCatalogView(catalog, capability) {
  const source = asRecord(catalog);
  if (source?.schemaVersion !== 1 || !Array.isArray(source.categories)) return [];

  return source.categories.map((categoryValue) => {
    const category = asRecord(categoryValue) ?? {};
    const items = Array.isArray(category.items) ? category.items : [];
    return {
      ...category,
      items: items.map((itemValue) => {
        const item = asRecord(itemValue) ?? {};
        return {
          ...item,
          qualification: resolveQualification(item.qualification, capability),
        };
      }),
    };
  });
}

/** Filter category and item evidence without discarding Unknown rows on an empty query. */
export function filterCapabilityCatalog(categories, query) {
  const normalizedQuery = typeof query === "string" ? query.trim().toLocaleLowerCase() : "";
  if (!normalizedQuery) return Array.isArray(categories) ? categories : [];
  if (!Array.isArray(categories)) return [];

  return categories.flatMap((category) => {
    const categoryText = [category.id, category.title]
      .filter((value) => typeof value === "string")
      .join(" ")
      .toLocaleLowerCase();
    const categoryMatches = categoryText.includes(normalizedQuery);
    const items = Array.isArray(category.items) ? category.items : [];
    const matchingItems = categoryMatches ? items : items.filter((item) => {
      const exposure = asRecord(item.exposure);
      const qualification = asRecord(item.qualification);
      const searchable = [
        item.id,
        item.name,
        item.description,
        exposure?.label,
        exposure?.permissionSource,
        exposure?.evidenceSource,
        qualification?.state,
        qualification?.evidenceSource,
        qualification?.scope,
      ].filter((value) => typeof value === "string").join(" ").toLocaleLowerCase();
      return searchable.includes(normalizedQuery);
    });

    return matchingItems.length ? [{ ...category, items: matchingItems }] : [];
  });
}
