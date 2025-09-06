using System;
using System.Collections;
using System.Collections.Generic;

namespace Klopoff.TrackableState.Core
{
    public class TrackableList<T> : IList<T>, ITrackable
    {
        internal readonly IList<T> Inner;
        
        private readonly Func<T, T> _wrapper;
        private readonly Func<T, T> _unwrapper;
        
        public bool IsDirty { get; private set; }
        public event EventHandler<ChangeEventArgs> Changed;
        
        public TrackableList(IList<T> inner, Func<T, T> wrapper, Func<T, T> unwrapper)
        {
            Inner = inner;
            
            _wrapper = wrapper ?? (x => x);
            _unwrapper = unwrapper ?? (x => x);

            for (int i = 0; i < inner.Count; i++)
            {
                Inner[i] = _wrapper(inner[i]);
            }

            HookAll();
        }

        public IList<T> Normalize()
        {
            IList<T> normalized = new List<T>();
            
            foreach (T item in Inner)
            {
                normalized.Add(_unwrapper(item));
            }
            
            return normalized;
        }

        public void AcceptChanges()
        {
            foreach (T item in Inner)
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
            foreach (T item in Inner)
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
            for (int i = 0; i < Inner.Count; i++)
            {
                if (ReferenceEquals(Inner[i], item) || cmp.Equals(Inner[i], item))
                {
                    return i;
                }
            }
            return -1;
        }
        
        #region IList<T>
        
        public T this[int index]
        {
            get => Inner[index];
            set
            {
                T wrappedItem = _wrapper(value);
                T oldItem = Inner[index];
                if (!EqualityComparer<T>.Default.Equals(oldItem, wrappedItem))
                {
                    Unhook(oldItem);
                    Inner[index] = wrappedItem;
                    Hook(wrappedItem);
                    IsDirty = true;
                    Changed?.Invoke(this, ChangeEventArgs.ListReplace(string.Empty, index, oldItem, value));
                }
            }
        }

        public int Count => Inner.Count;
        
        public bool IsReadOnly => Inner.IsReadOnly;

        public void Add(T item)
        {
            int idx = Inner.Count;
            T wrappedItem = _wrapper(item);
            Inner.Add(wrappedItem);
            Hook(wrappedItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(string.Empty, idx, item));
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
            Changed?.Invoke(this, ChangeEventArgs.ListClear(string.Empty));
        }
        
        public bool Contains(T item) => Inner.Contains(item);
        
        public int IndexOf(T item) => Inner.IndexOf(item);

        public void Insert(int index, T item)
        {
            T wrappedItem = _wrapper(item);
            Inner.Insert(index, wrappedItem);
            Hook(wrappedItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(string.Empty, index, item));
        }

        public bool Remove(T item)
        {
            int idx = Inner.IndexOf(item);
            if (idx < 0)
            {
                return false;
            }
            
            RemoveAt(idx);
            return true;
        }

        public void RemoveAt(int index)
        {
            T removed = Inner[index];
            Unhook(removed);
            Inner.RemoveAt(index);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListRemove(string.Empty, index, removed));
        }
        
        public void CopyTo(T[] array, int arrayIndex) => Inner.CopyTo(array, arrayIndex);
        
        public IEnumerator<T> GetEnumerator() => Inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Inner).GetEnumerator();
        
        #endregion
    }
    
    public static class TrackableListExtensions
    {
        public static TrackableList<T> AsTrackable<T>(this IList<T> source, Func<T, T> wrapper, Func<T, T> unwrapper)
        {
            if (source is TrackableList<T> t)
            {
                return t;
            }
            return new TrackableList<T>(source, wrapper, unwrapper);
        }
        
        public static IList<T> AsNormal<T>(this IList<T> source)
        {
            if (source is TrackableList<T> t)
            {
                return t.Normalize();
            }
            return source;
        }
    }
}