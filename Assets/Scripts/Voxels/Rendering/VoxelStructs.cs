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

        public VoxelFace(int3 pos, int width, int height, VoxelNormal normal, int color) {
            data1 = (uint)pos.x | (uint)pos.y << 10 | (uint)pos.z << 20;
            data2 = (uint)width - 1 | (uint)height - 1 << 6 | (uint)normal << 12 | (uint)color << 16;
        }

        public int X => (int)(data1 & 0x3FF);
        public int Y => (int)(data1 >> 10 & 0x3FF);
        public int Z => (int)(data1 >> 20);
        public int Width => (int)((data2 & 0x3F) + 1);
        public int Height => (int)((data2 >> 6 & 0x3F) + 1);
        public VoxelNormal Normal => (VoxelNormal)(data2 >> 12 & 7);
        public int Color => (int)(data2 >> 16);

        public override string ToString() => $"[({X} {Y} {Z}) ({Width} {Height}) {Normal} {Color}]";
    }


    /// <summary>
    /// Per-mesh data
    /// </summary>
    internal readonly struct VoxelMesh {
        public readonly float3 center;
        public readonly float3 size;
        public readonly float3 position;
        private readonly uint data1; // normal (3b), faceCount (29b)
        private readonly uint startFace;

        public VoxelMesh(float3 center, float3 size, float3 position, VoxelNormal normal, int faceCount, int startFace) {
            this.center = center;
            this.size = size;
            this.position = position;
            data1 = (uint)normal | ((uint)faceCount << 3);
            this.startFace = (uint)startFace;
        }

        public int StartFace => (int)startFace;
        public int FaceCount => (int)(data1 >> 3);
        public VoxelNormal Normal => (VoxelNormal)(data1 & 0b111);

        public override string ToString() => $"[{center} {size} {position} {Normal} {StartFace} {FaceCount}]";
    }


    /// <summary>
    /// Per-mesh data for objects
    /// </summary>
    internal readonly struct ObjectMesh {
        public readonly VoxelMesh mesh;
        private readonly uint startColor;
        private readonly uint startInstance;

        public ObjectMesh(
            float3 center, float3 size, float3 position, VoxelNormal normal, int faceCount, int startFace,
            int startColor, int startInstance
        ) {
            mesh = new(center, size, position, normal, faceCount, startFace);
            this.startColor = (uint)startColor;
            this.startInstance = (uint)startInstance;
        }

        public int StartColor => (int)startColor;
        public int StartInstance => (int)startInstance;
    }
    
    
    /// <summary>
    /// Per-command offsets
    /// </summary>
    internal readonly struct CommandOffset {
        public readonly float3 position;
        public readonly uint color;
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