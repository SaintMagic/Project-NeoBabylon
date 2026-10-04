import { useCallback, useEffect, useRef, useState } from "react";
import { requestHost, type HostNotification } from "./bridge";
import { createTaskTelemetry, createTelemetryFramePublisher, reduceTaskTelemetry, telemetryIdentityKey, type TaskTelemetry, type TelemetryAction, type TelemetryIdentity } from "./task-telemetry.mjs";

const read = (record: Record<string, unknown>, name: string) => Object.hasOwn(record, name) ? record[name] : record[`${name[0].toUpperCase()}${name.slice(1)}`];

export function useTaskTelemetry(identity: TelemetryIdentity) {
  const identityKey = telemetryIdentityKey(identity);
  const store = useRef<TaskTelemetry>(createTaskTelemetry(identity));
  const [published, setPublished] = useState(store.current);
  const requestGeneration = useRef(0);
  const mounted = useRef(true);
  const publisher = useRef<ReturnType<typeof createTelemetryFramePublisher<TaskTelemetry>> | null>(null);
  if (publisher.current === null) publisher.current = createTelemetryFramePublisher<TaskTelemetry>({
    schedule: requestAnimationFrame, cancel: cancelAnimationFrame, onValue: (value) => { if (mounted.current) setPublished(value); },
  });

  const update = useCallback((action: TelemetryAction) => {
    const next = reduceTaskTelemetry(store.current, action);
    if (next !== store.current) { store.current = next; publisher.current?.push(next); }
  }, []);

  const reset = useCallback((nextIdentity: TelemetryIdentity) => {
    requestGeneration.current++;
    publisher.current?.clear();
    const previous = store.current;
    const sameThread = nextIdentity.workspace === previous.identity.workspace && nextIdentity.threadId !== null && nextIdentity.threadId === previous.identity.threadId;
    const sameThreadNewModel = sameThread && (nextIdentity.providerId !== previous.identity.providerId || nextIdentity.modelIdentifier !== previous.identity.modelIdentifier);
    const preserveFence = sameThread && (sameThreadNewModel || previous.fence !== null);
    store.current = createTaskTelemetry(nextIdentity, { epoch: previous.epoch + 1,
      fenceFrom: preserveFence ? previous : null, observedAfterUtcMs: sameThreadNewModel ? Date.now() : previous.fence?.observedAfterUtcMs ?? null });
    setPublished(store.current);
  }, []);

  const refresh = useCallback(async (finalForRequestId?: string) => {
    const captured = store.current;
    if (!captured.identity.threadId) return;
    const generation = ++requestGeneration.current;
    const stillCurrent = () => mounted.current && generation === requestGeneration.current
      && captured.identityKey === store.current.identityKey && captured.epoch === store.current.epoch
      && captured.revision === store.current.revision;
    try {
      const result = await requestHost("getThreadUsage", { threadId: captured.identity.threadId });
      if (!stillCurrent()) return;
      if (read(result, "attributedTo") !== "NeoBabylon.Host" || read(result, "threadId") !== captured.identity.threadId) {
        throw new Error("NeoBabylon.UI: The host did not confirm usage for this exact chat.");
      }
      update({ type: "snapshot", epoch: captured.epoch, revision: captured.revision, result, finalForRequestId });
    } catch (error) {
      if (stillCurrent()) update({ type: "warning", message: `Usage refresh unavailable: ${error instanceof Error ? error.message : "unknown host failure"}. Missing metrics remain Unknown.` });
    }
  }, [update]);

  const begin = useCallback((nextIdentity: TelemetryIdentity, requestId: string) => {
    if (store.current.identityKey !== telemetryIdentityKey(nextIdentity)) reset(nextIdentity);
    requestGeneration.current++;
    update({ type: "begin", requestId, nowMs: performance.now() });
  }, [reset, update]);
  const notification = useCallback((event: HostNotification) => update({ type: "notification", event }), [update]);
  const complete = useCallback((requestId: string, result: Record<string, unknown>, outcome: string) => {
    const threadId = read(result, "threadId");
    const turnId = read(result, "turnId");
    update({ type: "complete", requestId, threadId: typeof threadId === "string" ? threadId : null,
      turnId: typeof turnId === "string" ? turnId : null, outcome, nowMs: performance.now() });
    // A malformed final identity cannot leave an old measured turn accepting later events.
    if (store.current.activeTurn?.requestId === requestId) update({ type: "abandon", requestId, nowMs: performance.now() });
    void refresh(requestId);
  }, [refresh, update]);
  const abandon = useCallback((requestId: string) => {
    update({ type: "abandon", requestId, nowMs: performance.now() });
    void refresh();
  }, [refresh, update]);
  const invalidateContext = useCallback(() => {
    requestGeneration.current++;
    update({ type: "invalidateContext", observedAfterUtcMs: Date.now() });
  }, [update]);
  const beginCompaction = useCallback((requestId: string) => {
    requestGeneration.current++;
    update({ type: "beginCompaction", requestId });
  }, [update]);
  const finishCompaction = useCallback((requestId: string, confirmed: boolean, turnId: string | null) => {
    requestGeneration.current++;
    update({ type: "finishCompaction", requestId, confirmed, turnId, observedAfterUtcMs: Date.now() });
  }, [update]);
  const identityToken = useCallback(() => ({ key: store.current.identityKey, epoch: store.current.epoch }), []);
  const matchesIdentity = useCallback((key: string, epoch: number) => store.current.identityKey === key && store.current.epoch === epoch, []);

  useEffect(() => {
    if (store.current.identityKey !== identityKey) reset(identity);
    if (identity.threadId) void refresh();
    // The primitive key includes workspace/thread/provider/model; draft and transcript updates do not refetch usage.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [identityKey, reset, refresh]);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; requestGeneration.current++; publisher.current?.clear(); };
  }, []);

  return { snapshot: published.identityKey === identityKey && published.epoch === store.current.epoch ? published : createTaskTelemetry(identity),
    reset, begin, notification, complete, abandon, refresh, invalidateContext, beginCompaction, finishCompaction, identityKey, identityToken, matchesIdentity };
}
