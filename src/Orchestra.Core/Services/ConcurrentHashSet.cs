using System;
using System.Collections.Generic;
using System.Threading;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Thread-safe HashSet implementation
    /// </summary>
    internal class ConcurrentHashSet<T> : IDisposable
    {
        private readonly HashSet<T> _hashSet = new HashSet<T>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private bool _disposed;

        public bool Add(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

            _lock.EnterWriteLock();
            try
            {
                return _hashSet.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryRemove(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

            _lock.EnterWriteLock();
            try
            {
                return _hashSet.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

            _lock.EnterReadLock();
            try
            {
                return _hashSet.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<T> ToList()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

            _lock.EnterReadLock();
            try
            {
                return new List<T>(_hashSet);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Clear()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

            _lock.EnterWriteLock();
            try
            {
                _hashSet.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ConcurrentHashSet<T>));

                _lock.EnterReadLock();
                try
                {
                    return _hashSet.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _lock?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}