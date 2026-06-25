#ifndef VOXEL_SHADER_CGINC
#define VOXEL_SHADER_CGINC

#if defined(_VOXEL_INSTANCE_ON)
#error Instancing isn't supported yet
#endif

#if defined(_VOXEL_INSTANCE_ON) && !defined(_VOXEL_TRANSFORM_ON)
#error _VOXEL_INSTANCE can't be enabled without _VOXEL_TRANSFORM
#endif

#include "UnityCG.cginc"
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#include "VoxelStructs.cginc"


// Light level depending on the normal
static const uint lightLevels[] = {
    12, // x-
    12, // x+
    9,  // y-
    15, // y+
    12, // z-
    12, // z+
};

// Buffers
StructuredBuffer<VoxelFace> faces;
StructuredBuffer<uint> colors;
StructuredBuffer<CommandOffset> offsets;
#ifdef _VOXEL_TRANSFORM_ON
StructuredBuffer<float4x4> renderedTransforms;
#endif

uniform float quadsInterleaving; // Remove 1 pixel gaps between triangles


// Unpacked data
struct VoxelData {
    float3 pos;
    uint normalID;
    uint width;
    uint height;
    uint lightLevel;
#ifdef _VOXEL_TEXTURE_ON
    float2 posInRect;
    uint colorIndex;
#else
    fixed4 color;
#endif
};

// Vertex to fragment data
struct VoxelV2F {
    float4 vertex : SV_POSITION;
#ifdef _VOXEL_TEXTURE_ON
    float2 uv : TEXCOORD0;
    nointerpolation uint3 texData : TEXCOORD1; // x: offset, y: width, z: light level
#else
    nointerpolation fixed4 color : COLOR;
#endif
};


// Get and unpack data from the buffers
VoxelData unpackVertex(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
    // Get data
    uint cmd = GetCommandID(0);
#ifdef SHADER_API_D3D11
    vertexID += unity_IndirectDrawArgs.Load(cmd * 20 + 12);
#endif
#if defined(_VOXEL_INSTANCE_ON) && !defined(SHADER_API_VULKAN)
    instanceID += unity_IndirectDrawArgs.Load(cmd * 20 + 16);
#endif
    VoxelFace face = faces[vertexID >> 2];
    vertexID &= 3;
    CommandOffset offset = offsets[cmd];

    // Unpack data
    float3 pos = FACE_XYZ(face) + offset.position;
    uint width = FACE_WIDTH(face);
    uint height = FACE_HEIGHT(face);
    uint normalID = FACE_NORMAL(face);
    uint colorID = FACE_COLOR(face);
    uint normalAxis = NORMAL_AXIS(normalID);
    uint widthAxis = AXIS_WIDTH(normalAxis);
    uint heightAxis = AXIS_HEIGHT(normalAxis);

    // Position
    uint incWidth = (uint(vertexID) & 1u) ^ uint(normalAxis != 0) ^ NORMAL_NEGATIVE(normalID);
    uint incHeight = vertexID >> 1;
    float posArr[3] = { pos.x, pos.y, pos.z };
    posArr[normalAxis] += NORMAL_POSITIVE(normalID);
    float interleaving = distance(_WorldSpaceCameraPos, pos) * quadsInterleaving * 0.001f;
    posArr[widthAxis] += -interleaving + incWidth * (width + 2 * interleaving);
    posArr[heightAxis] += -interleaving + incHeight * (height + 2 * interleaving);
    pos = float3(posArr[0], posArr[1], posArr[2]);
#ifdef _VOXEL_TRANSFORM_ON
#ifdef _VOXEL_INSTANCE_ON
    float4x4 transform = renderedTransforms[instanceID];
#else
    float4x4 transform = renderedTransforms[cmd];
#endif
    pos = mul(transform, float4(pos, 1)).xyz;
#endif
#ifdef _VOXEL_TRANSFORM_ON
    float2 posInRect = 0;
    if (incWidth) posInRect.x += width;
    if (incHeight) posInRect.y += height;
#endif

    VoxelData o;
    o.pos = pos;
    o.normalID = normalID;
    o.width = width;
    o.height = height;
    o.lightLevel = lightLevels[normalID];
#ifdef _VOXEL_TEXTURE_ON
    o.posInRect = posInRect;
    o.colorIndex = colorID + offset.color;
#else
    o.color = getColor(colors[colorID]);
#endif
    return o;
}


// Get vertex to fragment data
VoxelV2F voxelVertex(VoxelData d) {
    VoxelV2F o;
    o.vertex = float4(d.pos, 1);
#ifdef _VOXEL_TEXTURE_ON
    o.uv = d.posInRect;
    o.texData = uint3(d.colorIndex, d.width, d.lightLevel);
#else
    o.color = d.color;
    o.color.xyz *= d.lightLevel / 15.0; // Simple lighting depending on the orientation
#endif
    o.vertex = mul(UNITY_MATRIX_VP, o.vertex);
    return o;
}


// Get the color of a pixel
fixed4 voxelFragment(VoxelV2F i) {
#ifdef _VOXEL_TEXTURE_ON
    uint2 uv = (uint2)i.uv;
    fixed4 color = getColor(colors[i.texData.x + uv.y * i.texData.y + uv.x]);
    color.xyz *= i.texData.z / 15.0;
    return color;
#else
    return i.color;
#endif
}

#endif