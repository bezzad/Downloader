using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a thread-safe, ordered collection of objects.
/// With thread-safe multi-thread adding and single-thread consuming methodology (N producers - 1 consumer)
/// </summary>
/// <typeparam name="T">Specifies the type of elements in the ConcurrentDictionary.</typeparam>
/// <remarks>
[DebuggerTypeProxy(typeof(IReadOnlyCollection<>))]
[DebuggerDisplay("Count = {Count}")]
internal class ConcurrentPacketBuffer<T> : IReadOnlyCollection<T>, IDisposable where T : class, ISizeableObject
{
    private volatile bool _disposed = false;
    private long _bufferSize = long.MaxValue;
    protected readonly ILogger _logger;
    protected readonly SemaphoreSlim _queueConsumeLocker = new SemaphoreSlim(0);
    protected readonly PauseTokenSource _addingBlocker = new PauseTokenSource();
    protected readonly PauseTokenSource _flushBlocker = new PauseTokenSource();
    protected readonly ConcurrentQueue<T> _queue;

    public long BufferSize
    {
        get => _bufferSize;
        set
        {
            _bufferSize = (value <= 0) ? long.MaxValue : value;
        }
    }

    public ConcurrentPacketBuffer(long size, ILogger logger = null) : this(logger)
    {
        BufferSize = size;
    }

    public ConcurrentPacketBuffer(ILogger logger = null)
    {
        _queue = new ConcurrentQueue<T>();
        _logger = logger;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _queue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _queue.Count;
    public bool IsAddingCompleted => _addingBlocker.IsPaused;
    public bool IsEmpty => _queue.Count == 0;

    public T[] ToArray()
    {
        return _queue.ToArray();
    }

    public async Task<bool> TryAdd(T item)
    {
        try
        {
            await _addingBlocker.WaitWhilePausedAsync().ConfigureAwait(false);
            _flushBlocker.Pause();
            _queue.Enqueue(item);
            _queueConsumeLocker.Release();
            StopAddingIfLimitationExceeded(item.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task WaitTryTakeAsync(CancellationToken cancellation, Func<T, Task> callbackTask)
    {
        try
        {
            await _queueConsumeLocker.WaitAsync(cancellation).ConfigureAwait(false);
            if (_queue.TryDequeue(out var item) && item != null)
            {
                await callbackTask(item).ConfigureAwait(false);
            }
        }
        finally
        {
            ResumeAddingIfEmpty();
        }
    }

    private void StopAddingIfLimitationExceeded(long packetSize)
    {
        if (BufferSize < packetSize * Count)
        {
            _logger?.LogDebug($"ConcurrentPacketBuffer: Stop writing packets to the queue on size {packetSize * Count}bytes until the memory is free");
            StopAdding();
        }
    }

    private void ResumeAddingIfEmpty()
    {
        if (IsEmpty)
        {
            _flushBlocker.Resume();
            ResumeAdding();
        }
    }

    public async Task WaitToComplete()
    {
        await _flushBlocker.WaitWhilePausedAsync().ConfigureAwait(false);
    }

    public void StopAdding()
    {
        _logger?.LogDebug("ConcurrentPacketBuffer: stop writing new items to the list by blocking writer threads");
        _addingBlocker.Pause();
    }

    public void ResumeAdding()
    {
        _logger?.LogDebug("ConcurrentPacketBuffer: resume writing new item to the list");
        _addingBlocker.Resume();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAdding();
        _queueConsumeLocker.Dispose();
        _addingBlocker.Resume();
    }
}
