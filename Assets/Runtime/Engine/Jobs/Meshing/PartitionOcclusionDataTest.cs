using System.Text;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using static Runtime.Engine.Jobs.Meshing.PartitionOcclusionData.OccDirection;
using static UnityEngine.Debug;

namespace Runtime.Engine.Jobs.Meshing
{
    public class PartitionOcclusionDataTest : UnityEngine.MonoBehaviour
    {
        private static readonly string[] DirectionNames = new[]
        {
            "+X",
            "-X",
            "+Y",
            "-Y",
            "+Z",
            "-Z"
        };

        private void Start()
        {
            PartitionOcclusionData data = new PartitionOcclusionData();
            data.SetFaceConnected(PositiveX, PositiveY);
            data.SetFaceConnected(NegativeZ, PositiveY);
            data.SetFaceConnected(NegativeX, NegativeY);
            data.SetFaceConnected(PositiveZ, NegativeY);
            data.SetFaceConnected(PositiveX, NegativeZ);
            data.SetFaceConnected(NegativeX, PositiveZ);
            data.SetFaceConnected(NegativeY, PositiveY);


            Log("failed connections:");
            if (!data.ArePartitionFacesConnected(PositiveX, PositiveY))
                Log("+X <-> +Y not connected");
            if (!data.ArePartitionFacesConnected(NegativeZ, PositiveY))
                Log("-Z <-> +Y not connected");
            if (!data.ArePartitionFacesConnected(NegativeX, NegativeY))
                Log("-X <-> -Y not connected");
            if (!data.ArePartitionFacesConnected(PositiveZ, NegativeY))
                Log("+Z <-> -Y not connected");
            if (!data.ArePartitionFacesConnected(PositiveX, NegativeZ))
                Log("+X <-> -Z not connected");
            if (!data.ArePartitionFacesConnected(NegativeX, PositiveZ))
                Log("-X <-> +Z not connected");
            if (!data.ArePartitionFacesConnected(NegativeY, PositiveY))
                Log("-Y <-> +Y not connected");


            StringBuilder builder = new();
            builder.AppendLine("  " + string.Join(", ", DirectionNames));
            for (int i = 0; i < 6; i++)
            {
                string line = DirectionNames[i] + ": ";
                for (int j = 0; j < 6; j++)
                {
                    bool connected = data.ArePartitionFacesConnected((PartitionOcclusionData.OccDirection)i,
                        (PartitionOcclusionData.OccDirection)j);
                    line += connected ? " 1 " : " 0 ";
                }

                builder.AppendLine(line);
            }

            Log(builder.ToString());

            // Test GetOccFromNormal
            int3[] testNormals =
            {
                VectorConstants.Int3Forward,
                VectorConstants.Int3Backward,
                VectorConstants.Int3Right,
                VectorConstants.Int3Left,
                VectorConstants.Int3Up,
                VectorConstants.Int3Down
            };

            foreach (var normal in testNormals)
            {
                var result = PartitionOcclusionData.GetOccFromNormal(normal);
                Log($"Normal: {normal} => OccDirection: {result}");
            }
        }
    }
}