using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Parameters for the voxel shader
    /// </summary>
    public readonly struct ShaderParameters {
        public readonly bool textured;
        public readonly bool instanced;

        public ShaderParameters(bool textured, bool instanced) {
            this.textured = textured;
            this.instanced = instanced;
        }
    }

}