using System;

namespace Klopoff.TrackableState.Core
{
    [AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
    public sealed class TrackableAttribute : Attribute
    {
        /// <summary>
        /// Indicates whether attributes from the original symbol
        /// should be copied to the generated Trackable class
        /// or overridden members.
        /// </summary>
        public bool CopyAttributes { get; set; } = true;
    }
}