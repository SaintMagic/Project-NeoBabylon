import assert from "node:assert/strict";
import { copyFileSync, mkdtempSync, mkdirSync, rmSync, existsSync, readdirSync, readFileSync, symlinkSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

test("accepts only the exact source-root Lab Runs directory", () => {
  const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-root-"));
  try {
    const source = path.join(temporary, "NeoBabylon");
    const parent = path.join(source, ".local", "Lab", "Runs");
    mkdirSync(parent, { recursive: true });
    assert.equal(assertQaRunsParent(source, parent), parent);
    for (const candidate of [path.join(temporary, "NeoBabylon-Data", "QA"), path.join(source, ".local", "Lab", "Runs-extra"), source, path.join(parent, "child")]) {
      assert.throws(() => assertQaRunsParent(source, candidate));
    }
  } finally { rmSync(temporary, { recursive: true, force: true }); }
});

test("creates one validated fresh run and its App root", () => {
  const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-create-"));
  try {
    const source = path.join(temporary, "NeoBabylon");
    const parent = path.join(source, ".local", "Lab", "Runs");
    mkdirSync(parent, { recursive: true });
    const { runRoot, applicationRoot } = newQaRun(parent, "qa-01");
    assert.equal(runRoot, path.join(parent, "qa-01"));
    assert.equal(applicationRoot, path.join(runRoot, "App"));
    for (const invalid of ["../escape", "qa_01", "-bad", "bad-"]) assert.throws(() => newQaRun(parent, invalid));
    assert.throws(() => newQaRun(parent, "qa-01"));
    const existing = path.join(parent, "qa-02", "App");
    mkdirSync(existing, { recursive: true });
    assert.throws(() => newQaRun(parent, "qa-02"));
    assert.equal(existsSync(path.join(source, "escape")), false);
  } finally { rmSync(temporary, { recursive: true, force: true }); }
});

test("rejects a missing Runs parent without creating directories", () => {
  const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-missing-"));
  try {
    const source = path.join(temporary, "NeoBabylon");
    const lab = path.join(source, ".local", "Lab");
    const parent = path.join(lab, "Runs");
    mkdirSync(lab, { recursive: true });
    const before = readdirSync(lab);
    assert.throws(() => assertQaRunsParent(source, parent));
    assert.throws(() => newQaRun(parent, "qa-absent"));
    assert.deepEqual(readdirSync(lab), before);
    assert.equal(existsSync(parent), false);
  } finally { rmSync(temporary, { recursive: true, force: true }); }
});

test("redirected .local, Lab, or Runs cannot create an outside run", () => {
  for (const component of [".local", "Lab", "Runs"]) {
    const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-link-"));
    try {
      const source = path.join(temporary, "NeoBabylon");
      const outside = path.join(temporary, "outside");
      const parent = path.join(source, ".local", "Lab", "Runs");
      const link = component === ".local" ? path.join(source, ".local")
        : component === "Lab" ? path.join(source, ".local", "Lab") : parent;
      const outsideParent = component === ".local" ? path.join(outside, "Lab", "Runs")
        : component === "Lab" ? path.join(outside, "Runs") : outside;
      mkdirSync(path.dirname(link), { recursive: true });
      mkdirSync(outsideParent, { recursive: true });
      symlinkSync(outside, link, process.platform === "win32" ? "junction" : "dir");
      const outsideCanary = path.join(outsideParent, "qa-escape");
      assert.throws(() => newQaRun(parent, "qa-escape"), `${component} redirect was accepted`);
      assert.equal(existsSync(outsideCanary), false, `${component} redirect created an outside run`);
      assert.throws(() => assertQaRunsParent(source, parent));
    } finally { rmSync(temporary, { recursive: true, force: true }); }
  }
});

test("invalid or reused IDs leave the parent and existing run unchanged", () => {
  const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-unchanged-"));
  try {
    const source = path.join(temporary, "NeoBabylon");
    const parent = path.join(source, ".local", "Lab", "Runs");
    const existing = path.join(parent, "qa-used");
    mkdirSync(path.join(existing, "App"), { recursive: true });
    const canary = path.join(existing, "canary.txt");
    writeFileSync(canary, "preserve", "utf8");
    const before = readdirSync(parent);
    for (const runId of ["../escape", "qa_01", "-bad", "bad-", "", "a".repeat(81), "qa-used"]) {
      assert.throws(() => newQaRun(parent, runId));
      assert.deepEqual(readdirSync(parent), before);
      assert.equal(readFileSync(canary, "utf8"), "preserve");
    }
    assert.equal(existsSync(path.join(source, ".local", "Lab", "escape")), false);
  } finally { rmSync(temporary, { recursive: true, force: true }); }
});

const nativeRunners = [
  "native-approval-qualification.mjs",
  "native-command-failure-rendering.mjs",
  "native-command-stop.mjs",
  "native-cross-project-recovery.mjs",
  "native-draft-storage-failure.mjs",
  "native-history-pagination.mjs",
  "native-large-output.mjs",
  "native-live-nex-output.mjs",
  "native-live-patch-review.mjs",
  "native-model-capability-rebind.mjs",
  "native-pending-send-recovery.mjs",
  "native-tool-capability-catalog.mjs",
  "native-turn-interruption.mjs",
  "native-visual-acceptance.mjs",
];

test("native runner preflights accept exact Lab Runs and reject the old sibling", () => {
  const qaSource = path.dirname(fileURLToPath(import.meta.url));
  for (const name of nativeRunners) {
    const temporary = mkdtempSync(path.join(os.tmpdir(), "nb-qa-runner-"));
    try {
      const source = path.join(temporary, "NeoBabylon");
      const qaDir = path.join(source, "tests", "qa");
      const runtimeDir = path.join(source, "runtime");
      const approvedParent = path.join(source, ".local", "Lab", "Runs");
      const oldParent = path.join(temporary, "NeoBabylon-Data", "QA");
      const releaseDir = path.join(source, "docs", "release");
      for (const directory of [qaDir, runtimeDir, approvedParent, oldParent, releaseDir]) mkdirSync(directory, { recursive: true });
      copyFileSync(path.join(qaSource, "qa-run-root.mjs"), path.join(qaDir, "qa-run-root.mjs"));
      const binary = path.join(runtimeDir, "mock-app-server.exe");
      writeFileSync(binary, "offline fixture", "utf8");
      const sha256 = createHash("sha256").update("offline fixture").digest("hex");
      writeFileSync(path.join(runtimeDir, "runtime-lock.json"), JSON.stringify({ runtime: {
        appServerBinaryRelativePath: "runtime/mock-app-server.exe", sha256,
      } }));
      writeFileSync(path.join(releaseDir, "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json"), JSON.stringify({
        providerId: "openrouter",
        modelIdentifier: "nex-agi/nex-n2.5-pro:free",
        providerRoute: { state: "Known", value: { endpointTag: "nex-agi/fp8" } },
      }));

      const lines = readFileSync(path.join(qaSource, name), "utf8").split(/\r?\n/);
      const stop = name === "native-live-nex-output.mjs"
        ? lines.findIndex((line) => line.startsWith("const directResult ="))
        : lines.findIndex((line) => line.startsWith("const { runRoot") && line.includes("newQaRun(qaParent,"));
      assert.ok(stop > 0, `${name}: preflight boundary was not found`);
      const prefix = lines.slice(0, stop + (name === "native-live-nex-output.mjs" ? 0 : 1)).join("\n");
      assert.ok(!prefix.includes("host = spawn("), `${name}: test preflight would launch the native host`);
      assert.ok(!prefix.includes("fetch("), `${name}: test preflight would make a network request`);
      const preflight = path.join(qaDir, name);
      writeFileSync(preflight, `${prefix}\nconsole.log("QA_PARENT_ACCEPTED");\n`);

      const fourArgs = ["native-history-pagination.mjs", "native-large-output.mjs", "native-live-nex-output.mjs"].includes(name);
      const run = (parent) => spawnSync(process.execPath,
        [preflight, ...Array(fourArgs ? 3 : 2).fill(process.execPath), parent],
        { encoding: "utf8", timeout: 10_000, env: { ...process.env, OPENROUTER_API_KEY: "offline-qa-placeholder" } });
      const accepted = run(approvedParent);
      assert.equal(accepted.status, 0, `${name}: approved parent failed: ${accepted.stderr}`);
      assert.match(accepted.stdout, /QA_PARENT_ACCEPTED/);
      const rejected = run(oldParent);
      assert.notEqual(rejected.status, 0, `${name}: old sibling was accepted`);
      assert.match(rejected.stderr, /QA runs parent must be exactly/);
      assert.deepEqual(readdirSync(oldParent), [], `${name}: old sibling was modified`);
    } finally { rmSync(temporary, { recursive: true, force: true }); }
  }
});
