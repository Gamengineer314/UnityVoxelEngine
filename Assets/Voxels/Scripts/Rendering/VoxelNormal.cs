using Unity.Mathematics;

namespace Voxels.Rendering {

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