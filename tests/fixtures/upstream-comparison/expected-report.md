# Upstream comparison

## Pinned source

- Manifest record: `CODEX-SOURCE-FIXTURE` (rust-v9.8.7)
- Expected revision (manifest): `a15149613060f1705910898e0e4e9d52fd22b30b`
- Runtime-lock revision: `a15149613060f1705910898e0e4e9d52fd22b30b`
- Runtime HEAD: `a15149613060f1705910898e0e4e9d52fd22b30b`
- Manifest/runtime-lock revision agreement: yes
- Locked runtime version: `9.8.7`

## Local runtime changes

- Tracked changed files: 1
  - `tracked.txt` (sha256 `753cab102715e65377df4a392dd72a9208b0cc2f337aef3094d49f1204c54ba2`)
- Untracked, non-ignored files: 1
  - `untracked.txt` (sha256 `5c63bc45c6af00c91437289c7ba77420eb28c25ad5b734806d054edc79f197b8`)
- Expected tracked diff fingerprint (runtime lock): `bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb`
- Tracked diff fingerprint matches runtime lock: no
- Actual tracked diff fingerprint (sha256): `98cf64407e3cac96ae7ef7ffc36b51db2291ddb3c0c57dbfa397fd19cbcc75cb`

## Fixed review actions

- Review every tracked change against the pinned upstream revision; this report is not approval.
- Review untracked files and hashes before deciding whether they belong in the runtime source tree.
- Resolve any manifest/runtime-lock revision mismatch manually; this generator never updates either record.
- Qualify built binaries and runtime behavior separately; source comparison does not establish either.
