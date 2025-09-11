using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Klopoff.TrackableState.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FixedList8<T> : IEnumerable<T>
    {
        private FixedArray8<T> _items;
        private byte _count;

        public const int Capacity = FixedArray8<T>.Length;

        public int Count => _count;

        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= _count)
                {
                    throw new IndexOutOfRangeException();
                }

                return ref _items.AsSpan()[index];
            }
        }

        public FixedList8(ReadOnlySpan<T> items)
        {
            if (items.Length > Capacity)
            {
                throw new ArgumentException($"Span length must be <= {Capacity}", nameof(items));
            }

            _items = default;
            _count = (byte)items.Length;

            items.CopyTo(_items.AsSpan());
        }
        
        public FixedList8(T i0)
        {
            _items = new FixedArray8<T>(i0);
            _count = 1;
        }

        public void Clear()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T value)
        {
            if (_count >= Capacity)
            {
                throw new InvalidOperationException("FixedList8 is full.");
            }
            
            _items.AsSpan()[_count++] = value;
        }
        
        public void Insert(int index, in T value)
        {
            if ((uint)index > _count)
            {
                throw new IndexOutOfRangeException();
            }

            if (_count >= Capacity)
            {
                throw new InvalidOperationException("FixedList8 is full.");
            }

            Span<T> span = _items.AsSpan();
            
            if (index < _count)
            {
                span.Slice(index, _count - index).CopyTo(span.Slice(index + 1));
            }

            span[index] = value;
            _count++;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= _count)
            {
                throw new IndexOutOfRangeException();
            }

            Span<T> span = _items.AsSpan();
            
            int tail = _count - index - 1;
            if (tail > 0)
            {
                span.Slice(index + 1, tail).CopyTo(span.Slice(index));
            }
            
            span[_count - 1] = default;
            _count--;
        }
        
        public int IndexOf(T value)
        {
            EqualityComparer<T> cmp = EqualityComparer<T>.Default;
            ReadOnlySpan<T> span = AsReadOnlySpan();
            
            for (int i = 0; i < span.Length; i++)
            {
                if (cmp.Equals(span[i], value))
                {
                    return i;
                }
            }
            
            return -1;
        }

        public bool Contains(T value) => IndexOf(value) >= 0;
        
        public FixedArray8<T> Reverse()
        {
            FixedArray8<T> result = default;
            ReadOnlySpan<T> span = AsReadOnlySpan();
            Span<T> dest = result.AsSpan()[.._count];
            
            for (int i = 0; i < span.Length; i++)
            {
                dest[i] = span[span.Length - 1 - i];
            }

            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() => _items.AsSpan()[.._count];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();
        
        public struct Enumerator : IEnumerator<T>
        {
            private FixedArray8<T> _items;
            private int _count;
            private int _index;

            internal Enumerator(FixedArray8<T> items, int count)
            {
                _items = items;
                _count = count;
                _index = -1;
            }

            public T Current => (uint)_index >= (uint)_count ? throw new InvalidOperationException() : _items.AsReadOnlySpan()[_index];

            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next < _count)
                {
                    _index = next;
                    return true;
                }
                return false;
            }

            public void Reset() => _index = -1;

            public void Dispose() { }
        }
        
        public Enumerator GetEnumerator() => new(_items, _count);
        
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(_items, _count);
        
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_items, _count);
    }
}