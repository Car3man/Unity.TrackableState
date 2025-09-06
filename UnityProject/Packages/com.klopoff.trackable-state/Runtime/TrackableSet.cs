using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Klopoff.TrackableState
{
    public class TrackableSet<T> : ISet<T>, ITrackable
    {
        internal readonly ISet<T> Inner;
        
        private readonly Func<T, T> _wrapper;
        private readonly Func<T, T> _unwrapper;
        
        public bool IsDirty { get; private set; }
        public event EventHandler<ChangeEventArgs> Changed;
        
        public TrackableSet(ISet<T> inner, Func<T, T> wrapper, Func<T, T> unwrapper)
        {
            Inner = inner;
            
            _wrapper = wrapper ?? (x => x);
            _unwrapper = unwrapper ?? (x => x);

            HashSet<T> wrappedSet = HashSetPool<T>.Get();
            foreach (T item in Inner)
            {
                wrappedSet.Add(_wrapper(item));
            }
            Inner.Clear();
            foreach (T item in wrappedSet)
            {
                Inner.Add(item);
            }
            
            HookAll();
        }
        
        public ISet<T> Normalize()
        {
            HashSet<T> normalized = new HashSet<T>();
            
            foreach (T item in Inner)
            {
                normalized.Add(_unwrapper(item));
            }
            
            return normalized;
        }

        public void AcceptChanges()
        {
            foreach (T it in Inner)
            {
                if (it is ITrackable t)
                {
                    t.AcceptChanges();
                }
            }
            
            IsDirty = false;
        }

        private void HookAll()
        {
            foreach (T it in Inner)
            {
                Hook(it);
            }
        }

        private void Hook(T item)
        {
            if (item is ITrackable t)
            {
                t.Changed += ChildChanged;
            }
        }
        private void Unhook(T item)
        {
            if (item is ITrackable t)
            {
                t.Changed -= ChildChanged;
            }
        }

        private void ChildChanged(object sender, ChangeEventArgs e)
        {
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ChildOfSet(string.Empty, e));
        }

        #region ISet<T>
        
        public int Count => Inner.Count;
        
        public bool IsReadOnly => Inner.IsReadOnly;

        public bool Add(T item)
        {
            T wrappedValue = _wrapper(item);
            bool added = Inner.Add(wrappedValue);
            if (added)
            {
                Hook(wrappedValue);
                IsDirty = true;
                Changed?.Invoke(this, ChangeEventArgs.SetAdd(string.Empty, item));
            }
            return added;
        }

        void ICollection<T>.Add(T item) => Add(item!);
        
        public void ExceptWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            
            foreach (T it in other)
            {
                Remove(it);
            }
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            
            List<T> toKeep = ListPool<T>.Get();
            
            foreach (T item in other)
            {
                if (Contains(item))
                {
                    toKeep.Add(item);
                }
            }
            
            foreach (T item in Inner)
            {
                if (!toKeep.Contains(item))
                {
                    Remove(item);
                }
            }
            
            ListPool<T>.Release(toKeep);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) => Inner.IsProperSubsetOf(other);
        
        public bool IsProperSupersetOf(IEnumerable<T> other) => Inner.IsProperSupersetOf(other);
        
        public bool IsSubsetOf(IEnumerable<T> other) => Inner.IsSubsetOf(other);
        
        public bool IsSupersetOf(IEnumerable<T> other) => Inner.IsSupersetOf(other);
        
        public bool Overlaps(IEnumerable<T> other) => Inner.Overlaps(other);
        
        public bool SetEquals(IEnumerable<T> other) => Inner.SetEquals(other);

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            HashSet<T> otherSet = HashSetPool<T>.Get();
            otherSet.UnionWith(other);

            foreach (T item in otherSet)
            {
                if (!Remove(item))
                {
                    Add(item);
                }
            }
            
            HashSetPool<T>.Release(otherSet);
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T it in other)
            {
                Add(it);
            }
        }
        
        public void Clear()
        {
            if (Inner.Count > 0)
            {
                IsDirty = true;
            }
            
            foreach (T it in Inner)
            {
                Unhook(it);
            }

            Inner.Clear();
            Changed?.Invoke(this, ChangeEventArgs.SetClear(string.Empty));
        }

        public bool Contains(T item) => Inner.Contains(item);
        
        public void CopyTo(T[] array, int arrayIndex) => Inner.CopyTo(array, arrayIndex);

        public bool Remove(T item)
        {
            if (Inner.Remove(item))
            {
                Unhook(item);
                IsDirty = true;
                Changed?.Invoke(this, ChangeEventArgs.SetRemove(string.Empty, item));
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator() => Inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Inner).GetEnumerator();
        
        #endregion
    }
    
    public static class TrackableSetExtensions
    {
        public static TrackableSet<T> AsTrackable<T>(this ISet<T> source, Func<T, T> wrapper, Func<T, T> unwrapper)
        {
            if (source is TrackableSet<T> t)
            {
                return t;
            }
            return new TrackableSet<T>(source, wrapper, unwrapper);
        }

        public static ISet<T> AsNormal<T>(this ISet<T> source)
        {
            if (source is TrackableSet<T> t)
            {
                return t.Normalize();
            }
            return source;
        }
    }
}