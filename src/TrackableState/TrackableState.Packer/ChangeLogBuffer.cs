using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Klopoff.TrackableState.Core;
using Klopoff.TrackableState.Core.Pools;

namespace TrackableState.Packer
{
    public class ChangeLogBuffer
    {
        private readonly List<ChangeEventArgs> _buffer;
        private readonly PathKeyComparer _comparer;
        private readonly int _autoPackInterval;

        private int _autoPackCounter;

        public IReadOnlyList<ChangeEventArgs> Snapshot => _buffer;
        public int Count => _buffer.Count;
        
        public const int DefaultCoalesceAddScanLimit = 64;

        public ChangeLogBuffer(int capacity = 1024, int autoPackInterval = 256)
        {
            _buffer = new List<ChangeEventArgs>(capacity);
            _comparer = new PathKeyComparer();
            _autoPackInterval = autoPackInterval;
            _autoPackCounter = 0;
        }
        
        public void Add(in ChangeEventArgs e)
        {
            _buffer.Add(e);
            _autoPackCounter++;
            
            if (_autoPackInterval > 0 && _autoPackCounter >= _autoPackInterval)
            {
                Pack();
                _autoPackCounter = 0;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddCoalescing(in ChangeEventArgs e, int scanLimit = DefaultCoalesceAddScanLimit)
        {
            int count = _buffer.Count;
            if (count == 0 || scanLimit <= 0)
            {
                Add(e);
                return false;
            }

            PathKey newKey = new PathKey(e.path);
            int startIndex = Math.Max(0, count - scanLimit);

            for (int i = count - 1; i >= startIndex; i--)
            {
                ChangeEventArgs existing = _buffer[i];
                PathKey existingKey = new PathKey(existing.path);
                
                if (IsAncestorOrDescendantPaths(newKey.Path, existingKey.Path) && !_comparer.Equals(newKey, existingKey))
                {
                    break;
                }
                
                if (!_comparer.Equals(newKey, existingKey))
                {
                    continue;
                }

                int right = i;
                int left = i;

                for (int j = right - 1; j > 0; j--)
                {
                    ChangeEventArgs prev = _buffer[j];
                    PathKey prevKey = new PathKey(prev.path);
                    
                    if (!_comparer.Equals(newKey, prevKey))
                    {
                        break;
                    }

                    left = j;
                }
                
                _buffer[left] = Merge(_buffer[left], e);
                
                if (right > left)
                {
                    _buffer.RemoveRange(left + 1, right - left);
                }
                return true;
            }
            
            Add(e);
            return false;
        }
        
        public void Pack()
        {
            int n = _buffer.Count;
            if (n <= 1)
            {
                return;
            }

            Span<bool> skip = stackalloc bool[n];
            
            using var openPooled = DictionaryPool<PathKey, MergeSession>.GetPooled(out Dictionary<PathKey, MergeSession> open, _comparer);
            using var mergedAtIndexPooled = DictionaryPool<int, ChangeEventArgs>.GetPooled(out Dictionary<int, ChangeEventArgs> mergedAtIndex);
            using var toClosePooled = ListPool<PathKey>.GetPooled(out List<PathKey> toClose);
            
            for (int i = 0; i < n; i++)
            {
                ChangeEventArgs current = _buffer[i];
                PathKey currentKey = new PathKey(current.path);
                
                if (open.Count > 0)
                {
                    foreach (KeyValuePair<PathKey, MergeSession> kv in open)
                    {
                        PathKey sessionKey = kv.Key;
                        
                        if (IsAncestorOrDescendantPaths(sessionKey.Path, currentKey.Path) && !_comparer.Equals(sessionKey, currentKey))
                        {
                            toClose.Add(sessionKey);
                        }
                    }
                    
                    for (int t = 0; t < toClose.Count; t++)
                    {
                        PathKey closeKey = toClose[t];
                        MergeSession session = open[closeKey];
                        
                        if (session.Count > 1)
                        {
                            mergedAtIndex[session.FirstIndex] = Merge(session.FirstEvent, session.LastEvent);
                        }
                        
                        open.Remove(closeKey);
                    }
                    
                    toClose.Clear();
                }
                
                if (open.TryGetValue(currentKey, out MergeSession existing))
                {
                    skip[i] = true;
                    existing.Add(current);
                    open[currentKey] = existing;
                }
                else
                {
                    open[currentKey] = new MergeSession(i, current, current);
                }
            }
            
            if (open.Count > 0)
            {
                foreach (KeyValuePair<PathKey, MergeSession> kv in open)
                {
                    MergeSession session = kv.Value;
                    
                    if (session.Count > 1)
                    {
                        mergedAtIndex[session.FirstIndex] = Merge(session.FirstEvent, session.LastEvent);
                    }
                }
            }
            
            int write = 0;
            
            for (int i = 0; i < n; i++)
            {
                if (skip[i])
                {
                    continue;
                }

                if (mergedAtIndex.TryGetValue(i, out ChangeEventArgs merged))
                {
                    _buffer[write++] = merged;
                }
                else
                {
                    if (write != i)
                    {
                        _buffer[write] = _buffer[i];
                    }
                    
                    write++;
                }
            }

            if (write < n)
            {
                _buffer.RemoveRange(write, n - write);
            }
        }

        public void Clear() => _buffer.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChangeEventArgs Merge(in ChangeEventArgs first, in ChangeEventArgs last) => new(
            path: last.path,
            oldValue: first.oldValue,
            newValue: last.newValue,
            index: last.index,
            key: last.key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAncestorOrDescendantPaths(in FixedList8<PathSegment> a, in FixedList8<PathSegment> b)
        {
            if (a.Count <= b.Count)
            {
                bool match = true;
                for (int i = 0; i < a.Count; i++)
                {
                    if (a[i].segmentType != b[i].segmentType) { match = false; break; }
                    if (a[i].memberInfo.Id != b[i].memberInfo.Id) { match = false; break; }
                }
                if (match) { return true; }
            }

            if (b.Count <= a.Count)
            {
                bool match = true;
                for (int i = 0; i < b.Count; i++)
                {
                    if (b[i].segmentType != a[i].segmentType) { match = false; break; }
                    if (b[i].memberInfo.Id != a[i].memberInfo.Id) { match = false; break; }
                }
                if (match) { return true; }
            }

            return false;
        }
    }
}