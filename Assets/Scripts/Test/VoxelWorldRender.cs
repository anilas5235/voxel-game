using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Test
{
    public class VoxelWorldRender
    {
        private RenderBufferManager _solidBufferManager = new();
        private RenderBufferManager _transparentBufferManager = new();
        private RenderBufferManager _foliageBufferManager = new();

        Dictionary<int2, GraphicsBuffer> _voxelDataBuffers = new();

        public void UpdatePartitions(List<int3> partitions)
        {
            foreach (int3 partitionPos in partitions)
            {
            }
        }

        private void CopyJob()
        {
        }
    }

    public class RenderBufferManager : IDisposable
    {
        private const int RenderBufferSize = PageSize * PagesPerBuffer;
        private const int PageSize = 128;
        private const int PagesPerBuffer = 512;

        private class BufferPage
        {
            public int3 PartitionPos;
            public int PointCount;

            public bool IsFull => PointCount >= PageSize;

            public void Clear()
            {
                PointCount = 0;
                PartitionPos = default;
            }
        }

        private class RenderBuffer : IDisposable
        {
            private static readonly uint[] DefaultArgs = { 0u, 1u, 0u, 0u, 0u };
            private readonly GraphicsBuffer _buffer;
            private readonly BufferPage[] _pages;
            private readonly GraphicsBuffer _argsBuffer;
            public BufferPage[] Pages => _pages;

            public RenderBuffer()
            {
                _buffer = new GraphicsBuffer(Target.Structured, RenderBufferSize, Marshal.SizeOf<Vertex>());
                _argsBuffer = new GraphicsBuffer(Target.IndirectArguments, 5, sizeof(uint));
                _argsBuffer.SetData(DefaultArgs);

                _pages = new BufferPage[PagesPerBuffer];
                for (int i = 0; i < _pages.Length; i++) _pages[i] = new BufferPage();
            }

            public void Dispose()
            {
                _buffer?.Dispose();
                _argsBuffer?.Dispose();
            }

            public bool TryFindFreePage(out int pageIndex)
            {
                for (int i = 0; i < _pages.Length; i++)
                {
                    if (_pages[i].PointCount != 0) continue;
                    
                    pageIndex = i;
                    return true;
                }

                pageIndex = -1;
                return false;
            }
        }

        private readonly List<RenderBuffer> _buffers = new();
        private readonly Dictionary<int3, List<BufferPage>> _partitionAllocations = new();
        private bool _isDisposed;

        public RenderBufferManager(int initialBuffers = 1)
        {
            for (int i = 0; i < initialBuffers; i++)
            {
                AddNewBuffer();
            }
        }

        public void Draw()
        {
            
        }

        public readonly struct AllocInfo
        {
            public readonly int BufferIndex;
            public readonly int PageIndex;
            public readonly int PointCount;
            public int Offset => PageIndex * PageSize;

            public AllocInfo(int bufferIndex, int pageIndex, int pointCount)
            {
                BufferIndex = bufferIndex;
                PageIndex = pageIndex;
                PointCount = pointCount;
            }
        }

        public List<AllocInfo> AllocBufferSpace(int3 partitionPos, int pointCount)
        {
            ThrowIfDisposed();

            if (pointCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount), "Point count must be >= 0.");
            }

            ReleasePartition(partitionPos);
            if (pointCount == 0)
            {
                return new List<AllocInfo>();
            }

            List<AllocInfo> allocations = new();
            List<BufferPage> pages = new();

            int remaining = pointCount;

            while (remaining > 0)
            {
                int count = math.min(PageSize, remaining);
                (int bufferIndex, int pageIndex) = ReservePage();
                BufferPage page = _buffers[bufferIndex].Pages[pageIndex];
                page.PartitionPos = partitionPos;
                page.PointCount = count;
                pages.Add(page);
                allocations.Add(new AllocInfo(bufferIndex, pageIndex, count));
                remaining -= count;
            }

            _partitionAllocations[partitionPos] = pages;
            return allocations;
        }

        private (int BufferIndex, int PageIndex) ReservePage()
        {
            for (int i = 0; i < _buffers.Count; i++)
            {
                if (_buffers[i].TryFindFreePage(out int pageIndex))
                {
                    return (i, pageIndex);
                }
            }

            AddNewBuffer();
            int newBufferIndex = _buffers.Count - 1;
            if (!_buffers[newBufferIndex].TryFindFreePage(out int firstPageIndex))
            {
                throw new InvalidOperationException("Newly allocated render buffer has no free page.");
            }

            return (newBufferIndex, firstPageIndex);
        }

        private void ReleasePartition(int3 partitionPos)
        {
            if (!_partitionAllocations.TryGetValue(partitionPos, out List<BufferPage> allocations))
            {
                return;
            }

            foreach (BufferPage page in allocations) page.Clear();

            _partitionAllocations.Remove(partitionPos);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RenderBufferManager));
            }
        }

        private void AddNewBuffer()
        {
            _buffers.Add(new RenderBuffer());
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (RenderBuffer buffer in _buffers)
            {
                buffer.Dispose();
            }

            _buffers.Clear();
            _partitionAllocations.Clear();
            _isDisposed = true;
        }
    }
}