using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Runtime.Engine.Unsafe
{
    /// <summary>
    /// A lightweight unsafe array backed by unmanaged memory.
    /// The caller is responsible for disposing this struct.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    public unsafe struct UnsafeArray<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private void* _ptr;
        private int _length;
        private AllocatorManager.AllocatorHandle _allocator;
        private bool _isDisposed;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle _safetyHandle;
#endif

        /// <summary>Total number of elements.</summary>
        public int Length => _length;

        /// <summary>
        /// Returns <c>true</c> if the array has been allocated and not yet disposed.
        /// </summary>
        public bool IsCreated => _ptr != null && !_isDisposed;

        /// <summary>
        /// Allocates a new unmanaged array of the given length.
        /// </summary>
        /// <param name="length">Number of elements. Must be &gt;= 0.</param>
        /// <param name="allocator">Unity allocator to use.</param>
        public UnsafeArray(int length, Allocator allocator = Allocator.Temp)
        {
            this = default;

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

            _length = length;
            _allocator = allocator;
            _isDisposed = false;

            if (length > 0)
            {
                long byteLength = (long)length * UnsafeUtility.SizeOf<T>();
                _ptr = UnsafeUtility.Malloc(byteLength, UnsafeUtility.AlignOf<T>(), allocator);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _safetyHandle = AtomicSafetyHandle.Create();
#endif
        }

        /// <summary>
        /// Allocates a new unmanaged array and copies data from a <see cref="NativeArray{T}"/>.
        /// </summary>
        public UnsafeArray(NativeArray<T> source, Allocator allocator = Allocator.Temp)
            : this(source.Length, allocator)
        {
            if (_length > 0)
            {
                long byteLength = (long)_length * UnsafeUtility.SizeOf<T>();
                UnsafeUtility.MemCpy(_ptr, source.GetUnsafeReadOnlyPtr(), byteLength);
            }
        }

        /// <summary>
        /// Gets a reference to the element at <paramref name="index"/>.
        /// </summary>
        public ref T this[int index]
        {
            get
            {
                if (!IsCreated)
                    throw new ObjectDisposedException(nameof(UnsafeArray<T>));

                // Cast to uint catches both negative values and values >= _length in one comparison.
                if ((uint)index >= (uint)_length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range for length {_length}.");

                return ref ((T*)_ptr)[index];
            }
        }

        /// <summary>
        /// Returns a <see cref="NativeArray{T}"/> view over the same memory.
        /// <para><b>Warning:</b> The returned array's lifetime is tied to this <see cref="UnsafeArray{T}"/>.
        /// Do not use the <see cref="NativeArray{T}"/> after this instance has been disposed.</para>
        /// </summary>
        public NativeArray<T> AsNativeArray()
        {
            if (!IsCreated)
                throw new ObjectDisposedException(nameof(UnsafeArray<T>));

            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                _ptr, _length, Allocator.None
            );
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _safetyHandle);
#endif
            return array;
        }

        /// <summary>
        /// Releases unmanaged memory. Safe to call even when the array is empty.
        /// Calling <see cref="Dispose"/> more than once is a no-op.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_ptr != null)
            {
                UnsafeUtility.Free(_ptr, _allocator.ToAllocator);
                _ptr = null;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(_safetyHandle))
            {
                AtomicSafetyHandle.Release(_safetyHandle);
                _safetyHandle = default;
            }
#endif
            _length = 0;
            _allocator = default;
        }
    }
}