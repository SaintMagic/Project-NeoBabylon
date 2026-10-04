export function handleDialogKeyDown(
  event: { key: string; shiftKey: boolean; target: EventTarget | null; preventDefault(): void },
  dialog: HTMLElement | null,
  onClose: () => void,
): void;
