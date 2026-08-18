#if UNITY_EDITOR
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [CustomEditor(typeof(VoxelMesh)), CanEditMultipleObjects]
    public class VoxelMeshEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) {
                foreach (UnityEngine.Object target in targets) {
                    ((VoxelMesh)target).OnInspectorChanged();
                }
            }
        }
    }

}
#endif