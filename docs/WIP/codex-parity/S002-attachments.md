# S002 — Durable conversation attachments

Priority: Important. Specification, not an implemented patch. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Current path and missing pieces

`App.tsx` has a text-only textarea. `bridge.ts`, `Core/AppServerClient.cs:DiagnosticOperation.Parse`, `MainWindow.xaml.cs` and `RuntimeSupervisor.StartTurnAsync` admit text/output-limit/effort only. `Core/AppServerProtocol.cs:TurnStartOptions` and `BuildTurnStartRequest` build one text input. `ThreadTranscriptProjector.cs` explicitly omits non-text input; `drafts.mjs` reconciles pending submissions using text and thread identity. Merely adding a paperclip would leave send, persistence, resume and model switching broken.

Reuse those paths. Add `Core/AttachmentManifest.cs`, `AttachmentStore.cs`, host `AttachmentService`, and UI `AttachmentTray.tsx`/attachment draft reducer. Keep native filesystem/clipboard access in the host. The generated exact pinned protocol remains authoritative; current online App Server docs describe text/image/localImage input but do not establish a generic PDF upload method.

## Data and lifecycle

Store immutable copied payloads below `Data/NeoBabylon/Attachments/blobs/<sha256>` and protected versioned manifests below an app-owned attachments namespace. A descriptor contains opaque attachment ID, content hash, original display name, detected media type, byte length, image dimensions when applicable, source-kind, draft/workspace identity and creation time. Keep original source path in host metadata only when needed; it is not a renderer authority token.

A draft manifest owns an ordered list of attachment IDs. A send receipt binds submission ID, thread ID, expected previous turn, capability identity, manifest hash and immutable text snapshot. States: staging → ready → submitting → accepted; or rejected / acceptance-unknown. Link accepted attachments to the runtime's returned turn/user-item identity. Payloads remain immutable after submission; editing/removing an attachment creates a new draft revision.

Commit blobs before manifests. A failed copy or manifest commit leaves an explicit failed chip and recoverable draft, never a fake attachment. For a crash after runtime admission but before local acknowledgement, reconcile the pending manifest and journal user item; do not resend automatically. Text equality alone is insufficient once two messages can have the same text with different images. An unknown admission state retains all candidate payloads until resolved.

Suggested initial safety bounds: eight attachments per draft, 20 MiB per copied file, 100 MiB total draft payload and 24 megapixels per decoded image. Enforce byte/pixel bounds in the host before decoding, not only in React. Values are initial product limits and must be displayed. Oversize input fails clearly; do not silently resize, truncate or OCR it. File MIME, extension and decoder result must agree enough to reject disguised executables as images.

## Native and renderer operations

Propose `pickAttachments(draftId, expectedRevision)`, `pasteImageAttachment(draftId, expectedRevision)`, `removeDraftAttachment`, `getAttachmentPreview`, and attachment-aware `startTurn` with ordered attachment IDs and manifest identity. All are named, bounded operations, not arbitrary file-read RPC.

The native file picker is the user gesture authorizing reads of precisely the selected files. Selecting a file does not authorize enumerating its parent. Copy from a verified file handle; reject paths/reparse transitions outside the explicitly selected target, device paths and directories. The host reads clipboard image data only for an explicit paste action, never on a timer or model request. This avoids sending huge base64 blobs through the generic JSON bridge. Preserve ordinary text paste. Add drag/drop only after the WebView2-to-host file identity path is verified; do not trust a renderer-supplied path merely because it came from a DOM event.

Expose previews through an app-owned resource route resolving opaque IDs to host-owned staged blobs. Never serve arbitrary file:// paths or auto-load remote images from document content. Provide filename/size/remove/retry controls, keyboard focus and a staging indicator. An image-only message can be sent. Send is blocked while staging or when the selected model cannot handle an attached modality; draft editing remains available.

## Provider mapping

Extend turn input as a discriminated list generated from the pinned schema, not an untyped provider dictionary. For image-capable exact tuples, use the supported local-image input; prove the runtime performs the intended provider serialization. LM Studio/OpenRouter image support is admitted from the selected capability plus exact transport tests, not provider brand alone.

The current NVIDIA adapter rejects unsupported input item types: keep that fail-closed behavior until an image translation has separate fixtures/live qualification. Do not send image bytes as ordinary text or strip the image during a model switch. Text/documents initially become explicitly scoped local attachment references for authorized runtime file tools, with a clear 'local file reference, not uploaded document parsing' presentation. Do not claim native PDF understanding on a text-only model. Automatic extraction/OCR, if later added, retains original bytes and labels extraction quality and truncation.

## Restoration and migration

`ThreadTranscriptProjector` and the saved item model must retain attachment descriptors rather than `[Non-text input omitted]` for new managed attachments. On reopening, join only exact app manifest/thread/item identities. Older journal images without an app-managed manifest display an unavailable/external reference state; do not fetch arbitrary historical paths automatically. Forks reference immutable blobs through new manifests, preserving provenance. Deletion/GC checks draft, accepted, fork and acceptance-unknown references; automatic cleanup is limited to unreferenced app-owned staging data.

## Tests, acceptance and order

Test two same-text messages with different images; image-only input; cancel picker; copy failure; corrupt image; path traversal/junction replacement; quota exhaustion; duplicate attachment IDs; reordered manifests; stale workspace/capability; unsupported model switch; crash before/after admission; fork/reopen after original source deletion; missing blobs; and GC protecting unknown submissions. No original user file is modified.

Implement persistence and receipt reconciliation first, native picker and tray second, one verified image provider path third, then paste and restoration/fork. Acceptance is a deterministic exact request plus native send/restart/reopen showing the same payload hash and original ordering. S004 rich rendering can follow; persistence cannot.
