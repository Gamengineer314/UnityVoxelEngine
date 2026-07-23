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
using System.Collections.Generic;

namespace Voxels.Editor {

    [ScriptedImporter(1, "ply")]
    public class VoxelPlyImporter : ScriptedImporter {
        [SerializeField] private float plyVoxelSize = 0.1f;
        [SerializeField] private float3 offset;
        [SerializeField] private bool fillHoles = true;
        [SerializeField] private bool removeInside = true;


        public override void OnImportAsset(AssetImportContext ctx) {
            // Convert .ply to VoxelColumns
            Native3DArray<Color32> colors;
            using (StreamReader reader = new(ctx.assetPath)) {
                colors = ReadVoxels(reader);
            }
            if (fillHoles) FillHoles(colors);
            if (removeInside) RemoveInside(colors);
            VoxelColumns voxels = new(colors, offset);
            colors.Dispose();

            // Create asset and prefab
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            VoxelColumnsAsset voxelAsset = ScriptableObject.CreateInstance<VoxelColumnsAsset>();
            voxelAsset.name = assetName;
            voxelAsset.Init(voxels);
            GameObject prefab = new(assetName, typeof(VoxelMesh));
            VoxelMesh mesh = prefab.GetComponent<VoxelMesh>();
            mesh.voxelsAsset = voxelAsset;
            mesh.parameters = AssetDatabase.LoadAssetAtPath<GenerationParameters>(Path.Combine("Assets", "Voxels", "Default.asset"));
            mesh.material = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine("Assets", "Voxels", "Shaders", "VoxelDefault.mat"));

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


        /// <summary>
        /// Fill invisible holes in the model
        /// </summary>
        /// <param name="colors">3D array of voxel colors</param>
        private void FillHoles(Native3DArray<Color32> colors) {
            Stack<int3> stack = new();
            List<int3> list = new();
            Native3DArray<bool> visited = new(colors.sizeX, colors.sizeY, colors.sizeZ, Allocator.Temp);
            for (int x = 0; x < colors.sizeX; x++) {
                for (int y = 0; y < colors.sizeY; y++) {
                    for (int z = 0; z < colors.sizeZ; z++) {
                        if (!Voxel.Color32Equals(colors[x, y, z], default)) continue;
                        bool isHole = true;
                        stack.Push(new int3(x, y, z));
                        do {
                            int3 pos = stack.Pop();
                            if (math.any(pos == -1) || pos.x == colors.sizeX || pos.y == colors.sizeY || pos.z == colors.sizeZ) {
                                isHole = false;
                                continue;
                            }
                            if (visited[pos] || !Voxel.Color32Equals(colors[pos], default)) continue;
                            visited[pos] = true;
                            list.Add(pos);
                            stack.Push(new(pos.x - 1, pos.y, pos.z));
                            stack.Push(new(pos.x + 1, pos.y, pos.z));
                            stack.Push(new(pos.x, pos.y + 1, pos.z));
                            stack.Push(new(pos.x, pos.y - 1, pos.z));
                            stack.Push(new(pos.x, pos.y, pos.z + 1));
                            stack.Push(new(pos.x, pos.y, pos.z - 1));
                        } while (stack.Count > 0);
                        if (isHole) {
                            foreach (int3 pos in list) {
                                colors[pos] = Color.black;
                            }
                        }
                        list.Clear();
                    }
                }
            }
        }


        /// <summary>
        /// Remove invisible blocks in the model
        /// </summary>
        /// <param name="colors">3D array of voxel colors</param>
        private void RemoveInside(Native3DArray<Color32> colors) {
            // Find visible voxels
            Native3DArray<bool> visible = new(colors.sizeX, colors.sizeY, colors.sizeZ, Allocator.Temp);
            for (int x = 0; x < colors.sizeX; x++) {
                for (int y = 0; y < colors.sizeY; y++) {
                    for (int z = 0; z < colors.sizeZ; z++) {
                        if (!Voxel.Color32Equals(colors[x, y, z], default) && (
                            x == 0 || y == 0 || z == 0 || x == colors.sizeX - 1 || y == colors.sizeY - 1 || z == colors.sizeZ - 1 ||
                            Voxel.Color32Equals(colors[x - 1, y, z], default) ||
                            Voxel.Color32Equals(colors[x + 1, y, z], default) ||
                            Voxel.Color32Equals(colors[x, y - 1, z], default) ||
                            Voxel.Color32Equals(colors[x, y + 1, z], default) ||
                            Voxel.Color32Equals(colors[x, y, z - 1], default) ||
                            Voxel.Color32Equals(colors[x, y, z + 1], default)
                        )) {
                            visible[x, y, z] = true;
                        }
                    }
                }
            }

            // Hide invisible faces
            for (int x = 0; x < colors.sizeX; x++) {
                for (int y = 0; y < colors.sizeY; y++) {
                    for (int z = 0; z < colors.sizeZ; z++) {
                        if (!Voxel.Color32Equals(colors[x, y, z], default) && !visible[x, y, z] && (
                            visible[x - 1, y, z] ||
                            visible[x + 1, y, z] ||
                            visible[x, y - 1, z] ||
                            visible[x, y + 1, z] ||
                            visible[x, y, z - 1] ||
                            visible[x, y, z + 1]
                        )) {
                            colors[x, y, z] = Voxel.ghost;
                        }
                    }
                }
            }

            // Remove all other voxels
            for (int x = 0; x < colors.sizeX; x++) {
                for (int y = 0; y < colors.sizeY; y++) {
                    for (int z = 0; z < colors.sizeZ; z++) {
                        if (!visible[x, y, z] && !Voxel.Color32Equals(colors[x, y, z], Voxel.ghost)) {
                            colors[x, y, z] = default;
                        }
                    }
                }
            }
        }
    }

}
#endif