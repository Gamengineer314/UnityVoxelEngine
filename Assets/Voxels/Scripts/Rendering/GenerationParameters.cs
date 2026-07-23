using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Parameters for the mesh generator
    /// </summary>
    [CreateAssetMenu(menuName = "Voxels/Generation Parameters")]
    public class GenerationParameters : ScriptableObject {
        [Tooltip("Max size for mesh chunks. Multiple chunks can be generated from the same voxel collection if it exceeds this size. The generator will perform best if [chunkSize] is a multiple of 64.")]
        public int chunkSize = 64;

        [Tooltip("Number of faces below which chunks at the same position with different normals must be merged together. Objects smaller than the threshold will use a single chunk but can't be partially culled based on normals.")]
        public int mergeNormalsThreshold = 1024;

        [Tooltip("Max horizontal size a generator job can process. Multiple jobs will be used to generate the chunks in parallel if a voxel collection exceeds this size. [jobHorizontalSize] should be a multiple of [chunkSize].")]
        public int jobHorizontalSize = 1024;

        [Tooltip("Whether objects can only be seen from above and inside its horizontal bounds. This allows to remove faces below the objects and on their sides.")]
        public bool seenFromAbove = false;
    }

}