using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Klopoff.TrackableState
{
    public class TrackableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ITrackable
    {
        internal readonly IDictionary<TKey, TValue> Inner;
        
        private readonly Func<TValue, TValue> _wrapper;
        private readonly Func<TValue, TValue> _unwrapper;
        private readonly Dictionary<ITrackable, HashSet<TKey>> _valueToKeys;
        
        public bool IsDirty { get; private set; }
        public event EventHandler<ChangeEventArgs> Changed;
        
        public TrackableDictionary(IDictionary<TKey, TValue> inner, Func<TValue, TValue> wrapper, Func<TValue, TValue> unwrapper)
        {
            Inner = inner;
            
            _wrapper = wrapper ?? (x => x);
            _unwrapper = unwrapper ?? (x => x);
            _valueToKeys = new Dictionary<ITrackable, HashSet<TKey>>();

            List<TKey> keys = ListPool<TKey>.Get();
            foreach (TKey key in inner.Keys)
            {
                keys.Add(key);
            }
            foreach (TKey key in keys)
            {
                Inner[key] = _wrapper(Inner[key]);
            }
            
            HookAll();
        }
        
        public IDictionary<TKey, TValue> Normalize()
        {
            Dictionary<TKey, TValue> normalized = new Dictionary<TKey, TValue>(Inner.Count);
            
            foreach (KeyValuePair<TKey, TValue> kv in Inner)
            {
                normalized[kv.Key] = _unwrapper(kv.Value);
            }
            
            return normalized;
        }

        public void AcceptChanges()
        {
            foreach (KeyValuePair<TKey, TValue> kv in Inner)
            {
                if (kv.Value is ITrackable t)
                {
                    t.AcceptChanges();
                }
            }
            
            IsDirty = false;
        }

        private void HookAll()
        {
            foreach (KeyValuePair<TKey, TValue> kv in Inner)
            {
                Hook(kv.Key, kv.Value);
            }
        }

        private void Hook(TKey key, TValue value)
        {
            if (value is ITrackable t)
            {
                if (!_valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
                {
                    keys = new HashSet<TKey>();
                    _valueToKeys[t] = keys;
                    t.Changed += ChildChanged;
                }
                
                keys.Add(key);
            }
        }

        private void Unhook(TKey key, TValue value)
        {
            if (value is ITrackable t && _valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
            {
                keys.Remove(key);
                
                if (keys.Count == 0)
                {
                    _valueToKeys.Remove(t);
                    t.Changed -= ChildChanged;
                }
            }
        }

        private void ChildChanged(object sender, ChangeEventArgs e)
        {
            IsDirty = true;
            
            if (sender is ITrackable t && _valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
            {
                foreach (TKey key in keys)
                {
                    Changed?.Invoke(this, ChangeEventArgs.ChildOfDict(string.Empty, key, e));
                }
            }
        }

        #region IDictionary<TKey,TValue>
        
        public TValue this[TKey key]
        {
            get => Inner[key];
            set
            {
                TValue wrappedValue = _wrapper(value);
                bool had = Inner.TryGetValue(key, out TValue old);
                if (!had || !EqualityComparer<TValue>.Default.Equals(old!, wrappedValue))
                {
                    if (had)
                    {
                        Unhook(key, old);
                    }
                    
                    Inner[key] = wrappedValue;
                    Hook(key, wrappedValue);

                    IsDirty = true;
                    Changed?.Invoke(this, had
                        ? ChangeEventArgs.DictReplace(string.Empty, key, old, value)
                        : ChangeEventArgs.DictAdd(string.Empty, key, value)
                    );
                }
            }
        }

        public ICollection<TKey> Keys => Inner.Keys;
        
        public ICollection<TValue> Values => Inner.Values;
        
        public int Count => Inner.Count;
        
        public bool IsReadOnly => Inner.IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            TValue wrappedValue = _wrapper(value);
            Inner.Add(key, wrappedValue);
            Hook(key, wrappedValue);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.DictAdd(string.Empty, key, value));
        }

        public bool ContainsKey(TKey key) => Inner.ContainsKey(key);

        public bool Remove(TKey key)
        {
            if (Inner.TryGetValue(key, out TValue value))
            {
                Unhook(key, value);
                bool ok = Inner.Remove(key);
                if (ok)
                {
                    IsDirty = true;
                    Changed?.Invoke(this, ChangeEventArgs.DictRemove(string.Empty, key, value));
                }
                return ok;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) => Inner.TryGetValue(key, out value!);

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        
        public void Clear()
        {
            if (Inner.Count > 0)
            {
                IsDirty = true;
            }
            
            foreach (KeyValuePair<TKey, TValue> kv in Inner)
            {
                Unhook(kv.Key, kv.Value);
            }
            
            Inner.Clear();
            Changed?.Invoke(this, ChangeEventArgs.DictClear(string.Empty));
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => Inner.Contains(item);
        
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => Inner.CopyTo(array, arrayIndex);
        
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Inner).GetEnumerator();
        
        #endregion
    }
    
    public static class TrackableDictionaryExtensions
    {
        public static TrackableDictionary<TKey, TValue> AsTrackable<TKey, TValue>(this IDictionary<TKey, TValue> source,
            Func<TValue, TValue> wrapper, Func<TValue, TValue> unwrapper)
        {
            if (source is TrackableDictionary<TKey, TValue> t)
            {
                return t;
            }
            return new TrackableDictionary<TKey, TValue>(source, wrapper, unwrapper);
        }
        
        public static IDictionary<TKey, TValue> AsNormal<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            if (source is TrackableDictionary<TKey, TValue> t)
            {
                return t.Normalize();
            }
            return source;
        }
    }
}