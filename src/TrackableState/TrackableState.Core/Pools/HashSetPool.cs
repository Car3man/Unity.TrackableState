using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Klopoff.TrackableState.Core.Pools
{
    public static class HashSetPool<T>
    {
        private static readonly ConcurrentBag<HashSet<T>> Pool = new();
        private static readonly Action<HashSet<T>> ReturnAction = Release;
        
        private static int _count;

        public static int MaxPoolSize { get; set; } = 64;
        public static int MaxRetainedCount { get; set; } = 1024;

        public static HashSet<T> Get(IEqualityComparer<T> comparer = null)
        {
            if (Pool.TryTake(out var set))
            {
                Interlocked.Decrement(ref _count);
                
                if (set.Count != 0)
                {
                    set.Clear();
                }

                if (comparer != null && !ReferenceEquals(set.Comparer, comparer))
                {
                    Release(set);
                    return new HashSet<T>(comparer);
                }

                return set;
            }

            return comparer is null ? new HashSet<T>() : new HashSet<T>(comparer);
        }

        public static void Release(HashSet<T> set)
        {
            if (set is null)
            {
                return;
            }

            int count = set.Count;
            set.Clear();
            
            if (count > MaxRetainedCount)
            {
                set.TrimExcess();
            }

            if (Volatile.Read(ref _count) >= MaxPoolSize)
            {
                return;
            }

            Pool.Add(set);
            Interlocked.Increment(ref _count);
        }
        
        public static Pooled<HashSet<T>> GetPooled(out HashSet<T> set, IEqualityComparer<T> comparer = null)
        {
            set = Get(comparer);
            return new Pooled<HashSet<T>>(set, ReturnAction);
        }
    }
}