#!/usr/bin/env python3
"""Check/apply each declared patch combination in disposable detached worktrees.

Run from any location: python docs/WIP/codex-parity/verify-patches.py
Requires Git and the exact reviewed base object. Never applies to the caller's tree.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path

BASE = "f126612a00231a58626d72108aac6ee912431ee3"
DEPENDENCIES = {"001": [], "002": [], "003": ["002"], "004": [], "005": ["004"], "006": [], "007": []}


def run(args: list[str], cwd: Path) -> str:
    process = subprocess.run(args, cwd=cwd, text=True, encoding="utf-8", errors="replace", capture_output=True)
    if process.returncode:
        raise RuntimeError(f"{args!r}: {process.stdout}\n{process.stderr}")
    return process.stdout.strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--output", type=Path, default=Path("patch-verification.json"))
    options = parser.parse_args()
    repository = options.repository.resolve()
    patches = {p.name[:3]: p.resolve() for p in sorted((repository / "patches/codex-parity").glob("[0-9][0-9][0-9]-*.patch"))}
    if patches.keys() != DEPENDENCIES.keys():
        raise RuntimeError("Patch series does not match the reviewed dependency manifest.")
    if run(["git", "rev-parse", BASE + "^{commit}"], repository) != BASE:
        raise RuntimeError("Exact reviewed base is unavailable; fetch it explicitly.")
    evidence = {"sourceCommit": BASE, "checks": [], "patches": [
        {"file": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest(), "dependencies": DEPENDENCIES[k]}
        for k, p in patches.items()
    ]}
    for label, series in [(k, DEPENDENCIES[k] + [k]) for k in patches] + [("cumulative", list(patches))]:
        with tempfile.TemporaryDirectory(prefix="nb-parity-check-") as temporary:
            worktree = Path(temporary) / "source"
            run(["git", "worktree", "add", "--detach", str(worktree), BASE], repository)
            try:
                for k in series:
                    run(["git", "apply", "--check", "--index", str(patches[k])], worktree)
                    run(["git", "apply", "--index", str(patches[k])], worktree)
                run(["git", "diff", "--cached", "--check"], worktree)
                changed = run(["git", "diff", "--cached", "--name-only"], worktree).splitlines()
                for k in reversed(series):
                    run(["git", "apply", "--reverse", "--check", "--index", str(patches[k])], worktree)
                    run(["git", "apply", "--reverse", "--index", str(patches[k])], worktree)
                if run(["git", "status", "--porcelain"], worktree):
                    raise RuntimeError("Reverse series did not restore the exact base tree.")
                evidence["checks"].append({"label": label, "series": series, "apply": "pass", "reverse": "pass", "changedFiles": changed})
                print(f"PASS {label}: apply, whitespace and exact reverse ({','.join(series)})", flush=True)
            finally:
                run(["git", "worktree", "remove", "--force", str(worktree)], repository)
    options.output.parent.mkdir(parents=True, exist_ok=True)
    options.output.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
