#if UNITY_EDITOR
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;
using Voxels.Rendering;

namespace Voxels.Editor {

    [ScriptedImporter(1, "ply")]
    public class VoxelPlyImporter : ScriptedImporter {
        [SerializeField] private float plyVoxelSize = 0.1f;


        public override void OnImportAsset(AssetImportContext ctx) {
            // Convert .ply to VoxelColumns
            Native3DArray<Color32> colors;
            using (StreamReader reader = new(ctx.assetPath)) {
                colors = ReadVoxels(reader);
            }
            VoxelColumns voxels = new(colors);
            colors.Dispose();

            // Create asset and prefab
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            VoxelColumnsAsset voxelAsset = ScriptableObject.CreateInstance<VoxelColumnsAsset>();
            voxelAsset.name = assetName;
            voxelAsset.Init(voxels);
            GameObject prefab = new(assetName, typeof(VoxelMesh));
            VoxelMesh mesh = prefab.GetComponent<VoxelMesh>();
            mesh.voxelsAsset = voxelAsset;
            mesh.material = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine("Assets", "Shaders", "Voxels", "VoxelDefault.mat"));

            ctx.AddObjectToAsset("prefab", prefab);
            ctx.SetMainObject(prefab);
            ctx.AddObjectToAsset("voxels", voxelAsset);
        }


        /// <summary>
        /// Read voxels from a .ply file
        /// </summary>
        /// <param name="reader">File reader</param>
        /// <returns>3D array of voxel colors</returns>
        private Native3DArray<Color32> ReadVoxels(StreamReader reader) {
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
            int3 min = 0, max = 0;
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
                min = math.select(min, vertices[i], vertices[i] < min);
                max = math.select(max, vertices[i], vertices[i] > max);
            }

            // Read cubes
            Native3DArray<Color32> colors = new(max.x - min.x, max.y - min.y, max.z - min.z, Allocator.Persistent);
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