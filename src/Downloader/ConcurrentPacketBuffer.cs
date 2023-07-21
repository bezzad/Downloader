using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
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
        private long _bufferSize = long.MaxValue;
        protected readonly SemaphoreSlim _queueConsumeLocker = new SemaphoreSlim(0);
        protected readonly PauseTokenSource _addingBlocker = new PauseTokenSource();
        protected readonly ManualResetEventSlim _completionEvent = new ManualResetEventSlim(true);
        protected readonly ConcurrentQueue<T> _queue;

        public long BufferSize
        {
            get => _bufferSize;
            set
            {
                _bufferSize = (value <= 0) ? long.MaxValue : value;
            }
        }

        public ConcurrentPacketBuffer(long size) : this()
        {
            BufferSize = size;
        }

        public ConcurrentPacketBuffer()
        {
            _queue = new ConcurrentQueue<T>();
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
            await _addingBlocker.WaitWhilePausedAsync();
            _queue.Enqueue(item);
            _queueConsumeLocker.Release();
            _completionEvent.Reset();
            StopAddingIfLimitationExceeded(item.Length);
            return true;
        }

        public async Task<T> WaitTryTakeAsync(CancellationToken cancellation)
        {
            ResumeAddingIfEmpty();
            await _queueConsumeLocker.WaitAsync(cancellation).ConfigureAwait(false);
            if (_queue.TryDequeue(out var item))
            {
                return item;
            }

            return null;
        }

        private void StopAddingIfLimitationExceeded(long packetSize)
        {
            if (BufferSize < packetSize * Count)
            {
                // Stop writing packets to the queue until the memory is free
                CompleteAdding();
            }
        }

        private void ResumeAddingIfEmpty()
        {
            if (IsEmpty)
            {
                // resume writing packets to the queue
                ResumeAdding();
                _completionEvent.Set();
            }
        }

        public void WaitToComplete()
        {
            _completionEvent.Wait();
        }

        public void CompleteAdding()
        {
            // stop writing new items to the list by blocking writer threads
            _addingBlocker.Pause();
        }

        public void ResumeAdding()
        {
            // resume writing new item to the list
            _addingBlocker.Resume();
        }

        public void Dispose()
        {
            CompleteAdding();
            _queueConsumeLocker.Dispose();
            _completionEvent?.Dispose();
        }
    }
}
