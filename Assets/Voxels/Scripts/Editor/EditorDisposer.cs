#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorDisposer {
        public static List<IDisposable> disposables = new();

        static EditorDisposer() {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (VoxelRenderer renderer in UnityEngine.Object.FindObjectsOfType<VoxelRenderer>()) {
                    if (renderer.isActiveAndEnabled) renderer.OnDestroy();
                }
                foreach (IDisposable disposable in disposables) {
                    disposable.Dispose();
                }
            };
            AssemblyReloadEvents.afterAssemblyReload += () => {
                foreach (VoxelRenderer renderer in UnityEngine.Object.FindObjectsOfType<VoxelRenderer>()) {
                    if (renderer.isActiveAndEnabled) renderer.Awake();
                }
                foreach (VoxelMesh mesh in UnityEngine.Object.FindObjectsOfType<VoxelMesh>(true)) {
                    mesh.Start();
                }
            };
        }
    }

}
#endif