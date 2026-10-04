export type ToolCapabilityCatalogRow = Record<string, unknown>;

export type SelectedToolQualification = {
  operationId: string;
  state: string;
  observedOn?: string;
  evidenceSource?: string;
  scope?: string;
  note?: string;
};

export function selectedToolQualifications(
  capability: Record<string, unknown> | null,
): SelectedToolQualification[];

export function buildCapabilityCatalogView(
  catalog: Record<string, unknown>,
  capability: Record<string, unknown> | null,
): ToolCapabilityCatalogRow[];

export function filterCapabilityCatalog(
  categories: ToolCapabilityCatalogRow[],
  query: string,
): ToolCapabilityCatalogRow[];
