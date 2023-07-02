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
    internal class ConcurrentPacketBuffer<T> : IReadOnlyCollection<T>, IDisposable where T : Packet
    {
        private readonly SemaphoreSlim _queueConsumeLocker = new SemaphoreSlim(0);
        private readonly ManualResetEventSlim _addingBlocker = new ManualResetEventSlim(true);
        protected readonly SemaphoreSlim _singleConsumerLock = new SemaphoreSlim(1);
        protected readonly ConcurrentQueue<T> _queue;

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
        public bool IsAddingCompleted => !_addingBlocker.IsSet;
        public bool IsEmpty => _queue.Count == 0;

        public T[] ToArray()
        {
            return _queue.ToArray();
        }

        public bool TryAdd(T item)
        {
            _addingBlocker.Wait();
            _queue.Enqueue(item);
            _queueConsumeLocker.Release();
            return true;
        }

        public async Task<T> WaitTryTakeAsync()
        {
            try
            {
                await _singleConsumerLock.WaitAsync().ConfigureAwait(false);
                await _queueConsumeLocker.WaitAsync().ConfigureAwait(false);
                if (_queue.TryDequeue(out var item))
                {
                    return item;
                }

                return null;
            }
            finally
            {
                _singleConsumerLock.Release();
                await Task.Yield();
            }
        }

        public void CompleteAdding()
        {
            // stop writing new items to the list by blocking writer threads
            _addingBlocker.Reset();
        }

        public void ResumeAdding()
        {
            // resume writing new item to the list
            _addingBlocker.Set();
        }

        public void Dispose()
        {
            CompleteAdding();
            _queueConsumeLocker.Dispose();
        }
    }
}
