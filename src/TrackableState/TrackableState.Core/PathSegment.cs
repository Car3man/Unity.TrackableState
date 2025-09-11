using System.Runtime.InteropServices;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PathSegment
    {
        public SegmentType segmentType;
        public ChangeKind changeKind;
        public MemberInfo memberInfo;
        
        public PathSegment(
            SegmentType segmentType,
            ChangeKind changeKind
        )
        {
            this.segmentType = segmentType;
            this.changeKind = changeKind;
            memberInfo = default;
        }
        
        public PathSegment(
            SegmentType segmentType,
            ChangeKind changeKind,
            MemberInfo memberInfo
            )
        {
            this.segmentType = segmentType;
            this.changeKind = changeKind;
            this.memberInfo = memberInfo;
        }
    }
}