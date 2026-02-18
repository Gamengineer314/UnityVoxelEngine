#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorDisposer {
        static EditorDisposer() {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Object.FindObjectsOfType<VoxelRenderer>()) renderer.OnDestroy();
                foreach (VoxelTerrainLayer layer in Object.FindObjectsOfType<VoxelTerrainLayer>()) layer.OnDestroy();
                foreach (VoxelObjectLayer layer in Object.FindObjectsOfType<VoxelObjectLayer>()) layer.OnDestroy();
            };
            AssemblyReloadEvents.afterAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Object.FindObjectsOfType<VoxelRenderer>()) renderer.Awake();
                foreach (VoxelTerrainLayer layer in Object.FindObjectsOfType<VoxelTerrainLayer>()) layer.Awake();
                foreach (VoxelObjectLayer layer in Object.FindObjectsOfType<VoxelObjectLayer>()) layer.Awake();
                foreach (VoxelObject voxelObject in Object.FindObjectsOfType<VoxelObject>()) voxelObject.Start();
            };
        }
    }

}
#endif