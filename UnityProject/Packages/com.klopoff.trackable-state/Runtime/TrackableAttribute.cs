using System;

namespace Klopoff.TrackableState
{
    [AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
    public sealed class TrackableAttribute : Attribute
    {
    }
}