using Unity.Mathematics;

namespace Voxels.Rendering {

    /// <summary>
    /// Per-chunk data
    /// </summary>
    internal readonly struct VoxelChunk {
        public readonly float3 center; // Center of the bounding box
        public readonly float3 size; // Half the size of the bounding box
        public readonly CommandOffset offset; // Position and color index (if using texture) offsets
        private readonly uint normal; // Normal of all the faces in the chunk
        private readonly uint startFace; // Index of the first face in the chunk in the faces buffer
        private readonly uint faceCount; // Number of faces in the chunk
        private readonly uint startInstance; // Index of the first instance of the chunk in the transforms buffer (if using instancing or transform)
        private readonly uint startRenderedInstance; // Index of the first instance of the chunks in the rendered transforms buffer (if using instancing)
        private readonly uint instanceCount; // Number of instances of the chunk (if using instancing)

        public VoxelChunk(float3 center, float3 size, float3 position, int startColor, VoxelNormal normal, int startFace, int faceCount, int startInstance, int startRenderedInstance, int instanceCount) {
            this.center = center;
            this.size = size;
            offset = new(position, startColor);
            this.normal = (uint)normal;
            this.startFace = (uint)startFace;
            this.faceCount = (uint)faceCount;
            this.startInstance = (uint)startInstance;
            this.startRenderedInstance = (uint)startRenderedInstance;
            this.instanceCount = (uint)instanceCount;
        }

        public VoxelNormal Normal => (VoxelNormal)normal;
        public int StartFace => (int)startFace;
        public int FaceCount => (int)faceCount;
        public int StartInstance => (int)startInstance;
        public int StartRenderedInstance => (int)startRenderedInstance;
        public int InstanceCount => (int)instanceCount;

        public override string ToString() => $"[{center} {size} {offset.position} {offset.Color} {Normal} {StartFace} {FaceCount} {startInstance} {startRenderedInstance} {instanceCount}]";
    }
    
    
    /// <summary>
    /// Per-command offsets
    /// </summary>
    internal readonly struct CommandOffset {
        public readonly float3 position;
        private readonly uint color;

        public CommandOffset(float3 position, int color) {
            this.position = position;
            this.color = (uint)color;
        }

        public int Color => (int)color;
    }
    
}