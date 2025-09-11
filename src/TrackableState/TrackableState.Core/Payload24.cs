using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Payload24 : IEquatable<Payload24>
    {
        private readonly struct Inline24
        {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
            public readonly ulong A;
            public readonly ulong B;
            public readonly ulong C;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
        }

        private readonly Inline24 _inline;
        private readonly object _fallback;

        private readonly RuntimeTypeHandle _type;
        private readonly PayloadKind _kind;
        private readonly ushort _size;

        public Type Type => Type.GetTypeFromHandle(_type);
        public PayloadKind Kind => _kind;
        public ushort Size => _size;
        
        private const int InlineSize = 24;
        
        private Payload24(RuntimeTypeHandle type, PayloadKind kind, ushort size, Inline24 inline, object fallback)
        {
            _type = type;
            _kind = kind;
            _size = size;
            _inline = inline;
            _fallback = fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Payload24 From<T>(in T value)
        {
            RuntimeTypeHandle type = typeof(T).TypeHandle;
            ushort size = (ushort)Unsafe.SizeOf<T>();
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() && size <= InlineSize)
            {
                Inline24 words = default;

                ref byte dst = ref Unsafe.As<Inline24, byte>(ref words);
                ref T srcT = ref Unsafe.AsRef(in value);
                ref byte src = ref Unsafe.As<T, byte>(ref srcT);

                CopyUpTo24(ref dst, ref src, size);

                return new Payload24(type, PayloadKind.Inline, size, words, null);
            }
            
            return new Payload24(typeof(T).TypeHandle, PayloadKind.Reference, size, default, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(out T value)
        {
            if (_type.Value != typeof(T).TypeHandle.Value)
            {
                value = default;
                return false;
            }

            switch (_kind)
            {
                case PayloadKind.Inline:
                    Inline24 words = _inline;
                    ref byte src = ref Unsafe.As<Inline24, byte>(ref words);
                    value = Unsafe.ReadUnaligned<T>(ref src);
                    return true;
                case PayloadKind.Reference:
                    value = (T)_fallback;
                    return true;
                case PayloadKind.None:
                default:
                    throw new NotImplementedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>()
        {
            if (TryGet(out T value))
            {
                return value;
            }

            throw new InvalidOperationException($"Payload does not contain a value of type {typeof(T)}");
        }

        public bool Equals(Payload24 other)
        {
            if (_kind != other._kind)
            {
                return false;
            }

            switch (_kind)
            {
                case PayloadKind.None:
                    return true;
                case PayloadKind.Inline:
                    return _type.Value == other._type.Value
                           && _size == other._size
                           && _inline.A == other._inline.A
                           && _inline.B == other._inline.B
                           && _inline.C == other._inline.C;
                case PayloadKind.Reference:
                    return ReferenceEquals(_fallback, other._fallback);
                default:
                    throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj) => obj switch
        {
            null => _kind == PayloadKind.None || (_kind == PayloadKind.Reference && _fallback is null),
            Payload24 other => Equals(other),
            _ => false
        };

        public override int GetHashCode()
        {
            if (_kind == PayloadKind.None)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _kind.GetHashCode();
                hash = hash * 31 + _type.GetHashCode();
                hash = hash * 31 + _size.GetHashCode();

                switch (_kind)
                {
                    case PayloadKind.None:
                        break;
                    case PayloadKind.Inline:
                        hash = hash * 31 + _inline.A.GetHashCode();
                        hash = hash * 31 + _inline.B.GetHashCode();
                        hash = hash * 31 + _inline.C.GetHashCode();
                        break;
                    case PayloadKind.Reference:
                        hash = hash * 31 + (_fallback != null ? RuntimeHelpers.GetHashCode(_fallback) : 0);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                return hash;
            }
        }

        public override string ToString()
        {
            switch (_kind)
            {
                case PayloadKind.None:
                    return string.Empty;
                case PayloadKind.Reference:
                    return _fallback?.ToString() ?? string.Empty;
                case PayloadKind.Inline:
                    Type t = Type;

                    Inline24 words = _inline;
                    ref byte src = ref Unsafe.As<Inline24, byte>(ref words);

                    if (t == typeof(bool)) return Unsafe.ReadUnaligned<bool>(ref src).ToString();
                    if (t == typeof(byte)) return Unsafe.ReadUnaligned<byte>(ref src).ToString();
                    if (t == typeof(sbyte)) return Unsafe.ReadUnaligned<sbyte>(ref src).ToString();
                    if (t == typeof(char)) return Unsafe.ReadUnaligned<char>(ref src).ToString();
                    if (t == typeof(short)) return Unsafe.ReadUnaligned<short>(ref src).ToString();
                    if (t == typeof(ushort)) return Unsafe.ReadUnaligned<ushort>(ref src).ToString();
                    if (t == typeof(int)) return Unsafe.ReadUnaligned<int>(ref src).ToString();
                    if (t == typeof(uint)) return Unsafe.ReadUnaligned<uint>(ref src).ToString();
                    if (t == typeof(long)) return Unsafe.ReadUnaligned<long>(ref src).ToString();
                    if (t == typeof(ulong)) return Unsafe.ReadUnaligned<ulong>(ref src).ToString();
                    if (t == typeof(float)) return Unsafe.ReadUnaligned<float>(ref src).ToString(CultureInfo.InvariantCulture);
                    if (t == typeof(double)) return Unsafe.ReadUnaligned<double>(ref src).ToString(CultureInfo.InvariantCulture);
                    if (t == typeof(decimal)) return Unsafe.ReadUnaligned<decimal>(ref src).ToString(CultureInfo.InvariantCulture);
                    if (t == typeof(Guid)) return Unsafe.ReadUnaligned<Guid>(ref src).ToString();
                    if (t == typeof(DateTime)) return Unsafe.ReadUnaligned<DateTime>(ref src).ToString(CultureInfo.InvariantCulture);
                    if (t == typeof(TimeSpan)) return Unsafe.ReadUnaligned<TimeSpan>(ref src).ToString();

                    if (t.IsEnum)
                    {
                        Type ut = Enum.GetUnderlyingType(t);

                        if (ut == typeof(byte)) return Enum.ToObject(t, Unsafe.ReadUnaligned<byte>(ref src)).ToString()!;
                        if (ut == typeof(sbyte)) return Enum.ToObject(t, Unsafe.ReadUnaligned<sbyte>(ref src)).ToString()!;
                        if (ut == typeof(short)) return Enum.ToObject(t, Unsafe.ReadUnaligned<short>(ref src)).ToString()!;
                        if (ut == typeof(ushort)) return Enum.ToObject(t, Unsafe.ReadUnaligned<ushort>(ref src)).ToString()!;
                        if (ut == typeof(int)) return Enum.ToObject(t, Unsafe.ReadUnaligned<int>(ref src)).ToString()!;
                        if (ut == typeof(uint)) return Enum.ToObject(t, Unsafe.ReadUnaligned<uint>(ref src)).ToString()!;
                        if (ut == typeof(long)) return Enum.ToObject(t, Unsafe.ReadUnaligned<long>(ref src)).ToString()!;
                        if (ut == typeof(ulong))  return Enum.ToObject(t, Unsafe.ReadUnaligned<ulong>(ref src)).ToString()!;
                    }
                    
                    return InlineToStringFallback(t)(this);
                default:
                    throw new NotImplementedException();
            }
        }

        // TODO: Maybe cache these delegates?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Func<Payload24, string> InlineToStringFallback(Type t)
        {
            MethodInfo mi = typeof(Payload24).GetMethod(nameof(InlineToStringGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
            MethodInfo gmi = mi.MakeGenericMethod(t);
            return (Func<Payload24, string>)Delegate.CreateDelegate(typeof(Func<Payload24, string>), gmi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string InlineToStringGeneric<T>(Payload24 p) where T : struct
        {
            Inline24 words = p._inline;
            ref byte src = ref Unsafe.As<Inline24, byte>(ref words);
            T value = Unsafe.ReadUnaligned<T>(ref src);
            return value.ToString();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyUpTo24(ref byte dst, ref byte src, int size)
        {
            if (size >= 16)
            {
                Unsafe.WriteUnaligned(ref dst, Unsafe.ReadUnaligned<ulong>(ref src));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 8), Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, 8)));

                int rem = size - 16;
                if (rem == 0) return;

                if (rem >= 8)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 16), Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, 16)));
                    return;
                }

                int off = 16;
                if ((rem & 4) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, off)));
                    off += 4;
                }
                if ((rem & 2) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, off)));
                    off += 2;
                }
                if ((rem & 1) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref src, off)));
                }
                
                return;
            }
            
            if (size >= 8)
            {
                Unsafe.WriteUnaligned(ref dst, Unsafe.ReadUnaligned<ulong>(ref src));

                int rem = size - 8;
                if (rem == 0) return;

                int off = 8;
                if ((rem & 4) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, off)));
                    off += 4;
                }
                if ((rem & 2) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, off)));
                    off += 2;
                }
                if ((rem & 1) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref src, off)));
                }
                return;
            }
            
            {
                int off = 0;
                if ((size & 4) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, off)));
                    off += 4;
                }
                if ((size & 2) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, off)));
                    off += 2;
                }
                if ((size & 1) != 0)
                {
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, off), Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref src, off)));
                }
            }
        }

        public static bool operator ==(Payload24 left, Payload24 right) => left.Equals(right);

        public static bool operator !=(Payload24 left, Payload24 right) => !left.Equals(right);
    }
}