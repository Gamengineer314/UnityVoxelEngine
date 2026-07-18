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

            // Create TextAsset
            string directoryPath = Path.Combine(Application.dataPath, "Voxels");
            Directory.CreateDirectory(directoryPath);
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            string bytesName = assetName + ".bytes";
            voxels.Write(Path.Combine(directoryPath, bytesName));
            voxels.Dispose();
            AssetDatabase.ImportAsset(Path.Combine("Assets", "Voxels", bytesName));

            // Create asset and prefab
            VoxelColumnsAsset voxelAsset = ScriptableObject.CreateInstance<VoxelColumnsAsset>();
            voxelAsset.name = assetName;
            GameObject prefab = new(assetName, typeof(VoxelMesh));
            VoxelMesh mesh = prefab.GetComponent<VoxelMesh>();
            mesh.voxelsAsset = voxelAsset;
            mesh.material = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine("Assets", "Shaders", "Voxels", "VoxelDefault.mat"));

            // Create VoxelColumnsAsset
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


        private class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
                // Assign TextAsset references to created VoxelColumnsAsset after the TextAsset was imported
                foreach (string assetPath in importedAssets) {
                    if (Path.GetExtension(assetPath) == ".ply") {
                        VoxelColumnsAsset voxelAsset = AssetDatabase.LoadAssetAtPath<VoxelColumnsAsset>(assetPath);
                        string textAssetPath = Path.Combine("Assets", "Voxels", Path.GetFileNameWithoutExtension(assetPath) + ".bytes");
                        TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(textAssetPath);
                        voxelAsset.Asset = textAsset;
                    }
                }

                // Delete TextAsset for deleted VoxelColumnAsset
                foreach (string assetPath in deletedAssets) {
                    if (Path.GetExtension(assetPath) == ".ply") {
                        string textAssetPath = Path.Combine("Assets", "Voxels", Path.GetFileNameWithoutExtension(assetPath) + ".bytes");
                        File.Delete(textAssetPath);
                        File.Delete(textAssetPath + ".meta");
                    }
                }
            }
        }
    }

}
#endif