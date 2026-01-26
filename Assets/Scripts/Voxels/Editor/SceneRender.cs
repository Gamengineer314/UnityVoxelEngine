#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Voxels.Editor {
    
    [InitializeOnLoad]
    internal class SceneRender {
        private static readonly Dictionary<Object, SceneRender> renders = new();

        public enum Mode { All = 0, None = 1, Renderer = 2 };
        public readonly Mode mode;
        public readonly Object renderer;

        private SceneRender(Mode mode, Object renderer) {
            this.mode = mode;
            this.renderer = renderer;
        }

        private SceneRender(Mode mode) : this(mode, null) {}


        static SceneRender() {
            AssemblyReloadEvents.beforeAssemblyReload += RemoveAll;
        }

        private static void RemoveAll() {
            Object[] renderables = renders.Keys.ToArray();
            foreach (Object renderable in renderables) {
                Remove(renderable);
            }
        }


        public static SceneRender Get(Object renderable) {
            if (renders.ContainsKey(renderable)) return renders[renderable];
            else {
                int id = renderable.GetInstanceID();
                Mode mode = (Mode)EditorPrefs.GetInt($"SceneRender_{id}_Mode", 0);
                SceneRender render = mode == Mode.Renderer ?
                    new(mode, EditorUtility.InstanceIDToObject(EditorPrefs.GetInt($"SceneRender_{id}_Renderer"))) :
                    new(mode);
                renders[renderable] = render;
                return render;
            }
        }

        public static void Remove(Object renderable) {
            int id = renderable.GetInstanceID();
            if (renderable == null) {
                EditorPrefs.DeleteKey($"SceneRender_{id}_Mode");
                EditorPrefs.DeleteKey($"SceneRender_{id}_Renderer");
            }
            else if (renders.TryGetValue(renderable, out SceneRender render)) {
                EditorPrefs.SetInt($"SceneRender_{id}_Mode", (int)render.mode);
                if (render.mode == Mode.Renderer) {
                    EditorPrefs.SetInt($"SceneRender_{id}_Renderer", render.renderer.GetInstanceID());
                }
            }
            renders.Remove(renderable);
        }


        public static void InspectorRenderSelection(Object renderable, Object[] renderers) {
            SceneRender render = Get(renderable);

            int renderIndex = -1;
            string[] options = new string[2 + renderers.Length];
            options[0] = "All";
            options[1] = "None";
            for (int i = 0; i < renderers.Length; i++) {
                options[2 + i] = renderers[i].name;
                if (renderers[i] == render.renderer) renderIndex = i;
            }
            if (render.mode == Mode.Renderer) {
                if (renderIndex == -1) renderIndex = 0;
                else renderIndex += 2;
            }
            else renderIndex = (int)render.mode;

            int chosenIndex = EditorGUILayout.Popup(new GUIContent("Scene Render"), renderIndex, options);
            if (chosenIndex < 2) render = new((Mode)chosenIndex);
            else render = new(Mode.Renderer, renderers[chosenIndex - 2]);
            renders[renderable] = render;
        }
    }

}

#endif