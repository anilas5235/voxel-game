using UnityEngine;

namespace ProceduralMeshes {

	public interface IMeshGenerator {

		Bounds Bounds { get; }

		int VertexCount { get; }

		int IndexCount { get; }

		int JobLength { get; }

		int Resolution { get; set; }

		void Execute<S> (S streams) where S : struct, IMeshStreams;
	}
}