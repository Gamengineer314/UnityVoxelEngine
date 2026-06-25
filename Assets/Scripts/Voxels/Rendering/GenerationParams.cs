using System;

namespace Voxels.Rendering {

    /// <summary>
    /// Parameters for the mesh generator
    /// </summary>
    [Serializable]
    public struct GenerationParameters {
        public int chunkSize;
        public int mergeNormalsThreshold;
        public int jobHorizontalSize;
        public bool seenFromAbove;

        /// <summary>
        /// Create generation parameters
        /// </summary> 
        /// <param name="chunkSize">
        /// Max size for mesh chunks.
        /// Multiple chunks can be generated from the same voxel collection if it exceeds this size.
        /// The generator will perform best if [chunkSize] is a multiple of 64.
        /// </param>
        /// <param name="mergeNormalsThreshold">
        /// Number of faces below which chunks at the same position with different normals must be merged together.
        /// Objects smaller than the threshold will use a single chunk but can't be partially culled based on normals.
        /// </param>
        /// <param name="jobHorizontalSize">
        /// Max horizontal size a generator job can process.
        /// Multiple jobs will be used to generate the chunks in parallel if a voxel collection exceeds this size.
        /// The generator will perform best if [jobHorizontalSize] is a multiple of [chunkSize]
        /// </param>
        /// <param name="seenFromAbove">
        /// Whether objects can only be seen from above and inside its horizontal bounds.
        /// This allows to remove faces below the objects and on their sides.
        /// </param>
        public GenerationParameters(int chunkSize = 64, int mergeNormalsThreshold = 256, int jobHorizontalSize = 1024, bool seenFromAbove = false) {
            this.chunkSize = chunkSize;
            this.mergeNormalsThreshold = mergeNormalsThreshold;
            this.jobHorizontalSize = jobHorizontalSize;
            this.seenFromAbove = seenFromAbove;
        }

        public static GenerationParameters Default => new(64, 256, 1024, false);
    }

}