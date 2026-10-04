export function nextListboxIndex(key, currentIndex, count) {
  if (!Number.isSafeInteger(count) || count <= 0) return -1;
  const current = Number.isInteger(currentIndex) && currentIndex >= 0 && currentIndex < count
    ? currentIndex
    : 0;
  if (key === "ArrowDown") return (current + 1) % count;
  if (key === "ArrowUp") return (current + count - 1) % count;
  if (key === "Home") return 0;
  if (key === "End") return count - 1;
  return null;
}
