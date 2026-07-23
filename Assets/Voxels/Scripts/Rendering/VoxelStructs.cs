using Unity.Mathematics;

namespace Voxels.Rendering {

    /// <summary>
    /// Face of a voxel
    /// </summary>
    internal readonly struct VoxelFace {
        public const int maxSize = 1024;
        public const int maxColor = 65535;

        public readonly uint data1; // x (10b), y (10b), z (10b)
        public readonly uint data2; // width (6b), height (6b), normal (3b), color (16b)

        public VoxelFace(int3 position, int width, int height, VoxelNormal normal, int color) {
            data1 = (uint)position.x | (uint)position.y << 10 | (uint)position.z << 20;
            data2 = (uint)width - 1 | (uint)height - 1 << 6 | (uint)normal << 12 | (uint)color << 16;
        }

        public int X => (int)(data1 & 0x3FF);
        public int Y => (int)(data1 >> 10 & 0x3FF);
        public int Z => (int)(data1 >> 20);
        public int3 Position => new(X, Y, Z);
        public int Width => (int)((data2 & 0x3F) + 1);
        public int Height => (int)((data2 >> 6 & 0x3F) + 1);
        public VoxelNormal Normal => (VoxelNormal)(data2 >> 12 & 7);
        public int Color => (int)(data2 >> 16);

        public override string ToString() => $"[({X} {Y} {Z}) ({Width} {Height}) {Normal} {Color}]";
    }


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


    /// <summary>
    /// Normals for a cube
    /// </summary>
    public enum VoxelNormal {
        XNegative = 0,
        XPositive = 1,
        YNegative = 2,
        YPositive = 3,
        ZNegative = 4,
        ZPositive = 5,
        Any = 6,
        None = 7
    }


    /// <summary>
    /// Normals helper functions
    /// </summary>
    public static class VoxelNormals {
        /// <summary>
        /// Axis of a normal
        /// </summary>
        public static int Axis(VoxelNormal normal) => (int)((uint)normal >> 1);

        /// <summary>
        /// Whether a normal is positive or negative
        /// </summary>
        public static bool Positive(VoxelNormal normal) => ((int)normal & 1) == 1;

        // x: 1, y: 0, z: 1
        internal static int WidthAxis(VoxelNormal normal) => WidthAxis(Axis(normal));
        internal static int WidthAxis(int axis) => 1 & ~axis;

        // x: 2, y: 2, z: 0
        internal static int HeightAxis(VoxelNormal normal) => HeightAxis(Axis(normal));
        internal static int HeightAxis(int axis) => 2 & ~axis;
    }
    
}