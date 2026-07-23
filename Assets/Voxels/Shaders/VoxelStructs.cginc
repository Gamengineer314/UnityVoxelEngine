#ifndef VOXEL_STRUCTS_CGINC
#define VOXEL_STRUCTS_CGINC

#include "UnityCG.cginc"

// C equivalent of VoxelStructs.cs

struct CommandOffset {
    float3 position;
    uint color;
};

struct VoxelChunk {
    float3 center;
    float3 size;
    CommandOffset offset;
    uint normal;
    uint startFace;
    uint faceCount;
    uint startInstance;
    uint startRenderedInstance;
    uint instanceCount;
};


struct VoxelFace {
    uint data1;
    uint data2;
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
#define AXIS_WIDTH(axis) (1u & ~(axis))
#define AXIS_HEIGHT(axis) (2u & ~(axis))


float3 getNormal(uint normalID) {
    float normalArr[] = { 0, 0, 0 };
    normalArr[NORMAL_AXIS(normalID)] = NORMAL_SIGN(normalID);
    return float3(normalArr[0], normalArr[1], normalArr[2]);
}

fixed4 getColor(uint color32) {
    return fixed4(
        (color32 & 0xFF) / 255.0,
        ((color32 >> 8) & 0xFF) / 255.0,
        ((color32 >> 16) & 0xFF) / 255.0,
        ((color32 >> 24) & 0xFF) / 255.0
    );
}

#endif