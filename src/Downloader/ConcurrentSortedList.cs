using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Downloader
{
    /// <summary>
    /// Represents a thread-safe, ordered collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the bag.</typeparam>
    /// <remarks>
    /// <para>
    /// SortedList are useful for storing objects when ordering does matter, and like sets not support
    /// duplicates. <see cref="ConcurrentSortedList{T}"/> is a thread-safe bag implementation, optimized for
    /// scenarios where the same thread will be both producing and consuming data stored in the bag.
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
    public class ConcurrentSortedList<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        private readonly ConcurrentBag<T> _queue;
        protected ReaderWriterLockSlim _lock;

        public ConcurrentSortedList()
        {
            _queue = new ConcurrentBag<T>();
        }

        public IEnumerator<T> GetEnumerator()
        {
            try
            {
                _lock.EnterWriteLock();
                return new ConcurrentBag<T>(_queue).GetEnumerator();
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
            ((ICollection)_queue).CopyTo(array, index);
            _lock.ExitWriteLock();
        }

        public int Count => _queue?.Count ?? 0;

        public bool IsSynchronized => true;

        public object SyncRoot => _lock;

        public bool IsAddingCompleted => _lock.IsWriteLockHeld;

        public bool IsCompleted => _queue.IsEmpty;

        public void CopyTo(T[] array, int index)
        {
            _lock.EnterWriteLock();
            _queue.CopyTo(array, index);
            _lock.ExitWriteLock();
        }

        public T[] ToArray()
        {
            try
            {
                _lock.EnterWriteLock();
                return _queue.ToArray();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryAdd(T item)
        {
            _lock.EnterWriteLock();
            _queue.Add(item);
            _lock.ExitWriteLock();

            return true;
        }

        public bool TryTake(out T item)
        {
            try
            {
                _lock.EnterReadLock();
                return _queue.TryTake(out item);
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
