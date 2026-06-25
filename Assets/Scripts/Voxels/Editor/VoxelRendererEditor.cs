using UnityEngine;
using UnityEditor;
using Voxels.Rendering;

[CustomEditor(typeof(VoxelRenderer))]
public class VoxelRendererEditor : Editor {
    private SerializedProperty cullingShader;
    private SerializedProperty generationParameters;
    private SerializedProperty quadsInterleaving;

    private void OnEnable() {
        cullingShader = serializedObject.FindProperty("cullingShader");
        generationParameters = serializedObject.FindProperty("generationParameters");
        quadsInterleaving = serializedObject.FindProperty("quadsInterleaving");
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();

        EditorGUILayout.PropertyField(cullingShader);
        generationParameters.isExpanded = EditorGUILayout.Foldout(generationParameters.isExpanded, "Generation Parameters", true, EditorStyles.foldoutHeader);
        if (generationParameters.isExpanded) {
            for (int layer = 0; layer < 32; layer++) {
                string layerName = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(layerName)) continue;
                SerializedProperty element = generationParameters.GetArrayElementAtIndex(layer);
                EditorGUILayout.PropertyField(element, new GUIContent(layerName), true);
            }
        }
        EditorGUILayout.PropertyField(quadsInterleaving);

        serializedObject.ApplyModifiedProperties();
    }
}