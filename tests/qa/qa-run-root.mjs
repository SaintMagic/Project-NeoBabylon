import { existsSync, lstatSync, mkdirSync, realpathSync } from "node:fs";
import path from "node:path";

const RUN_ID = /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,78}[A-Za-z0-9])?$/;

function equalWindowsPath(left, right) {
  return path.resolve(left).replace(/[\\/]+$/, "").toLowerCase()
    === path.resolve(right).replace(/[\\/]+$/, "").toLowerCase();
}

function assertExistingDirectories(sourceRoot) {
  const source = path.resolve(sourceRoot);
  for (const component of [
    source,
    path.join(source, ".local"),
    path.join(source, ".local", "Lab"),
    path.join(source, ".local", "Lab", "Runs"),
  ]) {
    const entry = lstatSync(component);
    if (!entry.isDirectory() || entry.isSymbolicLink()
      || !equalWindowsPath(realpathSync.native(component), component)) {
      throw new Error(`QA runs path component is not a direct directory: ${component}`);
    }
  }
}

export function assertQaRunsParent(sourceRoot, candidate) {
  const expected = path.resolve(sourceRoot, ".local", "Lab", "Runs");
  const actual = path.resolve(candidate);
  if (!equalWindowsPath(actual, expected)) {
    throw new Error(`QA runs parent must be exactly ${expected}; received ${actual}`);
  }
  assertExistingDirectories(sourceRoot);
  return expected;
}

export function newQaRun(qaRunsParent, runId) {
  if (typeof runId !== "string" || !RUN_ID.test(runId)) {
    throw new Error("QA run ID must be 1-80 ASCII letters, digits, or hyphens, beginning and ending with an alphanumeric");
  }
  const parent = path.resolve(qaRunsParent);
  const sourceRoot = path.resolve(parent, "..", "..", "..");
  assertQaRunsParent(sourceRoot, parent);
  const runRoot = path.resolve(parent, runId);
  if (path.dirname(runRoot).toLowerCase() !== parent.toLowerCase()) throw new Error("QA run escaped its parent");
  const applicationRoot = path.join(runRoot, "App");
  if (existsSync(runRoot) || existsSync(applicationRoot)) throw new Error(`refusing to reuse existing QA run or application root: ${runRoot}`);
  mkdirSync(runRoot, { recursive: false });
  return { runRoot, applicationRoot };
}
