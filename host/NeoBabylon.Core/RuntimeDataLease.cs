namespace NeoBabylon.Core;

public sealed class RuntimeDataLease : IDisposable
{
    private readonly FileStream _stream;
    private int _disposed;

    private RuntimeDataLease(FileStream stream)
    {
        _stream = stream;
    }

    public static RuntimeDataLease Acquire(string dataRoot)
    {
        var root = Path.GetFullPath(dataRoot);
        Directory.CreateDirectory(root);
        var lockPath = Path.Combine(root, ".neobabylon-runtime.lock");
        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.SequentialScan);
            return new RuntimeDataLease(stream);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"The NeoBabylon runtime data root is already owned: {root}",
                ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stream.Dispose();
        }
    }
}
