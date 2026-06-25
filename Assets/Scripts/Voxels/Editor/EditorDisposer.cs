#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorDisposer {
        static EditorDisposer() {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Object.FindObjectsOfType<VoxelRenderer>()) {
                    if (renderer.isActiveAndEnabled) renderer.OnDestroy();
                }
            };
            AssemblyReloadEvents.afterAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Object.FindObjectsOfType<VoxelRenderer>()) {
                    if (renderer.isActiveAndEnabled) renderer.Awake();
                }
                foreach (VoxelMesh mesh in Object.FindObjectsOfType<VoxelMesh>()) {
                    if (mesh.isActiveAndEnabled) mesh.Start();
                }
            };
        }
    }

}
#endif