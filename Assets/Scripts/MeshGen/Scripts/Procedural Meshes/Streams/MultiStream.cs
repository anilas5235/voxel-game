using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralMeshes.Streams {

	public struct MultiStream : IMeshStreams {

		[NativeDisableContainerSafetyRestriction]
		private NativeArray<float3> stream0, stream1;

		[NativeDisableContainerSafetyRestriction]
		private NativeArray<float4> stream2;

		[NativeDisableContainerSafetyRestriction]
		private NativeArray<float4> stream3;

		[NativeDisableContainerSafetyRestriction]
		private NativeArray<TriangleUInt16> triangles;

		public void Setup (
			Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount
		) {
			NativeArray<VertexAttributeDescriptor> descriptor = new(
				4, Allocator.Temp, NativeArrayOptions.UninitializedMemory
			);
			descriptor[0] = new VertexAttributeDescriptor(dimension: 3);
			descriptor[1] = new VertexAttributeDescriptor(
				VertexAttribute.Normal, dimension: 3, stream: 1
			);
			descriptor[2] = new VertexAttributeDescriptor(
				VertexAttribute.Tangent, dimension: 4, stream: 2
			);
			descriptor[3] = new VertexAttributeDescriptor(
				VertexAttribute.TexCoord0, dimension: 4, stream: 3
			);
			meshData.SetVertexBufferParams(vertexCount, descriptor);
			descriptor.Dispose();

			meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

			meshData.subMeshCount = 1;
			meshData.SetSubMesh(
				0, new SubMeshDescriptor(0, indexCount) {
					bounds = bounds,
					vertexCount = vertexCount
				},
				MeshUpdateFlags.DontRecalculateBounds |
				MeshUpdateFlags.DontValidateIndices
			);

			stream0 = meshData.GetVertexData<float3>();
			stream1 = meshData.GetVertexData<float3>(1);
			stream2 = meshData.GetVertexData<float4>(2);
			stream3 = meshData.GetVertexData<float4>(3);
			triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetVertex (int index, Vertex vertex) {
			stream0[index] = vertex.Position;
			stream1[index] = vertex.Normal;
			stream2[index] = vertex.Tangent;
			stream3[index] = vertex.UV0;
		}

		public void SetTriangle (int index, int3 triangle) => triangles[index] = triangle;
	}
}