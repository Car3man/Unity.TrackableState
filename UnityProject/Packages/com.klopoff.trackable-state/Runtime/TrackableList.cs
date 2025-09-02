using System;
using System.Collections;
using System.Collections.Generic;

namespace Klopoff.TrackableState
{
    public class TrackableList<T> : IList<T>, ITrackable
    {
        private readonly IList<T> _inner;
        private readonly Func<T, T> _wrapper;
        
        public bool IsDirty { get; private set; }
        public event EventHandler<ChangeEventArgs> Changed;
        
        public TrackableList(IList<T> inner, Func<T, T> wrapper)
        {
            _inner = inner;
            _wrapper = wrapper ?? (x => x);

            for (int i = 0; i < inner.Count; i++)
            {
                _inner[i] = _wrapper(inner[i]);
            }

            HookAll();
        }

        public void AcceptChanges()
        {
            foreach (T item in _inner)
            {
                if (item is ITrackable t)
                {
                    t.AcceptChanges();
                }
            }
            
            IsDirty = false;
        }

        private void HookAll()
        {
            foreach (T item in _inner)
            {
                Hook(item);
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
            
            int idx = -1;
            if (sender is T sAsT)
            {
                idx = IndexOfReference(sAsT);
            }
            
            Changed?.Invoke(this, ChangeEventArgs.ChildOfList(string.Empty, idx, e));
        }
        
        private int IndexOfReference(T item)
        {
            EqualityComparer<T> cmp = EqualityComparer<T>.Default;
            for (int i = 0; i < _inner.Count; i++)
            {
                if (ReferenceEquals(_inner[i], item) || cmp.Equals(_inner[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        #region IList<T>
        
        public T this[int index]
        {
            get => _inner[index];
            set
            {
                T wrappedItem = _wrapper(value);
                T oldItem = _inner[index];
                if (!EqualityComparer<T>.Default.Equals(oldItem, wrappedItem))
                {
                    Unhook(oldItem);
                    _inner[index] = wrappedItem;
                    Hook(wrappedItem);
                    IsDirty = true;
                    Changed?.Invoke(this, ChangeEventArgs.ListReplace(string.Empty, index, oldItem, value));
                }
            }
        }

        public int Count => _inner.Count;
        
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(T item)
        {
            int idx = _inner.Count;
            T wrappedItem = _wrapper(item);
            _inner.Add(wrappedItem);
            Hook(wrappedItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(string.Empty, idx, item));
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
            Changed?.Invoke(this, ChangeEventArgs.ListClear(string.Empty));
        }
        
        public bool Contains(T item) => _inner.Contains(item);
        
        public int IndexOf(T item) => _inner.IndexOf(item);

        public void Insert(int index, T item)
        {
            T wrappedItem = _wrapper(item);
            _inner.Insert(index, wrappedItem);
            Hook(wrappedItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(string.Empty, index, item));
        }

        public bool Remove(T item)
        {
            int idx = _inner.IndexOf(item);
            if (idx < 0)
            {
                return false;
            }
            
            RemoveAt(idx);
            return true;
        }

        public void RemoveAt(int index)
        {
            T removed = _inner[index];
            Unhook(removed);
            _inner.RemoveAt(index);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListRemove(string.Empty, index, removed));
        }
        
        public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        
        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        
        #endregion
    }
    
    public static class TrackableListExtensions
    {
        public static TrackableList<T> AsTrackable<T>(this IList<T> source, Func<T, T> wrapper)
        {
            if (source is TrackableList<T> t)
            {
                return t;
            }
            return new TrackableList<T>(source, wrapper);
        }
    }
}