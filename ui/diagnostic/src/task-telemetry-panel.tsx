import { memo, useEffect, useState } from "react";
import { Button } from "@astryxdesign/core/Button";
import { contextMetrics, rateHistory, type TaskTelemetry } from "./task-telemetry.mjs";

export type CompactFeedback = { status: "pending" | "completed" | "failed" | "unknown"; message: string };
const formatCount = (value: number | null | undefined) => typeof value === "number" ? new Intl.NumberFormat("en-US").format(value) : "Unknown";
const formatRate = (value: number | null) => value === null ? "Unknown" : `${value.toFixed(1)} tok/s`;

export const TaskTelemetryPanel = memo(function TaskTelemetryPanel({ telemetry, capability, compactAvailable, compacting, compactFeedback, onCompact }: {
  telemetry: TaskTelemetry; capability: unknown; compactAvailable: boolean; compacting: boolean;
  compactFeedback: CompactFeedback | null; onCompact: () => void;
}) {
  const [nowMs, setNowMs] = useState(() => performance.now());
  const latestSampleAt = telemetry.samples.at(-1)?.atMs;
  const expired = latestSampleAt === undefined || nowMs - latestSampleAt > 90_000;
  useEffect(() => {
    if (expired) return;
    const timer = window.setInterval(() => setNowMs(performance.now()), 1000);
    return () => window.clearInterval(timer);
  }, [latestSampleAt, expired]);
  const context = contextMetrics(telemetry, capability);
  const total = telemetry.usage?.total;
  const history = rateHistory(telemetry.samples, Math.max(nowMs, latestSampleAt ?? nowMs));
  const knownSamples = history.filter((sample) => sample.rate !== null);
  const scale = Math.max(1, ...knownSamples.map((sample) => sample.rate ?? 0));
  const historyNow = Math.max(nowMs, latestSampleAt ?? nowMs);

  return <section className="task-telemetry" aria-label="Context and live metrics">
    <section className="task-context" aria-labelledby="task-context-heading">
      <header className="telemetry-heading"><h3 id="task-context-heading">Context</h3><strong>{context.percent === null ? "Unknown" : `${context.percent.toFixed(1)}%`}</strong></header>
      {context.usedTokens !== null && context.windowTokens !== null
        ? <meter className="context-meter" aria-label="Reported context usage" min={0} max={context.windowTokens}
          value={Math.min(context.usedTokens, context.windowTokens)} aria-valuetext={`${formatCount(context.usedTokens)} of ${formatCount(context.windowTokens)} tokens (${context.percent?.toFixed(1)}%)`} />
        : <span className="context-meter context-meter-unknown" role="img" aria-label="Context usage Unknown" />}
      <p className="telemetry-context-counts"><span>{formatCount(context.usedTokens)} current tokens</span><span>{formatCount(context.windowTokens)} window</span></p>
      <p className="telemetry-source" title={context.windowEvidence ?? undefined}>{context.windowSource}</p>
      <p className="telemetry-source" title={context.usageSource ?? undefined}>{context.usageSource ? "Current tokens: Codex reported last.totalTokens" : "Current tokens: Unknown · awaiting fresh matching evidence"}</p>
      <Button label="Compact context" variant="secondary" size="sm" width="100%" isDisabled={!compactAvailable}
        isLoading={compacting} onClick={onCompact} tooltip={compactAvailable ? "Ask Codex to compact this chat; preserve its transcript and draft" : "Requires an idle, executable chat. Host verifies continuing commands."} />
      {compactFeedback && <p className={`compact-feedback compact-${compactFeedback.status}`} role={compactFeedback.status === "failed" || compactFeedback.status === "unknown" ? "alert" : "status"}>{compactFeedback.message}</p>}
    </section>
    <section className="live-metrics" aria-labelledby="live-metrics-heading">
      <header className="telemetry-heading"><h3 id="live-metrics-heading">Live metrics</h3></header>
      <dl className="telemetry-grid">
        <div><dt>Throughput</dt><dd>{formatRate(telemetry.turnAverage)}</dd><small>OBSERVED TURN AVERAGE</small></div>
        <div><dt>Average active</dt><dd>{formatRate(telemetry.weightedAverage)}</dd><small>{telemetry.measuredTurns} measured turns · time weighted</small></div>
        <div><dt>Tokens spent</dt><dd>{formatCount(total?.totalTokens)}</dd><small>{formatCount(total?.inputTokens)} input · {formatCount(total?.outputTokens)} output</small></div>
        <div><dt>Cache read</dt><dd>{formatCount(total?.cachedInputTokens)}</dd><small>{formatCount(total?.cacheWriteInputTokens)} written</small></div>
        <div className="telemetry-cost"><dt>Provider cost</dt><dd>Unknown</dd><small>Billing not reported; price is not actual cost</small></div>
      </dl>
      <p className="telemetry-source">Rates: Codex output-count delta ÷ measured turn interval, including waiting/tools. Current host selection only; restored turns are not timed.</p>
      <p className="telemetry-source" title={telemetry.usageSource ?? undefined}>Spend/cache: {telemetry.usageSource ? "Codex reported · thread-lifetime totals (all models)" : "Unknown · no attributable usage"}</p>
      <figure className="telemetry-history">
        <svg viewBox="0 0 300 66" role="img" aria-label="90-second history of observed completed turn averages; gaps indicate unavailable evidence">
          <path className="telemetry-gridline" d="M0 4H300M0 32H300M0 60H300" />
          {knownSamples.map((sample, index) => {
            const x = (sample.atMs - historyNow + 90_000) / 90_000 * 300;
            const y = 60 - (sample.rate ?? 0) / scale * 56;
            return <line className="telemetry-sample" key={sample.requestId ?? index} x1={Math.max(0, x - 2)} x2={Math.min(300, x + 2)} y1={y} y2={y}>
              <title>{formatRate(sample.rate)} · {sample.source ?? "Codex usage + measured turn interval"}</title>
            </line>;
          })}
          {knownSamples.length === 0 && <text x="150" y="36" textAnchor="middle">Unknown · no measured samples</text>}
        </svg>
        <figcaption>90 s · completed turn averages · gaps unavailable{knownSamples.length > 0 ? ` · auto scale 0–${scale.toFixed(1)} tok/s` : ""}</figcaption>
      </figure>
      {telemetry.observedAtUtc && <p className="telemetry-source">Journal observation: <time dateTime={telemetry.observedAtUtc}>{telemetry.observedAtUtc}</time></p>}
      {telemetry.warning && <p className="telemetry-warning" role="status">{telemetry.warning}</p>}
    </section>
  </section>;
});
