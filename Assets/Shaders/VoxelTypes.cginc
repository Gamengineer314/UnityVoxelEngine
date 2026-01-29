struct VoxelMesh {
    float3 center;
    float3 size;
    float3 position;
    uint data1; // normal (3b), faceCount (29b)
    uint startFace;
};

#define MESH_NORMAL(mesh) ((mesh).data1 & 7)
#define MESH_COUNT(mesh) ((mesh).data1 >> 3)


struct VoxelFace {
    uint data1; // x (10b), y (10b), z (10b)
    uint data2; // width (6b), height (6b), normal (3b), color (16b)
};

#define FACE_XYZ(face) uint3((face).data1 & 0x3FFu, (face).data1 >> 10 & 0x3FFu, (face).data1 >> 20)
#define FACE_WIDTH(face) (((face).data2 & 0x3Fu) + 1)
#define FACE_HEIGHT(face) (((face).data2 >> 6 & 0x3Fu) + 1)
#define FACE_NORMAL(face) ((face).data2 >> 12 & 7u)
#define FACE_COLOR(face) ((face).data2 >> 16)


#define NORMAL_POSITIVE(normal) ((normal) & 1u)
#define NORMAL_NEGATIVE(normal) (~(normal) & 1u)
#define NORMAL_SIGN(normal) (2 * int(NORMAL_POSITIVE(normal)) - 1)
#define NORMAL_AXIS(normal) ((normal) >> 1)
#define NORMAL_ANY 6