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
    /// <typeparam name="T">Specifies the type of elements in the HashSet.</typeparam>
    /// <remarks>
    /// <para>
    /// SortedList are useful for storing objects when ordering does matter, and like sets not support
    /// duplicates. <see cref="ConcurrentSortedList{T}"/> is a thread-safe HashSet implementation, optimized for
    /// scenarios where the same thread will be both producing and consuming data stored in the HashSet.
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
    public class ConcurrentSortedList<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T> where T : IComparable
    {
        protected readonly List<T> _list;
        protected ReaderWriterLockSlim _lock;
        protected readonly Comparison<T> _comparison;


        public ConcurrentSortedList()
        {
            _list = new List<T>();
            _comparison = (p1, p2) => p1.CompareTo(p2);
        }

        public IEnumerator<T> GetEnumerator()
        {
            try
            {
                _lock.EnterWriteLock();
                return _list.GetEnumerator();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            _lock.EnterWriteLock();
            ((ICollection)_list).CopyTo(array, index);
            _lock.ExitWriteLock();
        }

        public int Count => _list?.Count ?? 0;

        public bool IsSynchronized => true;

        public object SyncRoot => _lock;

        public bool IsAddingCompleted => _lock.IsWriteLockHeld;

        public bool IsCompleted => _list.Count == 0;

        public void CopyTo(T[] array, int index)
        {
            _lock.EnterWriteLock();
            _list.CopyTo(array, index);
            _lock.ExitWriteLock();
        }

        public T[] ToArray()
        {
            try
            {
                _lock.EnterWriteLock();
                return _list.ToArray<T>();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryAdd(T item)
        {
            _lock.EnterWriteLock();
            int index = _list.BinarySearch(item);
            if (index < 0)
                index = ~index;
            _list.Insert(index, item);
            _lock.ExitWriteLock();

            return true;
        }

        public bool TryTake(out T item)
        {
            try
            {
                _lock.EnterReadLock();
                _lock.EnterWriteLock();
                item = _list.FirstOrDefault();
                if (item != null)
                {
                    _list.RemoveAt(0);
                }
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
                _lock.ExitWriteLock();
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
