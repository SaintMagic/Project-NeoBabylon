# Host runtime dispatcher contract

This is the Host-side API for the named operations dispatched by
`MainWindow.xaml.cs`. The dispatcher passes only the operation payload fields
shown below. It supplies the currently selected, trusted
`ModelCapabilityRecord` internally where required; callers never supply paths,
commands, MCP descriptors, or runtime identities.

```csharp
public Task<JsonObject> GetGeneratedToolActivationStatusAsync(
    string toolId,
    ModelCapabilityRecord selectedCapability,
    CancellationToken cancellationToken);

public JsonObject ActivateGeneratedTool(
    string toolId,
    string contentIdentity,
    string reviewIdentity,
    string expectedBindingRecordSha256,
    string note,
    ModelCapabilityRecord selectedCapability);

public JsonObject RevokeGeneratedToolActivation(
    string toolId,
    string expectedActivationRecordSha256,
    string note,
    ModelCapabilityRecord selectedCapability);

public JsonObject ListGeneratedToolActivationHistory(string toolId);

public JsonObject ReadGeneratedToolReviewComparison(
    string toolId,
    string contentIdentity);

public JsonObject ListProtectedRecordBackups(ProtectedDataRecordKey recordKey);

public JsonObject RestoreProtectedRecordBackup(
    ProtectedDataRecordKey recordKey,
    string expectedBackupSha256);
```

| Named operation | Payload fields | Host method |
|---|---|---|
| `getGeneratedToolActivationStatus` | `toolId` | `GetGeneratedToolActivationStatusAsync(toolId, _capability, cancellationToken)` |
| `activateGeneratedTool` | `toolId`, `contentIdentity`, `reviewIdentity`, `expectedBindingRecordSha256`, `note` | `ActivateGeneratedTool(..., _capability)` |
| `revokeGeneratedToolActivation` | `toolId`, `expectedActivationRecordSha256`, `note` | `RevokeGeneratedToolActivation(..., _capability)` |
| `listGeneratedToolActivationHistory` | `toolId` | `ListGeneratedToolActivationHistory(toolId)` |
| `readGeneratedToolReviewComparison` | `toolId`, `contentIdentity` | `ReadGeneratedToolReviewComparison(toolId, contentIdentity)` |
| `listProtectedRecordBackups` | `recordKey` (`Projects` or `ForkBookmarks`) | `ListProtectedRecordBackups(parsedRecordKey)` |
| `restoreProtectedRecordBackup` | `recordKey` (`Projects` or `ForkBookmarks`), `expectedBackupSha256` | `RestoreProtectedRecordBackup(parsedRecordKey, expectedBackupSha256)` |

Generated-tool status and activation resolve Node only from the trusted source
pin at `runtime/generated-tool-node-lock.json`, then ask
`AppPrivateGeneratedToolNodeRuntimeResolver` to verify the fixed app-private
executable. They pass that identity, the running App Server's actual
`RuntimeIdentity` (including executable SHA-256), the frozen selected
capability, effective `ToolExecutionPolicy`, and current MCP status/descriptors
to Core. The App Server executable hash is distinct from runtime/model
qualification evidence hashes. Activation intentionally supplies no observed
MCP status before registration; bootstrap registration is not permission to
start a model turn.

After registration, Host checks the exact MCP descriptor inventory and
`CallableTurnsAllowed` before every model turn. Unknown inventory blocks the
turn. Extra tools or schema mismatch trigger inert-config publication/reload
and append-only revocation; the attempted turn is not replayed. Runtime gates
remain compile-time closed, so current activation eligibility remains denied.

Protected-record keys are parsed strictly to the two enum values. Backup
operations return Host-attributed verified metadata only; restore remains
missing-only, hash-bound, journal/busy guarded, retains evidence, and returns
refresh/reopen guidance. Normal Codex history and credentials/storage outside
these two records are not in scope.

`GeneratedToolActivationHostException` projects `Code`, `Status`, `ToolId`,
and optional `FailureKind`. `ProtectedRecordHostException` projects `Code`,
`Status`, and optional fixed `RecordKey`. All successful operation responses
are attributed to `NeoBabylon.Host` by the operation projection.
