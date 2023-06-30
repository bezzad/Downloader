using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly ManualResetEventSlim _addingBlocker = new ManualResetEventSlim(true);
        protected readonly SemaphoreSlim _singleConsumerLock = new SemaphoreSlim(1);
        protected long _minPosition = long.MaxValue;
        protected readonly ConcurrentDictionary<long, T> _list;
        private readonly DebugLogger _logger = new DebugLogger();

        public ConcurrentPacketBuffer()
        {
            _list = new ConcurrentDictionary<long, T>();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => _list.Count;
        public bool IsAddingCompleted => !_addingBlocker.IsSet;
        public bool IsEmpty => _list.Count == 0;

        public T[] ToArray()
        {
            return _list.Values.ToArray();
        }

        public bool TryAdd(T item)
        {
            _addingBlocker.Wait();
            if (_list.TryAdd(item.Position, item))
            {
                Interlocked.CompareExchange(ref _minPosition, item.Position, Math.Min(_minPosition, item.Position));
                return true;
            }

            return false;
        }

        public async Task<T> TryTake()
        {
            try
            {
                T item = default;
                await _singleConsumerLock.WaitAsync().ConfigureAwait(false);

                if (_list.Count == 0)
                    return item;

                item = Pop();
                if (item is null)
                {
                    // Perhaps the next item being considered does not yet exist
                    // So, find minimum position
                    Interlocked.Exchange(ref _minPosition, _list.Keys.Min());
                    item = Pop();
                }

                await _logger.WriteLineAsync($"{{ Pos: {item?.Position ?? -1}, Buffer.Count: {Count} }}");
                return item;
            }
            finally
            {
                _singleConsumerLock.Release();
            }
        }

        private T Pop()
        {
            if (_list.TryRemove(_minPosition, out var item))
            {
                Interlocked.Exchange(ref _minPosition, item.EndOffset);
                return item;
            }

            return default;
        }

        public void CompleteAdding()
        {
            _logger.WriteLine("------------ Stopping --------------");
            // stop writing new items to the list by blocking writer threads
            _addingBlocker.Reset();
        }

        public void ResumeAdding()
        {
            _logger.WriteLine("----------- Resuming --------------");
            // resume writing new item to the list
            _addingBlocker.Set();
        }

        public void Dispose()
        {
            CompleteAdding();
            _list.Clear();
            _logger.WriteLine("Disposed.");
            _logger.Dispose();
        }
    }
}
