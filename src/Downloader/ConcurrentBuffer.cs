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
    public class ConcurrentBuffer<T> : IReadOnlyCollection<T> where T : IComparable<T>, IIndexable
    {
        private readonly ManualResetEventSlim _addingBlocker = new ManualResetEventSlim(true);
        protected readonly SemaphoreSlim _singleConsumerLock = new SemaphoreSlim(1);
        protected long _minPosition = long.MaxValue;
        protected readonly ConcurrentDictionary<long, T> _list;

        public ConcurrentBuffer()
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
            _list.TryAdd(item.Position, item);
            Interlocked.CompareExchange(ref _minPosition, item.Position, Math.Min(_minPosition, item.Position));

            return true;
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
                    _minPosition = _list.Keys.Min();
                    return Pop();
                }

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
                Interlocked.Exchange(ref _minPosition, item.NextPosition);
                return item;
            }

            return default;
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
    }
}
