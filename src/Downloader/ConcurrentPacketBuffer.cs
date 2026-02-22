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
/// <remarks>
/// <typeparam name="T">Specifies the type of elements in the ConcurrentDictionary.</typeparam>
/// </remarks>
[DebuggerTypeProxy(typeof(IReadOnlyCollection<>))]
[DebuggerDisplay("Count = {Count}")]
internal class ConcurrentPacketBuffer<T>(ILogger logger = null) : IReadOnlyCollection<T>, IDisposable
    where T : ISizeableObject, IDisposable
{
    private volatile bool _disposed;
    private readonly SemaphoreSlim _queueConsumeLocker = new(0);
    private readonly PauseTokenSource _addingBlocker = new();
    private readonly PauseTokenSource _flushBlocker = new();
    private readonly ConcurrentQueue<T> _queue = new();

    public long BufferSize
    {
        get;
        set => field = (value <= 0) ? long.MaxValue : value;
    } = long.MaxValue;

    public ConcurrentPacketBuffer(long size, ILogger logger = null) : this(logger)
    {
        BufferSize = size;
    }

    public IEnumerator<T> GetEnumerator()
    {
        // ReSharper disable once NotDisposedResourceIsReturned
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

    public void Add(T item)
    {
        _queue.Enqueue(item);
        _queueConsumeLocker.Release();
    }

    public async Task<bool> TryAddAsync(T item)
    {
        try
        {
            await _addingBlocker.WaitWhilePausedAsync().ConfigureAwait(false);
            _flushBlocker.Pause();
            Add(item);
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
            if (_queue.TryDequeue(out T item) && item != null)
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
            logger?.LogDebug($"ConcurrentPacketBuffer: Stop writing packets to the queue on " +
                             $"size {packetSize * Count}bytes until the memory is free");
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

    private void StopAdding()
    {
        logger?.LogDebug("ConcurrentPacketBuffer: stop writing new items to the list by blocking writer threads");
        _addingBlocker.Pause();
    }

    private void ResumeAdding()
    {
        logger?.LogDebug("ConcurrentPacketBuffer: resume writing new item to the list");
        _addingBlocker.Resume();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopAdding();
            _queueConsumeLocker.Dispose();
            _addingBlocker.Resume();
        }
    }
}