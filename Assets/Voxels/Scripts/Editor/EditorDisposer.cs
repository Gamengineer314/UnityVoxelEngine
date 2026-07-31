#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorDisposer {
        public static List<IDisposable> disposables = new();

        static EditorDisposer() {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Resources.FindObjectsOfTypeAll<VoxelRenderer>()) {
                    if (!EditorUtility.IsPersistent(renderer)) renderer.OnDestroy();
                }
                foreach (IDisposable disposable in disposables) {
                    disposable.Dispose();
                }
            };
            AssemblyReloadEvents.afterAssemblyReload += () => {
                foreach (VoxelRenderer renderer in Resources.FindObjectsOfTypeAll<VoxelRenderer>()) {
                    if (!EditorUtility.IsPersistent(renderer)) renderer.Awake();
                }
                foreach (VoxelMesh mesh in Resources.FindObjectsOfTypeAll<VoxelMesh>()) {
                    if (!EditorUtility.IsPersistent(mesh)) mesh.Start();
                }
            };
        }
    }

}
#endif