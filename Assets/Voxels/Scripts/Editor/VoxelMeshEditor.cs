#if UNITY_EDITOR
using UnityEditor;
using Voxels.Rendering;

namespace Voxels.Editor {

    [CustomEditor(typeof(VoxelMesh))]
    public class VoxelMeshEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) ((VoxelMesh)target).OnInspectorChanged();
        }
    }

}
#endif