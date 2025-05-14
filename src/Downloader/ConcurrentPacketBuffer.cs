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
    where T : class, ISizeableObject
{
    private volatile bool _disposed;
    private long _bufferSize = long.MaxValue;
    protected readonly ILogger Logger = logger;
    protected readonly SemaphoreSlim QueueConsumeLocker = new(0);
    protected readonly PauseTokenSource AddingBlocker = new();
    protected readonly PauseTokenSource FlushBlocker = new();
    protected readonly ConcurrentQueue<T> Queue = new();

    public long BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = (value <= 0) ? long.MaxValue : value;
    }

    public ConcurrentPacketBuffer(long size, ILogger logger = null) : this(logger)
    {
        BufferSize = size;
    }

    public IEnumerator<T> GetEnumerator()
    {
        // ReSharper disable once NotDisposedResourceIsReturned
        return Queue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => Queue.Count;
    public bool IsAddingCompleted => AddingBlocker.IsPaused;
    public bool IsEmpty => Queue.Count == 0;

    public T[] ToArray()
    {
        return Queue.ToArray();
    }

    public async Task<bool> TryAdd(T item)
    {
        try
        {
            await AddingBlocker.WaitWhilePausedAsync().ConfigureAwait(false);
            FlushBlocker.Pause();
            Queue.Enqueue(item);
            QueueConsumeLocker.Release();
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
            await QueueConsumeLocker.WaitAsync(cancellation).ConfigureAwait(false);
            if (Queue.TryDequeue(out T item) && item != null)
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
            Logger?.LogDebug($"ConcurrentPacketBuffer: Stop writing packets to the queue on " +
                             $"size {packetSize * Count}bytes until the memory is free");
            StopAdding();
        }
    }

    private void ResumeAddingIfEmpty()
    {
        if (IsEmpty)
        {
            FlushBlocker.Resume();
            ResumeAdding();
        }
    }

    public async Task WaitToComplete()
    {
        await FlushBlocker.WaitWhilePausedAsync().ConfigureAwait(false);
    }

    public void StopAdding()
    {
        Logger?.LogDebug("ConcurrentPacketBuffer: stop writing new items to the list by blocking writer threads");
        AddingBlocker.Pause();
    }

    public void ResumeAdding()
    {
        Logger?.LogDebug("ConcurrentPacketBuffer: resume writing new item to the list");
        AddingBlocker.Resume();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopAdding();
            QueueConsumeLocker.Dispose();
            AddingBlocker.Resume();
        }
    }
}