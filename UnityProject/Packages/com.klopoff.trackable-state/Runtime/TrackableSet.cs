using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Klopoff.TrackableState
{
    public class TrackableSet<T> : ISet<T>, ITrackable
    {
        private readonly ISet<T> _inner;
        private readonly Func<T, T> _wrapper;
        
        public bool IsDirty { get; private set; }
        public event EventHandler<ChangeEventArgs> Changed;
        
        public TrackableSet(ISet<T> inner, Func<T, T> wrapper)
        {
            _inner = inner;
            _wrapper = wrapper ?? (x => x);

            HashSet<T> wrappedSet = HashSetPool<T>.Get();
            foreach (T item in _inner)
            {
                wrappedSet.Add(_wrapper(item));
            }
            _inner.Clear();
            foreach (T item in wrappedSet)
            {
                _inner.Add(item);
            }
            
            HookAll();
        }

        public void AcceptChanges()
        {
            foreach (T it in _inner)
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
            foreach (T it in _inner)
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
        
        public int Count => _inner.Count;
        
        public bool IsReadOnly => _inner.IsReadOnly;

        public bool Add(T item)
        {
            T wrappedValue = _wrapper(item);
            bool added = _inner.Add(wrappedValue);
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
            
            foreach (T item in _inner)
            {
                if (!toKeep.Contains(item))
                {
                    Remove(item);
                }
            }
            
            ListPool<T>.Release(toKeep);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) => _inner.IsProperSubsetOf(other);
        
        public bool IsProperSupersetOf(IEnumerable<T> other) => _inner.IsProperSupersetOf(other);
        
        public bool IsSubsetOf(IEnumerable<T> other) => _inner.IsSubsetOf(other);
        
        public bool IsSupersetOf(IEnumerable<T> other) => _inner.IsSupersetOf(other);
        
        public bool Overlaps(IEnumerable<T> other) => _inner.Overlaps(other);
        
        public bool SetEquals(IEnumerable<T> other) => _inner.SetEquals(other);

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
            if (_inner.Count > 0)
            {
                IsDirty = true;
            }
            
            foreach (T it in _inner)
            {
                Unhook(it);
            }

            _inner.Clear();
            Changed?.Invoke(this, ChangeEventArgs.SetClear(string.Empty));
        }

        public bool Contains(T item) => _inner.Contains(item);
        
        public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

        public bool Remove(T item)
        {
            if (_inner.Remove(item))
            {
                Unhook(item);
                IsDirty = true;
                Changed?.Invoke(this, ChangeEventArgs.SetRemove(string.Empty, item));
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        
        #endregion
    }
    
    public static class TrackableSetExtensions
    {
        public static TrackableSet<T> AsTrackable<T>(this ISet<T> source, Func<T, T> wrapper)
        {
            if (source is TrackableSet<T> t)
            {
                return t;
            }
            return new TrackableSet<T>(source, wrapper);
        }
    }
}