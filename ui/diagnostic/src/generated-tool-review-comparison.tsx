import { useState } from "react";
import type { HostOperation } from "./bridge";
import { parseGeneratedToolReviewComparison, type GeneratedToolReviewComparison } from "./generated-tool-review.mjs";

type ComparisonOperation = Extract<HostOperation, "readGeneratedToolReviewComparison">;
type HostRequest = (operation: ComparisonOperation, payload?: Record<string, unknown>) => Promise<Record<string, unknown>>;

function displayData(value: unknown): string {
  try {
    const rendered = typeof value === "string" ? value : JSON.stringify(value, null, 2);
    return typeof rendered === "string" ? rendered.slice(0, 32_768) : "No projection supplied.";
  } catch {
    return "Projection could not be displayed.";
  }
}

function ChangePaths({ title, files }: { title: string; files: GeneratedToolReviewComparison["addedFiles"] }) {
  return <section className="candidate-comparison-group"><h5>{title}</h5>
    {files.length ? <ul>{files.map((file) => <li key={file.path}><code>{file.path}</code>
      <small>Reviewed SHA-256: {file.previousSha256 || "Not present"} · current SHA-256: {file.currentSha256 ?? "Not present"}</small>
    </li>)}</ul> : <p>None reported.</p>}
  </section>;
}

function JsonProjection({ title, value }: { title: string; value: unknown }) {
  return <section className="candidate-comparison-group"><h5>{title}</h5>
    {Array.isArray(value) && value.length === 0 ? <p>None recorded.</p>
      : value === null || value === undefined ? <p>Unavailable.</p>
        : <pre className="candidate-activation-json" tabIndex={0}>{displayData(value)}</pre>}
  </section>;
}

function ComparisonResult({ comparison }: { comparison: GeneratedToolReviewComparison }) {
  if (comparison.comparisonState === "noPriorReview") {
    return <div className="candidate-comparison-result" role="status">
      <p className="candidate-activation-reason">No earlier Review or Reject decision exists for comparison. No prior bytes or diff are available.</p>
      <p className="candidate-confirm-identity">Current identity: {comparison.currentContentIdentity}<br />Attribution: {comparison.attributedTo}</p>
    </div>;
  }
  if (comparison.comparisonState === "unavailable") {
    return <div className="candidate-comparison-result" role="status">
      <p className="candidate-activation-reason">Historical review snapshot unavailable. {comparison.unavailableReason ?? "Prior bytes were not retained."} No prior source, evidence, or diff is reconstructed.</p>
      <p className="candidate-confirm-identity">Current identity: {comparison.currentContentIdentity}<br />Reviewed identity: {comparison.reviewedContentIdentity ?? "Unavailable"}<br />Prior review identity: {comparison.priorReviewIdentity ?? "Unavailable"}<br />Attribution: {comparison.attributedTo}</p>
    </div>;
  }
  return <div className="candidate-comparison-result">
    <p className="candidate-confirm-identity">Prior decision: {comparison.priorDecision ?? "Unavailable"}<br />Reviewed identity: {comparison.reviewedContentIdentity ?? "Unavailable"}<br />Current identity: {comparison.currentContentIdentity}<br />Prior review identity: {comparison.priorReviewIdentity ?? "Unavailable"}<br />Attribution: {comparison.attributedTo}</p>
    <ChangePaths title="Added files" files={comparison.addedFiles} />
    <ChangePaths title="Removed files" files={comparison.removedFiles} />
    <ChangePaths title="Modified files" files={comparison.modifiedFiles} />
    {!comparison.addedFiles.length && !comparison.removedFiles.length && !comparison.modifiedFiles.length
      && <p role="status">The host reported no added, removed, or modified paths.</p>}
    {[...comparison.addedFiles, ...comparison.removedFiles, ...comparison.modifiedFiles]
      .filter((change) => change.previewAvailable || change.beforePreview !== null || change.afterPreview !== null)
      .map((preview) => <section className="candidate-comparison-preview" key={preview.path}>
      <h5>{preview.path}</h5>
      <div className="candidate-comparison-preview-columns">
        <div><strong>Reviewed bytes{preview.beforePreviewTruncated ? " · preview truncated" : ""}</strong>
          {preview.previousSha256 === "" ? <p>Not present in the reviewed snapshot.</p>
            : preview.beforePreview === null ? <p>No reviewed UTF-8 preview supplied.</p> : <pre tabIndex={0}>{preview.beforePreview}</pre>}</div>
        <div><strong>Current bytes{preview.afterPreviewTruncated ? " · preview truncated" : ""}</strong>
          {preview.currentSha256 === null ? <p>Not present in the current candidate.</p>
            : preview.afterPreview === null ? <p>No current UTF-8 preview supplied.</p> : <pre tabIndex={0}>{preview.afterPreview}</pre>}</div>
      </div>
    </section>)}
    <JsonProjection title="Changed manifest fields" value={comparison.changedManifestFields} />
    <JsonProjection title="Reviewed evidence projection" value={comparison.reviewedEvidence} />
    <JsonProjection title="Current evidence projection" value={comparison.currentEvidence} />
  </div>;
}

export function GeneratedToolReviewComparison({ toolId, contentIdentity, requestHost }: {
  toolId: string;
  contentIdentity: string;
  requestHost: HostRequest;
}) {
  const [comparison, setComparison] = useState<GeneratedToolReviewComparison | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadComparison() {
    setLoading(true);
    setError(null);
    setComparison(null);
    try {
      const response = await requestHost("readGeneratedToolReviewComparison", { toolId, contentIdentity });
      setComparison(parseGeneratedToolReviewComparison(response, toolId, contentIdentity));
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Review comparison could not be loaded.");
    } finally {
      setLoading(false);
    }
  }

  return <section className="candidate-subsection candidate-comparison" aria-label="Review comparison">
    <h4>Review comparison · host snapshot only</h4>
    <p>Shows file categories, bounded UTF-8 previews, manifest changes, and evidence projections returned by NeoBabylon.Host. Candidate content is displayed as inert text.</p>
    <button type="button" className="candidate-inline-action" disabled={loading} onClick={() => void loadComparison()}>{loading ? "Loading comparison…" : comparison ? "Reload comparison" : "Compare current content with prior review"}</button>
    {error && <p className="candidate-error" role="alert">{error}</p>}
    {comparison && <ComparisonResult comparison={comparison} />}
  </section>;
}
