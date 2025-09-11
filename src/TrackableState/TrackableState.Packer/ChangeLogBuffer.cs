using System;
using System.Collections.Generic;
using Klopoff.TrackableState.Core;
using Klopoff.TrackableState.Core.Pools;

namespace TrackableState.Packer
{
    public class ChangeLogBuffer
    {
        private readonly List<ChangeEventArgs> _buffer = new();
        private readonly PathKeyComparer _comparer = new();

        public IReadOnlyList<ChangeEventArgs> Snapshot => _buffer.ToArray();

        public void Add(in ChangeEventArgs e)
        {
            _buffer.Add(e);
        }

        public void Pack()
        {
            int n = _buffer.Count;
            if (n == 0)
            {
                return;
            }

            using var firstIndexPooled = DictionaryPool<PathKey, int>.GetPooled(out Dictionary<PathKey, int> firstIndex, _comparer);
            using var lastIndexPooled = DictionaryPool<PathKey, int>.GetPooled(out Dictionary<PathKey, int> lastIndex, _comparer);

            for (int i = 0; i < n; i++)
            {
                PathKey pathKey = new PathKey(_buffer[i].path);
                firstIndex.TryAdd(pathKey, i);
                lastIndex[pathKey] = i;
            }
            
            using var compactRangesPooled = DictionaryPool<PathKey, (int, int)>.GetPooled(out Dictionary<PathKey, (int first, int last)> compactRanges, _comparer);
            Span<bool> removed = stackalloc bool[n];

            foreach ((PathKey target, int first) in firstIndex)
            {
                int last = lastIndex[target];
                if (first == last)
                {
                    continue;
                }
                
                bool hasConflict = false;
                
                for (int j = first + 1; j < last; j++)
                {
                    PathKey other = new PathKey(_buffer[j].path);
                    
                    if (IsAncestorOrDescendant(target, other) && !_comparer.Equals(target, other))
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (hasConflict)
                {
                    continue;
                }
                
                compactRanges[target] = (first, last);
                
                for (int j = first; j <= last; j++)
                {
                    removed[j] = true;
                }

                removed[first] = false;
            }
            
            using var insertedPooled = HashSetPool<PathKey>.GetPooled(out HashSet<PathKey> inserted, _comparer);
            int write = 0;

            for (int i = 0; i < n; i++)
            {
                if (removed[i])
                {
                    continue;
                }

                FixedList8<PathSegment> path = _buffer[i].path;
                PathKey key = new PathKey(path);

                if (compactRanges.TryGetValue(key, out (int first, int last) range) && !inserted.Contains(key))
                {
                    ChangeEventArgs firstEvent = _buffer[range.first];
                    ChangeEventArgs lastEvent  = _buffer[range.last];
                    ChangeEventArgs merged = Merge(firstEvent, lastEvent);

                    _buffer[write++] = merged;
                    inserted.Add(key);
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
        
        private ChangeEventArgs Merge(in ChangeEventArgs first, in ChangeEventArgs last) => new(
            path: last.path,
            oldValue: first.oldValue,
            newValue: last.newValue,
            index: last.index,
            key: last.key);

        private bool IsAncestorOrDescendant(PathKey a, PathKey b)
        {
            if (a.Path.Count <= b.Path.Count)
            {
                bool match = true;
                
                for (int i = 0; i < a.Path.Count; i++)
                {
                    if (a.Path[i].segmentType != b.Path[i].segmentType)
                    {
                        match = false;
                        break;
                    }

                    if (a.Path[i].memberInfo.Id != b.Path[i].memberInfo.Id)
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    return true;
                }
            }
            
            if (b.Path.Count <= a.Path.Count)
            {
                bool match = true;
                
                for (int i = 0; i < b.Path.Count; i++)
                {
                    if (b.Path[i].segmentType != a.Path[i].segmentType)
                    {
                        match = false;
                        break;
                    }

                    if (b.Path[i].memberInfo.Id != a.Path[i].memberInfo.Id)
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear() => _buffer.Clear();
    }
}