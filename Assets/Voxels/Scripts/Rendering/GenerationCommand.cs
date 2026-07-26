using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Generation command for the mesh generator
    /// </summary>
    public readonly struct GenerationCommand {
        public readonly VoxelColumns voxels;
        public readonly int chunkSize;
        public readonly int mergeNormalsThreshold;
        public readonly bool seenFromAbove;
        public readonly bool textured;

        public GenerationCommand(VoxelColumns voxels, int chunkSize, int mergeNormalsThreshold, bool seenFromAbove, bool textured) {
            this.voxels = voxels;
            this.chunkSize = chunkSize;
            this.mergeNormalsThreshold = mergeNormalsThreshold;
            this.seenFromAbove = seenFromAbove;
            this.textured = textured;
        }

        public GenerationCommand(VoxelColumns voxels, GenerationParameters parameters, Material material) {
            this.voxels = voxels;
            chunkSize = parameters.chunkSize;
            mergeNormalsThreshold = parameters.mergeNormalsThreshold;
            seenFromAbove = parameters.seenFromAbove;
            textured = material.IsKeywordEnabled(ShaderID.shaderTexture);
        }
    }

}