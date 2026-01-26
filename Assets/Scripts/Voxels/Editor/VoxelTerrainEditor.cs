#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Linq;

namespace Voxels.Editor {

    [CustomEditor(typeof(VoxelTerrain)), CanEditMultipleObjects]
    internal class VoxelTerrainEditor : UnityEditor.Editor {
        private void OnDestroy() {
            SceneRender.Remove(target);
        }

        public override void OnInspectorGUI() {
            VoxelTerrain terrain = (VoxelTerrain)target;
            VoxelTerrainRenderer[] renderers = FindObjectsOfType<VoxelTerrainRenderer>(true)
                .Where(renderer => renderer.terrain == terrain).ToArray();

            DrawDefaultInspector();
            EditorGUILayout.Space();
            SceneRender.InspectorRenderSelection(terrain, renderers);
        }
    }

}
#endif