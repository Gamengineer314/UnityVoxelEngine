using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Shader ID cache
    /// </summary>
    internal static class ShaderID {
        public static readonly int quadsInterleaving = Shader.PropertyToID("quadsInterleaving");
        public static readonly int cameraPosition = Shader.PropertyToID("cameraPosition");
        public static readonly int cameraFarPlane = Shader.PropertyToID("cameraFarPlane");
        public static readonly int cameraLeftPlane = Shader.PropertyToID("cameraLeftPlane");
        public static readonly int cameraRightPlane = Shader.PropertyToID("cameraRightPlane");
        public static readonly int cameraDownPlane = Shader.PropertyToID("cameraDownPlane");
        public static readonly int cameraUpPlane = Shader.PropertyToID("cameraUpPlane");
        public static readonly int meshes = Shader.PropertyToID("meshes");
        public static readonly int faces = Shader.PropertyToID("faces");
        public static readonly int colors = Shader.PropertyToID("colors");
        public static readonly int commands = Shader.PropertyToID("commands");
        public static readonly int offsets = Shader.PropertyToID("offsets");
        public static readonly int transforms = Shader.PropertyToID("transforms");
        public static readonly int renderedTransforms = Shader.PropertyToID("renderedTransforms");
    }

}