using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace ProceduralMeshes {

	[StructLayout(LayoutKind.Sequential)]
	public struct Vertex {
		public float3 Position, Normal;
		public float4 Tangent;
		public float4 UV0;
	}
}