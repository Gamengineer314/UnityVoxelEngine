#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;
using Voxels.Rendering;

namespace Voxels.Editor {

    /// <summary>
    /// Voxel model importer
    /// </summary>
    public abstract class VoxelImporter : ScriptedImporter {
        [SerializeField] private float3 offset;
        [SerializeField] private bool fillHoles = true;
        [SerializeField] private bool removeInside = true;
        [SerializeField] private bool swapVerticalAxis = true;


        /// <summary>
        /// Read voxels from a file
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns>3D array of voxel colors</returns>
        protected abstract Native3DArray<Color32> ReadVoxels(string path);


        public override void OnImportAsset(AssetImportContext ctx) {
            // Convert .ply to VoxelColumns
            Native3DArray<Color32> colors = ReadVoxels(ctx.assetPath);
            if (fillHoles) FillHoles(colors);
            if (removeInside) RemoveInside(colors);
            if (swapVerticalAxis) colors = SwapVerticalAxis(colors);
            VoxelColumns voxels = new(colors, offset);
            colors.Dispose();

            // Create asset and prefab
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            VoxelColumnsAsset voxelAsset = ScriptableObject.CreateInstance<VoxelColumnsAsset>();
            voxelAsset.name = assetName;
            voxelAsset.Init(voxels);
            voxels.Dispose();
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


        /// <summary>
        /// Swap y and z axis
        /// </summary>
        /// <param name="colors">3D array of voxel colors</param>
        /// <returns>Resulting 3D array of voxel colors</returns>
        private Native3DArray<Color32> SwapVerticalAxis(Native3DArray<Color32> colors) {
            Native3DArray<Color32> swapped = new(colors.sizeX, colors.sizeZ, colors.sizeY, Allocator.Persistent);
            for (int x = 0; x < colors.sizeX; x++) {
                for (int y = 0; y < colors.sizeY; y++) {
                    for (int z = 0; z < colors.sizeZ; z++) {
                        swapped[x, z, y] = colors[x, y, z];
                    }
                }
            }
            colors.Dispose();
            return swapped;
        }
    }

}
#endif