using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace ProceduralMeshes {

	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct MeshJob<G, S> : IJob
		where G : struct, IMeshGenerator
		where S : struct, IMeshStreams {

		G generator;

		[WriteOnly]
		S streams;

		public void Execute () => generator.Execute(streams);

		public static JobHandle Schedule (
			Mesh mesh, Mesh.MeshData meshData, G generator, JobHandle dependency
		) {
			var job = new MeshJob<G, S>
			{
				generator = generator
			};
			job.streams.Setup(
				meshData,
				mesh.bounds = job.generator.Bounds,
				job.generator.VertexCount,
				job.generator.IndexCount
			);
			return job.Schedule(dependency);
		}
	}
}