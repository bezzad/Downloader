using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Downloader
{
    /// <summary>
    /// Represents a thread-safe, ordered collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the ConcurrentDictionary.</typeparam>
    /// <remarks>
    /// <para>
    /// SortedList are useful for storing objects when ordering does matter, and like sets not support
    /// duplicates. <see cref="ConcurrentSortedList{T}"/> is a thread-safe ConcurrentDictionary implementation, optimized for
    /// scenarios where the same thread will be both producing and consuming data stored in the ConcurrentDictionary.
    /// </para>
    /// <para>
    /// <see cref="ConcurrentSortedList{T}"/> accepts null reference (Nothing in Visual Basic) as a valid
    /// value for reference types.
    /// </para>
    /// <para>
    /// All public and protected members of <see cref="ConcurrentSortedList{T}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </para>
    /// </remarks>
    [DebuggerTypeProxy(typeof(IProducerConsumerCollection<>))]
    [DebuggerDisplay("Count = {Count}")]
    public class ConcurrentSortedList<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T> where T : IComparable, IIndexable
    {
        protected readonly ConcurrentDictionary<long, T> _list;
        protected ReaderWriterLockSlim _lock;
        protected readonly Comparison<T> _comparison;


        public ConcurrentSortedList()
        {
            _list = new ConcurrentDictionary<long, T>();
            _comparison = (p1, p2) => p1.CompareTo(p2);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection)_list.Values).CopyTo(array, index);
        }

        public int Count => _list?.Count ?? 0;

        public bool IsSynchronized => true;

        public object SyncRoot => _lock;

        public bool IsAddingCompleted => _lock.IsWriteLockHeld;

        public bool IsCompleted => _list.Count == 0;

        public void CopyTo(T[] array, int index)
        {
            _list.Values.CopyTo(array, index);
        }

        public T[] ToArray()
        {
            return _list.Values.ToArray();
        }

        public bool TryAdd(T item)
        {
            _list.TryAdd(item.Position, item);
            return true;
        }

        public bool TryTake(out T item)
        {
            try
            {
                _lock.EnterReadLock();
                var firstItem = _list.Keys.Min();
                return _list.TryRemove(firstItem, out item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CompleteAdding()
        {
            _lock.EnterWriteLock();
        }

        public void ResumeAdding()
        {
            _lock.ExitWriteLock();
        }
    }
}
