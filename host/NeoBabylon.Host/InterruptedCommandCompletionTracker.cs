using NeoBabylon.Core;

namespace NeoBabylon.Host;

public sealed class InterruptedCommandCompletionTracker
{
    private readonly object _gate = new();
    private readonly string _threadId;
    private readonly string _turnId;
    private readonly HashSet<string> _candidateItemIds;
    private readonly HashSet<string> _completedItemIds = new(StringComparer.Ordinal);

    public InterruptedCommandCompletionTracker(
        string threadId,
        string turnId,
        IEnumerable<string> candidateItemIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        ArgumentNullException.ThrowIfNull(candidateItemIds);
        _threadId = threadId;
        _turnId = turnId;
        _candidateItemIds = new HashSet<string>(candidateItemIds, StringComparer.Ordinal);
    }

    public void Observe(AppServerNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.Method != "item/completed"
            || !string.Equals(notification.Params["threadId"]?.GetValue<string>(), _threadId, StringComparison.Ordinal)
            || !string.Equals(notification.Params["turnId"]?.GetValue<string>(), _turnId, StringComparison.Ordinal)
            || notification.Params["item"]?["type"]?.GetValue<string>() != "commandExecution"
            || notification.Params["item"]?["id"]?.GetValue<string>() is not { Length: > 0 } itemId)
        {
            return;
        }

        lock (_gate)
        {
            if (_candidateItemIds.Contains(itemId))
            {
                _completedItemIds.Add(itemId);
            }
        }
    }

    public bool HasCompleted(string itemId)
    {
        lock (_gate)
        {
            return _completedItemIds.Contains(itemId);
        }
    }

    public bool MayPublishRunningBinding(string itemId)
    {
        lock (_gate)
        {
            return _candidateItemIds.Contains(itemId) && !_completedItemIds.Contains(itemId);
        }
    }
}
