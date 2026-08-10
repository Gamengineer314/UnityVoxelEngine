#if UNITY_EDITOR
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEditor.AssetImporters;
using Unity.Collections;
using Unity.Mathematics;

namespace Voxels.Editor {

    /// <summary>
    /// .ply importer for models exported from Magica Voxel with the "cube" option
    /// </summary>
    [ScriptedImporter(1, "ply")]
    public class PlyImporter : VoxelImporter {
        [SerializeField] private float plyVoxelSize = 0.1f;

        protected override Native3DArray<Color32> ReadVoxels(string path) {
            using StreamReader reader = new(path);

            // Read header
            string line;
            do {
                line = reader.ReadLine();
            } while (!line.StartsWith("element vertex"));
            int verticesCount = int.Parse(line.Split(' ')[2]);
            do {
                line = reader.ReadLine();
            } while (!line.StartsWith("element face"));
            int facesCount = int.Parse(line.Split(' ')[2]);
            do {
                line = reader.ReadLine();
            } while (!line.StartsWith("end_header"));

            // Read vertices
            int3[] vertices = new int3[verticesCount];
            Color32[] vertexColors = new Color32[verticesCount];
            int3 min = int.MaxValue;
            int3 max = int.MinValue;
            for (int i = 0; i < verticesCount; i++) {
                string[] words = reader.ReadLine().Split(' ');
                vertices[i] = (int3)math.round(new float3(
                    float.Parse(words[0], CultureInfo.InvariantCulture),
                    float.Parse(words[1], CultureInfo.InvariantCulture),
                    float.Parse(words[2], CultureInfo.InvariantCulture)
                ) / plyVoxelSize);
                vertexColors[i] = new Color32(
                    byte.Parse(words[3], CultureInfo.InvariantCulture),
                    byte.Parse(words[4], CultureInfo.InvariantCulture),
                    byte.Parse(words[5], CultureInfo.InvariantCulture),
                    255
                );
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }

            // Read cubes
            Native3DArray<Color32> colors = new(max - min, Allocator.Persistent);
            for (int i = 0; i < facesCount / 6; i++) {
                int vertIndex = int.Parse(reader.ReadLine().Split(' ')[1]);
                int3 local = vertices[vertIndex] - min;
                colors[local.x, local.y, local.z] = vertexColors[vertIndex];
                for (int j = 0; j < 5; j++) {
                    reader.ReadLine();
                }
            }
            return colors;
        }
    }

}
#endif