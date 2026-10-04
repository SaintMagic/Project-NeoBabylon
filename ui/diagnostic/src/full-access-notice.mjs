export const fullAccessNoticeStorageKey = "neobabylon.full-access-notice-hidden:v1";

export function isFullAccessNoticeHidden(storage) {
  try {
    return (storage ?? globalThis.localStorage).getItem(fullAccessNoticeStorageKey) === "1";
  } catch {
    return false;
  }
}

export function hideFullAccessNotice(storage) {
  try {
    (storage ?? globalThis.localStorage).setItem(fullAccessNoticeStorageKey, "1");
    return true;
  } catch {
    return false;
  }
}
