using System;

namespace Klopoff.TrackableState
{
    public interface ITrackable
    {
        event EventHandler<ChangeEventArgs> Changed;
        bool IsDirty { get; }
        void AcceptChanges();
    }
    
    public enum PathSegmentType
    {
        Property,
        List,
        Set,
        Dictionary
    }
    
    public enum ChangeKind
    {
        PropertySet,
        CollectionAdd,
        CollectionRemove,
        CollectionReplace,
        CollectionClear,
        ChildChange
    }

    public sealed class ChangeEventArgs : EventArgs
    {
        private readonly object _oldValue;
        private readonly object _newValue;

        public string Path => BuildPathSlow();
        public PathSegmentType SegmentType { get; }
        public ChangeKind Kind { get; }
        public string MemberName { get; }
        public object OldValue => Leaf._oldValue;
        public object NewValue => Leaf._newValue;
        public int Index { get; }
        public object Key { get; }
        public ChangeEventArgs Inner { get; }
        public ChangeEventArgs Leaf
        {
            get
            {
                ChangeEventArgs iter = this;
                ChangeEventArgs last = this;
                
                while (iter != null)
                {
                    last = iter;
                    iter = iter.Inner;
                }

                return last;
            }
        }
        
        public ChangeEventArgs(
            PathSegmentType segmentType,
            ChangeKind kind,
            string memberName,
            object oldValue = null,
            object newValue = null,
            int index = -1,
            object key = null,
            ChangeEventArgs inner = null)
        {
            SegmentType = segmentType;
            Kind = kind;
            MemberName = memberName;
            Index = index;
            Key = key;
            Inner = inner;
            
            _oldValue = oldValue;
            _newValue = newValue;
        }
        
        public static ChangeEventArgs PropertySet(string propertyName, object oldValue, object newValue)
            => new(PathSegmentType.Property, ChangeKind.PropertySet, propertyName, oldValue, newValue);
        public static ChangeEventArgs ListAdd(string listName, int index, object newItem)
            => new(PathSegmentType.List, ChangeKind.CollectionAdd, listName, null, newItem, index);
        public static ChangeEventArgs ListRemove(string listName, int index, object oldItem)
            => new(PathSegmentType.List, ChangeKind.CollectionRemove, listName, oldItem, null, index);
        public static ChangeEventArgs ListReplace(string listName, int index, object oldItem, object newItem)
            => new(PathSegmentType.List, ChangeKind.CollectionReplace, listName, oldItem, newItem, index);
        public static ChangeEventArgs ListClear(string listName)
            => new(PathSegmentType.List, ChangeKind.CollectionClear, listName);
        public static ChangeEventArgs SetAdd(string setName, object newItem)
            => new(PathSegmentType.Set, ChangeKind.CollectionAdd, setName, null, newItem);
        public static ChangeEventArgs SetRemove(string setName, object oldItem)
            => new(PathSegmentType.Set, ChangeKind.CollectionRemove, setName, oldItem, null);
        public static ChangeEventArgs SetClear(string setName)
            => new(PathSegmentType.Set, ChangeKind.CollectionClear, setName);
        public static ChangeEventArgs DictAdd(string dictName, object key, object newValue)
            => new(PathSegmentType.Dictionary, ChangeKind.CollectionAdd, dictName, null, newValue, -1, key);
        public static ChangeEventArgs DictRemove(string dictName, object key, object oldValue)
            => new(PathSegmentType.Dictionary, ChangeKind.CollectionRemove, dictName, oldValue, null, -1, key);
        public static ChangeEventArgs DictReplace(string dictName, object key, object oldValue, object newValue)
            => new(PathSegmentType.Dictionary, ChangeKind.CollectionReplace, dictName, oldValue, newValue, -1, key);
        public static ChangeEventArgs DictClear(string dictName)
            => new(PathSegmentType.Dictionary, ChangeKind.CollectionClear, dictName);
        public static ChangeEventArgs ChildProperty(string childPropertyName, ChangeEventArgs inner)
            => new(PathSegmentType.Property, ChangeKind.ChildChange, childPropertyName, inner: inner);
        public static ChangeEventArgs ChildOfList(string listName, int index, ChangeEventArgs inner)
            => new(PathSegmentType.List, ChangeKind.ChildChange, listName, index: index, inner: inner);
        public static ChangeEventArgs ChildOfSet(string setName, ChangeEventArgs inner)
            => new(PathSegmentType.Set, ChangeKind.ChildChange, setName, inner: inner);
        public static ChangeEventArgs ChildOfDict(string dictName, object key, ChangeEventArgs inner)
            => new(PathSegmentType.Dictionary, ChangeKind.ChildChange, dictName, key: key, inner: inner);

        private string BuildPathSlow()
        {
            string thisSeg = SegmentType switch
            {
                PathSegmentType.Property => MemberName,
                PathSegmentType.List => Kind != ChangeKind.CollectionClear ? $"[{Index}]" : string.Empty,
                PathSegmentType.Set => Kind != ChangeKind.CollectionClear ? "[*]" : string.Empty,
                PathSegmentType.Dictionary => Kind != ChangeKind.CollectionClear ? $"[{Key}]" : string.Empty,
                _ => MemberName
            };

            if (Inner == null)
            {
                return thisSeg;
            }
                
            string innerPath = Inner.Path;
            if (string.IsNullOrEmpty(innerPath))
            {
                return thisSeg;
            }

            if (Inner.SegmentType is PathSegmentType.List or PathSegmentType.Set or PathSegmentType.Dictionary)
            {
                return $"{thisSeg}{innerPath}";
            }

            return $"{thisSeg}.{innerPath}";
        }

        public override string ToString() => $"{Kind} @ {Path} (Old={OldValue ?? "null"}, New={NewValue ?? "null"})";
    }
}