export type Appearance = "dark" | "light";

export const APPEARANCE_STORAGE_KEY: "neobabylon.appearance:v1";
export function readAppearance(storage?: Pick<Storage, "getItem">): Appearance;
export function saveAppearance(appearance: Appearance, storage?: Pick<Storage, "setItem">): boolean;
export function applyAppearance(root: HTMLElement, appearance: Appearance): void;
