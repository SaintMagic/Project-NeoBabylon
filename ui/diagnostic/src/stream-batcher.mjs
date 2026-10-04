export function createAssistantDeltaBatcher({ schedule, cancel, onBatch, delayMs = 200 }) {
  const pending = new Map();
  let timer = null;

  function clearTimer() {
    if (timer === null) return;
    cancel(timer);
    timer = null;
  }

  function flush() {
    clearTimer();
    if (pending.size === 0) return;
    const batch = [...pending].map(([id, value]) => ({
      id,
      delta: value.chunks.join(""),
      ...(value.displayMetadata ? { displayMetadata: value.displayMetadata } : {}),
    }));
    pending.clear();
    onBatch(batch);
  }

  return {
    push(id, delta, displayMetadata) {
      if (!delta) return;
      const value = pending.get(id) ?? { chunks: [], displayMetadata: null };
      value.chunks.push(delta);
      if (displayMetadata) value.displayMetadata = displayMetadata;
      pending.set(id, value);
      if (timer === null) timer = schedule(flush, delayMs);
    },
    discard(id) {
      pending.delete(id);
      if (pending.size === 0) clearTimer();
    },
    flush,
    clear() {
      pending.clear();
      clearTimer();
    },
  };
}
