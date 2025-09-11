using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Klopoff.TrackableState.Core.Pools;

namespace Klopoff.TrackableState.Core
{
    public sealed class TrackableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ITrackable
    {
        private readonly IDictionary<TKey, TValue> _inner;
        private readonly Func<TValue, TValue> _wrapper;
        private readonly Func<TValue, TValue> _unwrapper;
        private readonly Dictionary<ITrackable, HashSet<TKey>> _valueToKeys;
        
        public event ChangeEventHandler Changed;
        public bool IsDirty { get; private set; }
        
        public TrackableDictionary(IDictionary<TKey, TValue> inner, Func<TValue, TValue> wrapper, Func<TValue, TValue> unwrapper)
        {
            _inner = inner;
            _wrapper = wrapper ?? (x => x);
            _unwrapper = unwrapper ?? (x => x);
            _valueToKeys = new Dictionary<ITrackable, HashSet<TKey>>();
            
            using var pooled = ListPool<TKey>.GetPooled(out var list);
            
            foreach (TKey key in inner.Keys)
            {
                list.Add(key);
            }
            
            foreach (TKey key in list)
            {
                _inner[key] = _wrapper(_inner[key]);
            }
            
            HookAll();
        }
        
        public IDictionary<TKey, TValue> Normalize()
        {
            Dictionary<TKey, TValue> normalized = new Dictionary<TKey, TValue>(_inner.Count);
            
            foreach (KeyValuePair<TKey, TValue> kv in _inner)
            {
                normalized[kv.Key] = _unwrapper(kv.Value);
            }
            
            return normalized;
        }

        public void AcceptChanges()
        {
            foreach (KeyValuePair<TKey, TValue> kv in _inner)
            {
                if (kv.Value is ITrackable t)
                {
                    t.AcceptChanges();
                }
            }
            
            IsDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HookAll()
        {
            foreach (KeyValuePair<TKey, TValue> kv in _inner)
            {
                Hook(kv.Key, kv.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Hook(TKey key, TValue value)
        {
            if (value is ITrackable t)
            {
                if (!_valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
                {
                    keys = new HashSet<TKey>();
                    _valueToKeys[t] = keys;
                    t.Changed += OnChange;
                }
                
                keys.Add(key);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unhook(TKey key, TValue value)
        {
            if (value is ITrackable t && _valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
            {
                keys.Remove(key);
                
                if (keys.Count == 0)
                {
                    _valueToKeys.Remove(t);
                    t.Changed -= OnChange;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnChange(object sender, in ChangeEventArgs args)
        {
            IsDirty = true;
            
            if (sender is ITrackable t && _valueToKeys.TryGetValue(t, out HashSet<TKey> keys))
            {
                foreach (TKey key in keys)
                {
                    Changed?.Invoke(this, ChangeEventArgs.ChildOfDictionary(args, Payload24.From(key)));
                }
            }
        }
        
        #region IDictionary<TKey,TValue>
        
        public TValue this[TKey key]
        {
            get => _inner[key];
            set
            {
                TValue newItem = _wrapper(value);
                bool had = _inner.TryGetValue(key, out TValue oldItem);
                if (!had || !EqualityComparer<TValue>.Default.Equals(oldItem!, newItem))
                {
                    if (had)
                    {
                        Unhook(key, oldItem);
                    }
                    
                    _inner[key] = newItem;
                    Hook(key, newItem);

                    IsDirty = true;
                    Changed?.Invoke(this, had 
                        ? ChangeEventArgs.DictionaryReplace(oldValue: Payload24.From(oldItem), newValue: Payload24.From(newItem), key: Payload24.From(key)) 
                        : ChangeEventArgs.DictionaryAdd(newValue: Payload24.From(newItem), key: Payload24.From(key)));
                }
            }
        }

        public ICollection<TKey> Keys => _inner.Keys;
        
        public ICollection<TValue> Values => _inner.Values;
        
        public int Count => _inner.Count;
        
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            TValue newItem = _wrapper(value);
            _inner.Add(key, newItem);
            Hook(key, newItem);
            IsDirty = true;
            Changed?.Invoke(this, ChangeEventArgs.DictionaryAdd(newValue: Payload24.From(newItem), key: Payload24.From(key)));
        }

        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

        public bool Remove(TKey key)
        {
            if (_inner.TryGetValue(key, out TValue oldItem))
            {
                Unhook(key, oldItem);
                bool ok = _inner.Remove(key);
                if (ok)
                {
                    IsDirty = true;
                    Changed?.Invoke(this, ChangeEventArgs.DictionaryRemove(oldValue: Payload24.From(oldItem), key: Payload24.From(key)));
                }
                return ok;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value!);

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        
        public void Clear()
        {
            if (_inner.Count > 0)
            {
                IsDirty = true;
            }
            
            foreach (KeyValuePair<TKey, TValue> kv in _inner)
            {
                Unhook(kv.Key, kv.Value);
            }
            
            _inner.Clear();
            Changed?.Invoke(this, ChangeEventArgs.DictionaryClear());
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => _inner.Contains(item);
        
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        
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