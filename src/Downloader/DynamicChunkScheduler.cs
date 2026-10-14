namespace Downloader;

/// <summary>
/// Assigns queued chunks to parallel workers and, once the queue is empty, lets an idle worker
/// take half of the largest active chunk's unconsumed tail.
/// </summary>
internal sealed class DynamicChunkScheduler
{
    private readonly DownloadPackage _package;
    private readonly long _minimumChunkSize;
    private readonly Queue<Chunk> _queued;
    private readonly HashSet<Chunk> _active = [];
    private readonly SemaphoreSlim _workAvailable = new(0);
    private readonly object _sync = new();
    private int _waitingWorkers;

    internal DynamicChunkScheduler(DownloadPackage package, long minimumChunkSize)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));
        _minimumChunkSize = minimumChunkSize;
        _queued = new Queue<Chunk>(package.Chunks ?? []);
    }

    internal async ValueTask<Chunk> GetNextAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_sync)
            {
                if (_queued.Count > 0)
                {
                    Chunk chunk = _queued.Dequeue();
                    _active.Add(chunk);
                    return chunk;
                }

                if (_active.Count == 0)
                    return null;

                _waitingWorkers++;
            }

            try
            {
                await _workAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                    _waitingWorkers--;
            }
        }
    }

    internal void Complete(Chunk chunk)
    {
        int workersToRelease = 0;
        lock (_sync)
        {
            if (!_active.Remove(chunk))
                return;

            if (_active.Count == 0 && _queued.Count == 0)
                workersToRelease = _waitingWorkers;
        }

        if (workersToRelease > 0)
            _workAvailable.Release(workersToRelease);
    }

    /// <summary>
    /// Called by an active downloader immediately after it commits a write position. That byte
    /// boundary is the only point at which its unfinished tail may safely change ownership.
    /// </summary>
    internal bool TrySplitAfterProgress(Chunk progressedChunk)
    {
        bool queuedTail = false;
        lock (_sync)
        {
            if (_minimumChunkSize <= 0 ||
                _waitingWorkers == 0 ||
                _queued.Count > 0 ||
                !_active.Contains(progressedChunk))
            {
                return false;
            }

            Chunk largest = null;
            long largestRemaining = -1;
            foreach (Chunk activeChunk in _active)
            {
                long remaining = activeChunk.EmptyLength;
                if (remaining > largestRemaining)
                {
                    largest = activeChunk;
                    largestRemaining = remaining;
                }
            }

            if (!ReferenceEquals(largest, progressedChunk) ||
                !_package.TrySplitChunk(progressedChunk, _minimumChunkSize, out Chunk tail))
            {
                return false;
            }

            _queued.Enqueue(tail);
            queuedTail = true;
        }

        if (queuedTail)
            _workAvailable.Release();

        return queuedTail;
    }
}