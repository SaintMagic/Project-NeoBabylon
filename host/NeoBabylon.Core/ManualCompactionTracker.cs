using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public sealed class ManualCompactionTracker(string threadId)
{
    private string? _itemId;
    private bool _itemCompleted;
    private string _outcome = "unknown";
    private string? _failureType;
    public string? TurnId { get; private set; }
    public bool Finished { get; private set; }
    public bool HasConfirmedTerminal { get; private set; }
    public bool RequestNotSent { get; private set; }

    public bool Observe(AppServerNotification notification)
    {
        if (Finished || Text(notification.Params["threadId"]) != threadId) return false;
        var turnId = Text(notification.Params["turnId"]) ?? Text(notification.Params["turn"]?["id"]);
        if (string.IsNullOrWhiteSpace(turnId)) return false;
        if (notification.Method == "turn/started")
        {
            if (TurnId is not null && TurnId != turnId) { FinishUnknown("identityLost"); return false; }
            TurnId = turnId;
            return true;
        }
        if (TurnId is null || turnId != TurnId) return false;
        if (notification.Method is "item/started" or "item/completed"
            && Text(notification.Params["item"]?["type"]) == "contextCompaction")
        {
            var itemId = Text(notification.Params["item"]?["id"]);
            if (string.IsNullOrWhiteSpace(itemId)) { FinishUnknown("identityLost"); return false; }
            if (notification.Method == "item/started")
            {
                if (_itemId is not null && _itemId != itemId) { FinishUnknown("identityLost"); return false; }
                _itemId = itemId;
            }
            else if (_itemId == itemId) _itemCompleted = true;
            return true;
        }
        if (notification.Method == "error")
        {
            if (notification.Params["willRetry"] is JsonValue retry && retry.TryGetValue<bool>(out var willRetry) && willRetry)
                return true;
            _failureType = "providerFailure";
            // Core can report this failure and still emit turn/completed afterwards.
            return true;
        }
        if (notification.Method == "turn/completed")
        {
            var status = Text(notification.Params["turn"]?["status"]);
            Finished = true;
            HasConfirmedTerminal = true;
            if (status is "failed" or "interrupted" || _failureType is not null)
            {
                _outcome = "failed";
                _failureType ??= status == "interrupted" ? "interrupted" : "providerFailure";
            }
            else if (status == "completed" && _itemCompleted) _outcome = "completed";
            else _failureType = "unprovenCompaction";
            return true;
        }
        return notification.Method == "thread/tokenUsage/updated";
    }

    public JsonObject FinishUnknown(string type)
    {
        if (!Finished) { Finished = true; _outcome = "unknown"; _failureType = type; }
        return Receipt();
    }

    public void RejectRequest()
    {
        Finished = true;
        HasConfirmedTerminal = true;
        _outcome = "failed";
        _failureType = "requestRejected";
    }

    public void RejectBeforeDispatch()
    {
        Finished = true;
        RequestNotSent = true;
        _outcome = "unknown";
        _failureType = "hostNotificationBacklog";
    }

    public JsonObject Receipt()
    {
        var receipt = new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["eventType"] = "compactionOutcome",
            ["threadId"] = threadId,
            ["turnId"] = TurnId,
            ["outcome"] = _outcome,
            ["succeeded"] = _outcome == "unknown" ? null : JsonValue.Create(_outcome == "completed"),
            ["failure"] = _failureType is null ? null : new JsonObject
            {
                ["type"] = _failureType,
                ["attributedTo"] = "NeoBabylon.Host",
                ["message"] = RequestNotSent
                    ? "The Host notification backlog could not be verified clear within its bound. No compaction request was sent; retry after notifications settle."
                    : _outcome == "failed"
                        ? "Codex App Server reported that context compaction failed or was interrupted."
                        : "Context compaction completion could not be verified. The request may still be running; no success is claimed."
            }
        };
        if (RequestNotSent) receipt["requestSent"] = false;
        return receipt;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
