using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ChangeEventArgs
    {
        public readonly FixedList8<PathSegment> path;
        public readonly Payload24 oldValue;
        public readonly Payload24 newValue;
        public readonly int index;
        public readonly Payload24 key;
        
        public string PathString
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < path.Count; i++)
                {
                    PathSegment segment = path[i];
                    
                    if (i > 0 && segment.segmentType == SegmentType.Property)
                    {
                        sb.Append('.');
                    }

                    switch (segment.segmentType)
                    {
                        case SegmentType.Property:
                            sb.Append(segment.memberInfo.Name);
                            break;
                        case SegmentType.List:
                            if (segment.changeKind != ChangeKind.CollectionClear)
                            {
                                sb.AppendFormat("[{0}]", index);
                            }
                            break;
                        case SegmentType.Set:
                            if (segment.changeKind != ChangeKind.CollectionClear)
                            {
                                sb.Append("[*]");
                            }
                            break;
                        case SegmentType.Dictionary:
                            if (segment.changeKind != ChangeKind.CollectionClear)
                            {
                                sb.AppendFormat("[{0}]", key);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                return sb.ToString();
            }
        }
        
        public ChangeEventArgs(
            FixedList8<PathSegment> path,
            in Payload24 oldValue,
            in Payload24 newValue,
            int index,
            in Payload24 key
        )
        {
            this.path = path;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.index = index;
            this.key = key;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs PropertySet(in MemberInfo member, in Payload24 oldValue, in Payload24 newValue)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Property, ChangeKind.PropertySet, member));
            return new ChangeEventArgs(path, oldValue, newValue, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ListAdd(in Payload24 newValue, int index)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.List, ChangeKind.CollectionAdd));
            return new ChangeEventArgs(path, default, newValue, index, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ListRemove(in Payload24 oldValue, int index)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.List, ChangeKind.CollectionRemove));
            return new ChangeEventArgs(path, oldValue, default, index, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ListReplace(in Payload24 oldValue, in Payload24 newValue, int index)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.List, ChangeKind.CollectionReplace));
            return new ChangeEventArgs(path, oldValue, newValue, index, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ListClear()
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.List, ChangeKind.CollectionClear));
            return new ChangeEventArgs(path , default, default, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs SetAdd(in Payload24 newValue)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Set, ChangeKind.CollectionAdd));
            return new ChangeEventArgs(path, default, newValue, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs SetRemove(in Payload24 oldValue)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Set, ChangeKind.CollectionRemove));
            return new ChangeEventArgs(path, oldValue, default, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs SetClear()
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Set, ChangeKind.CollectionClear));
            return new ChangeEventArgs(path , default, default, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs DictionaryAdd(in Payload24 newValue, in Payload24 key)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Dictionary, ChangeKind.CollectionAdd));
            return new ChangeEventArgs(path, default, newValue, -1, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs DictionaryRemove(in Payload24 oldValue, in Payload24 key)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Dictionary, ChangeKind.CollectionRemove));
            return new ChangeEventArgs(path, oldValue, default, -1, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs DictionaryReplace(in Payload24 oldValue, in Payload24 newValue, in Payload24 key)
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Dictionary, ChangeKind.CollectionReplace));
            return new ChangeEventArgs(path, oldValue, newValue, -1, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs DictionaryClear()
        {
            FixedList8<PathSegment> path = new FixedList8<PathSegment>(new PathSegment(SegmentType.Dictionary, ChangeKind.CollectionClear));
            return new ChangeEventArgs(path , default, default, -1, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ChildProperty(in ChangeEventArgs childEvent, in MemberInfo member)
        {
            FixedList8<PathSegment> childPath = childEvent.path;
            childPath.Insert(0, new PathSegment(SegmentType.Property, ChangeKind.ChildChange, member));
            return new ChangeEventArgs(childPath, childEvent.oldValue, childEvent.newValue, childEvent.index, childEvent.key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ChildOfList(in ChangeEventArgs childEvent, int index)
        {
            FixedList8<PathSegment> childPath = childEvent.path;
            childPath.Insert(0, new PathSegment(SegmentType.List, ChangeKind.ChildChange));
            return new ChangeEventArgs(childPath, childEvent.oldValue, childEvent.newValue, index, childEvent.key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ChildOfSet(in ChangeEventArgs childEvent)
        {
            FixedList8<PathSegment> childPath = childEvent.path;
            childPath.Insert(0, new PathSegment(SegmentType.Set, ChangeKind.ChildChange));
            return new ChangeEventArgs(childPath, childEvent.oldValue, childEvent.newValue, childEvent.index, childEvent.key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChangeEventArgs ChildOfDictionary(in ChangeEventArgs childEvent, in Payload24 key)
        {
            FixedList8<PathSegment> childPath = childEvent.path;
            childPath.Insert(0, new PathSegment(SegmentType.Dictionary, ChangeKind.ChildChange));
            return new ChangeEventArgs(childPath, childEvent.oldValue, childEvent.newValue, childEvent.index, key != default ? key : childEvent.key);
        }
    }
}