using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Klopoff.TrackableState.Core.Pools
{
    public static class ListPool<T>
    {
        private static readonly ConcurrentBag<List<T>> Pool = new();
        private static readonly Action<List<T>> ReturnAction = Release;

        private static int _count;

        public static int MaxPoolSize { get; set; } = 64;
        public static int MaxRetainedCount { get; set; } = 1024;
        
        public static List<T> Get()
        {
            if (Pool.TryTake(out var list))
            {
                Interlocked.Decrement(ref _count);

                if (list.Count != 0)
                {
                    list.Clear();
                }

                return list;
            }

            return new List<T>();
        }

        public static void Release(List<T> list)
        {
            if (list is null)
            {
                return;
            }

            int count = list.Count;
            list.Clear();
            
            if (count > MaxRetainedCount)
            {
                list.TrimExcess();
            }

            if (Volatile.Read(ref _count) >= MaxPoolSize)
            {
                return;
            }

            Pool.Add(list);
            Interlocked.Increment(ref _count);
        }
        
        public static Pooled<List<T>> GetPooled(out List<T> list)
        {
            list = Get();
            return new Pooled<List<T>>(list, ReturnAction);
        }
    }
}