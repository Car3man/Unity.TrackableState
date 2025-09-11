using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PathSegment
    {
        public SegmentType segmentType;
        public ChangeKind changeKind;
        public string memberName;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PathSegment(SegmentType segmentType, ChangeKind changeKind, string memberName)
        {
            this.segmentType = segmentType;
            this.changeKind = changeKind;
            this.memberName = memberName;
        }
    }
}