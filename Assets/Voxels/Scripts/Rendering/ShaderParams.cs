using UnityEngine;

namespace Voxels.Rendering {
    /// <summary>
    /// Parameters for the voxel shader
    /// </summary>
    public readonly struct ShaderParameters {
        public readonly bool texture;
        public readonly bool transform;
        public readonly bool instance;

        /// <summary>
        /// Get the parameters of a voxel material
        /// </summary>
        /// <param name="material">The material</param>
        public ShaderParameters(Material material) {
            texture = material.IsKeywordEnabled(ShaderID.shaderTexture);
            transform = material.IsKeywordEnabled(ShaderID.shaderTransform);
            instance = material.IsKeywordEnabled(ShaderID.shaderInstance);
        }
    }
}