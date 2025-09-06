using System;

namespace Klopoff.TrackableState.Core
{
    [AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
    public sealed class TrackableAttribute : Attribute
    {
    }
}