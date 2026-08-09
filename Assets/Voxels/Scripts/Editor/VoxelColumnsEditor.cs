#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Voxels.Collections;
using Voxels.Rendering;

namespace Voxels.Editor {
    
    [CustomEditor(typeof(VoxelColumnsAsset))]
    public class VoxelColumnsEditor : UnityEditor.Editor {
        private PreviewRenderUtility preview;
        private VoxelRenderer renderer;
        private Transform meshTransform;
        private Bounds meshBounds;
        private Vector2 previewDir;

        // Enable after OnEnable because Editor.OnEnable is called before ScriptableObject.OnEnable
        private void Enable() {
            VoxelColumnsAsset voxelsAsset = (VoxelColumnsAsset)target;
            VoxelColumns voxels = voxelsAsset.voxels;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int x = 0; x < voxels.sizeX; x++) {
                for (int z = 0; z < voxels.sizeZ; z++) {
                    minY = Mathf.Min(minY, voxels.GetMin(x, z));
                    maxY = Mathf.Max(maxY, voxels.GetMax(x, z));
                }
            }
            Vector3 size = new(voxels.sizeX, maxY - minY + 1, voxels.sizeZ);
            meshBounds = new Bounds((Vector3)voxels.offset + size / 2, size);
            previewDir = new Vector2(130, -20);

            preview = new PreviewRenderUtility();
            GameObject rendererObject = new("Renderer") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(rendererObject, preview.camera.gameObject.scene);
            renderer = rendererObject.AddComponent<VoxelRenderer>();
            GameObject voxelsObject = new("Voxels") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(voxelsObject, preview.camera.gameObject.scene);
            meshTransform = voxelsObject.transform;

            VoxelMesh mesh = voxelsObject.AddComponent<VoxelMesh>();
            mesh.voxelsAsset = voxelsAsset;
            mesh.parameters = AssetDatabase.LoadAssetAtPath<GenerationParameters>(Path.Combine("Assets", "Voxels", "Default.asset"));
            mesh.material = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine("Assets", "Voxels", "Shaders", "VoxelDefault.mat"));
            mesh.Generate();
            mesh.CompleteGeneration();
        }

        private void OnDisable() {
            preview?.Cleanup();
            preview = null;
        }

        public override bool HasPreviewGUI() => true;

        public override void OnPreviewGUI(Rect r, GUIStyle background) {
            if (preview is null) Enable();

            Event current = Event.current;
            Drag2D(r);
            if (current.type != EventType.Repaint) return;

            preview.BeginPreview(r, background);
            RenderPreview();
            preview.EndAndDrawPreview(r);
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height) {
            if (preview is null) Enable();
            preview.BeginStaticPreview(new Rect(0, 0, width, height));
            RenderPreview();
            return preview.EndStaticPreview();
        }

        private void RenderPreview() {
            float size = meshBounds.size.magnitude;
            preview.camera.nearClipPlane = 1.35f * size;
            preview.camera.farClipPlane = 2.45f * size;
            preview.camera.fieldOfView = 30;
            preview.camera.transform.SetPositionAndRotation(-Vector3.forward * (1.9f * size), Quaternion.identity);
            Quaternion rotation = Quaternion.Euler(previewDir.y, 0, 0) * Quaternion.Euler(0, previewDir.x, 0);
            Vector3 position = rotation * (-meshBounds.center);
            meshTransform.SetPositionAndRotation(position, rotation);
            renderer.Update();
            preview.Render();
        }


        // Copied from PreviewGUI.Drag2D that is internal
        private static readonly int sliderHash = "Slider".GetHashCode();
        private void Drag2D(Rect r) {
            int controlID = GUIUtility.GetControlID(sliderHash, FocusType.Passive);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlID)) {
                case EventType.MouseDown:
                    if (r.Contains(current.mousePosition) && r.width > 50f) {
                        GUIUtility.hotControl = controlID;
                        current.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID) {
                        previewDir -= 140 * current.delta / Mathf.Min(r.width, r.height);
                        current.Use();
                        GUI.changed = true;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID) {
                        GUIUtility.hotControl = 0;
                    }
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
            }
        }
    }

}
#endif