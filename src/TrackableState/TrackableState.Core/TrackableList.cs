using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Klopoff.TrackableState.Core
{
    public sealed class TrackableList<T> : IList<T>, ITrackable
    {
        private readonly IList<T> _inner;
        private readonly Func<T, T> _wrapper;
        private readonly Func<T, T> _unwrapper;

        public event ChangeEventHandler Changed;
        public bool IsDirty { get; private set; }
        
        public TrackableList(IList<T> inner, Func<T, T> wrapper, Func<T, T> unwrapper)
        {
            _inner = inner;
            _wrapper = wrapper ?? (x => x);
            _unwrapper = unwrapper ?? (x => x);

            for (int i = 0; i < inner.Count; i++)
            {
                _inner[i] = _wrapper(inner[i]);
            }

            HookAll();
        }

        public IList<T> Normalize()
        {
            IList<T> normalized = new List<T>();
            
            foreach (T item in _inner)
            {
                normalized.Add(_unwrapper(item));
            }
            
            return normalized;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HookAll()
        {
            foreach (T item in _inner)
            {
                Hook(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Hook(T item)
        {
            if (item is ITrackable t)
            {
                t.Changed += OnChange;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unhook(T item)
        {
            if (item is ITrackable t)
            {
                t.Changed -= OnChange;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnChange(object sender, in ChangeEventArgs args)
        {
            IsDirty = true;
            
            int index = -1;
            if (sender is T sAsT)
            {
                index = IndexOfReference(sAsT);
            }
            
            Changed?.Invoke(this, ChangeEventArgs.ChildOfList(args, index));
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
                T newItem = _wrapper(value);
                T oldItem = _inner[index];
                if (!EqualityComparer<T>.Default.Equals(oldItem, newItem))
                {
                    Unhook(oldItem);
                    _inner[index] = newItem;
                    Hook(newItem);
                    IsDirty = true;
                    Changed?.Invoke(this, ChangeEventArgs.ListReplace(oldValue: Payload24.From(oldItem), newValue: Payload24.From(newItem), index));
                }
            }
        }

        public int Count => _inner.Count;
        
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(T item)
        {
            int index = _inner.Count;
            T newItem = _wrapper(item);
            _inner.Add(newItem);
            Hook(newItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(newValue: Payload24.From(newItem), index));
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
            Changed?.Invoke(this, ChangeEventArgs.ListClear());
        }
        
        public bool Contains(T item) => _inner.Contains(item);
        
        public int IndexOf(T item) => _inner.IndexOf(item);

        public void Insert(int index, T item)
        {
            T newItem = _wrapper(item);
            _inner.Insert(index, newItem);
            Hook(newItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListAdd(newValue: Payload24.From(newItem), index));
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
            T oldItem = _inner[index];
            Unhook(oldItem);
            _inner.RemoveAt(index);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.ListRemove(oldValue: Payload24.From(oldItem), index));
        }
        
        public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        
        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        
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