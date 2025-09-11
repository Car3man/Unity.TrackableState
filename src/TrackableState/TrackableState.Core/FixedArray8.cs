using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    public struct FixedArray8<T>
    {
        private T _0;
        private T _1;
        private T _2;
        private T _3;
        private T _4;
        private T _5;
        private T _6;
        private T _7;

        public const int Length = 8;

        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= Length)
                {
                    throw new IndexOutOfRangeException();
                }
                
                return ref AsSpan()[index];
            }
        }
        
        public FixedArray8(Span<T> span)
        {
            if (span.Length > Length)
            {
                throw new ArgumentException($"Span length must less than {Length}", nameof(span));
            }
            
            this = default;
            Span<T> dest = AsSpan();
            span.CopyTo(dest);
        }
        
        public FixedArray8(T i0 = default, T i1 = default, T i2 = default, T i3 = default,
            T i4 = default, T i5 = default, T i6 = default, T i7 = default)
        {
            _0 = i0; _1 = i1; _2 = i2; _3 = i3;
            _4 = i4; _5 = i5; _6 = i6; _7 = i7;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref _0, Length);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();
    }
}