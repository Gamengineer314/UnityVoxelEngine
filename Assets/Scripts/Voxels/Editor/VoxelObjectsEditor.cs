#if UNITY_EDITOR
using UnityEditor;
using Voxels.Rendering;
using System.Linq;

namespace Voxels.Editor {

    [CustomEditor(typeof(VoxelObjects)), CanEditMultipleObjects]
    internal class VoxelObjectsEditor : UnityEditor.Editor {
        private void OnDestroy() {
            SceneRender.Remove(target);
        }

        public override void OnInspectorGUI() {
            VoxelObjects objects = (VoxelObjects)target;
            VoxelObjectRenderer[] renderers = FindObjectsOfType<VoxelObjectRenderer>(true)
                .Where(renderer => renderer.objects == objects).ToArray();

            DrawDefaultInspector();
            EditorGUILayout.Space();
            SceneRender.InspectorRenderSelection(objects, renderers);
        }
    }

}
#endif