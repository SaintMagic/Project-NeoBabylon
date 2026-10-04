using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoBabylon.Core;

internal static class GeneratedToolReviewChecks
{
    public static void ReviewBindsCurrentContentAndInterfaceWithoutActivatingCandidate()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Inspected the declared fixture candidate.");
            Require(review.Decision == "reviewed" && review.Interaction == "product-mediated-local-interaction",
                "review claimed an unverified reviewer or another transition");
            Require(review.ReviewInterfaceVersion == GeneratedToolReviewStore.ReviewInterfaceVersion,
                "review identity was not bound to the product-owned review interface");
            Require(review.SchemaVersion == 1,
                "unshipped atomic review format should start at schema version 1");
            Require(review.Sequence == 1 && review.PreviousRecordSha256 == string.Empty && review.RecordSha256.Length == 64,
                "review did not store the first integrity-linked record");
            Require(GeneratedToolReviewStore.ReadCurrent(applicationRoot, candidate.Manifest.ToolId) == review,
                "review did not survive a fresh candidate read");
            Require(GeneratedToolCandidateStore.Read(applicationRoot, candidate.Manifest.ToolId)?.State == "unapproved",
                "review activated or rewrote the candidate");
            Require(GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).SequenceEqual([review]),
                "review history was not retained separately");
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Duplicate."));
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.RecordRejection(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Conflicting decision."));
        });
    }

    public static void RejectionHistorySurvivesCandidateMutationAndNewIdentityNeedsNewReview()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var rejected = GeneratedToolReviewStore.RecordRejection(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Not suitable for integration.");
            Require(rejected.Decision == "rejected", "explicit rejection was not recorded");
            var entryPoint = Path.Combine(candidate.CandidatePath, "tool.js");
            var originalBytes = File.ReadAllBytes(entryPoint);
            File.WriteAllText(entryPoint, "changed candidate bytes");
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Stale bytes."));
            Require(GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).SequenceEqual([rejected]),
                "failed transition erased historical rejection");

            File.WriteAllBytes(entryPoint, originalBytes);
            var changedManifest = candidate.Manifest with { Purpose = "Changed reviewable purpose" };
            File.WriteAllBytes(Path.Combine(candidate.CandidatePath, "tool.json"),
                JsonSerializer.SerializeToUtf8Bytes(changedManifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            var changed = GeneratedToolCandidateStore.Read(applicationRoot, candidate.Manifest.ToolId)
                ?? throw new InvalidOperationException("valid changed candidate was not readable");
            Require(changed.ContentIdentity != candidate.ContentIdentity, "changed manifest kept the old content identity");
            Require(GeneratedToolReviewStore.ReadCurrent(applicationRoot, candidate.Manifest.ToolId) is null,
                "old rejection was applied to changed candidate content");
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Stale identity."));
            var current = GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, changed.ContentIdentity, "Reviewed changed candidate separately.");
            Require(current.Sequence == 2 && current.PreviousRecordSha256 == rejected.RecordSha256,
                "second decision did not link to the prior history record");
            Require(GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 2
                && GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).Contains(rejected)
                && GeneratedToolReviewStore.ReadCurrent(applicationRoot, candidate.Manifest.ToolId) == current,
                "append-only history did not preserve both distinct content decisions");
            var reviewsRoot = Path.Combine(applicationRoot, "Data", "GeneratedTools", "Reviews", candidate.Manifest.ToolId);
            File.Delete(Path.Combine(reviewsRoot, "0000000001.json"));
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId));
        });
    }

    public static void CorruptHistoryFailsClosedWithoutReplacingEvidence()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var stagingRoot = Path.Combine(applicationRoot, "Data", "GeneratedTools", "Reviews", ".staging", candidate.Manifest.ToolId);
            Directory.CreateDirectory(stagingRoot);
            var incompleteStaging = Path.Combine(stagingRoot, "interrupted.json");
            File.WriteAllText(incompleteStaging, "{incomplete");
            Require(GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 0,
                "incomplete staging poisoned final history");
            GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Fixture review.");
            Require(File.ReadAllText(incompleteStaging) == "{incomplete",
                "incomplete staging diagnostic was removed");
            var reviewsRoot = Path.Combine(applicationRoot, "Data", "GeneratedTools", "Reviews", candidate.Manifest.ToolId);
            var eventPath = Directory.GetFiles(reviewsRoot).Single();
            var eventJson = JsonNode.Parse(File.ReadAllText(eventPath))?.AsObject()
                ?? throw new InvalidOperationException("Fixture review JSON was not an object.");
            eventJson["note"] = "Changed after publication.";
            File.WriteAllText(eventPath, eventJson.ToJsonString());
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId));
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.RecordRejection(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Must not bypass corrupt history."));
            Require(Directory.GetFiles(reviewsRoot).Length == 1 && File.ReadAllText(eventPath).Contains("Changed after publication.", StringComparison.Ordinal),
                "corrupt review evidence was overwritten or silently cleaned");
        });
        WithCandidate((applicationRoot, candidate) =>
        {
            GeneratedToolReviewStore.RecordReview(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, "Fixture review.");
            var reviewsRoot = Path.Combine(applicationRoot, "Data", "GeneratedTools", "Reviews", candidate.Manifest.ToolId);
            var eventPath = Directory.GetFiles(reviewsRoot).Single();
            var draftShape = JsonNode.Parse(File.ReadAllText(eventPath))?.AsObject()
                ?? throw new InvalidOperationException("Fixture review JSON was not an object.");
            draftShape.Remove("sequence");
            draftShape.Remove("previousRecordSha256");
            draftShape.Remove("recordSha256");
            File.WriteAllText(eventPath, draftShape.ToJsonString());
            RequireThrows<InvalidDataException>(() => GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId));
            Require(File.Exists(eventPath), "draft-shaped evidence was removed instead of rejected");
        });
    }

    private static void WithCandidate(Action<string, GeneratedToolCandidate> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Task9-Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var applicationRoot = Path.Combine(root, "app");
        Directory.CreateDirectory(sourceRoot);
        try
        {
            ApplicationRootLayout.Create(sourceRoot, applicationRoot);
            var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase)
            {
                ["tool.js"] = Encoding.UTF8.GetBytes("export function invoke() { return 'fixture'; }"),
                ["evidence/search.txt"] = Encoding.UTF8.GetBytes("Declared search."),
                ["evidence/composition.txt"] = Encoding.UTF8.GetBytes("Declared composition."),
                ["evidence/authority.txt"] = Encoding.UTF8.GetBytes("Declared authority."),
                ["evidence/gap.txt"] = Encoding.UTF8.GetBytes("Declared gap.")
            };
            GeneratedToolEvidence Evidence(string kind, string id, string path) =>
                new(kind, id, "unverified-declaration", "Creator declaration", path, Hash(files[path].Span));
            var manifest = new GeneratedToolCandidateManifest(
                1, "NeoBabylon", "fixture-tool",
                Path.Combine(applicationRoot, "Data", "GeneratedTools", "Candidates", "fixture-tool"),
                "revision-1", "Fixture tool", "Exercise review state", "Return fixture value", "Declared capability gap",
                new GeneratedToolOrigin("task-9", "session-9", DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
                    "fixture creator", "fixture provider", "fixture model", "cap-v1:sha256:" + Hash(Encoding.UTF8.GetBytes("capability")), "gap-1"),
                "tool.js", new GeneratedToolContract("manual only", "{}", "{}"),
                new GeneratedToolAuthority(["read-only"], ["fixture workspace"], [], ["fixture input"],
                    "deny", "read-only", "never", Hash(files["evidence/authority.txt"].Span)),
                [],
                [
                    Evidence("existing-tool-search", "search-1", "evidence/search.txt"),
                    Evidence("composition-attempt", "composition-1", "evidence/composition.txt"),
                    Evidence("current-task-authority", "authority-1", "evidence/authority.txt"),
                    Evidence("capability-gap", "gap-1", "evidence/gap.txt")
                ],
                files.Select(file => new GeneratedToolFile(file.Key, Hash(file.Value.Span))).ToArray(),
                "unapproved");
            check(applicationRoot, GeneratedToolCandidateStore.Create(applicationRoot, manifest, files));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
