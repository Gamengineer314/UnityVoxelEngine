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
    
}