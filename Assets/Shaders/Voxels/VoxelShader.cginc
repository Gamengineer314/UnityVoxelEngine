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
    float3 position; // World position
    uint normalID; // Face normal
    uint width; // Face size
    uint height;
    uint lightLevel;
    float3 tangent1;
    float3 tangent2;
#ifdef _VOXEL_TEXTURE_ON
    float2 facePosition; // Position in the face
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
    float3 position = FACE_XYZ(face) + offset.position;
    uint width = FACE_WIDTH(face);
    uint height = FACE_HEIGHT(face);
    uint normalID = FACE_NORMAL(face);
    uint colorID = FACE_COLOR(face);
    uint normalAxis = NORMAL_AXIS(normalID);
    uint widthAxis = AXIS_WIDTH(normalAxis);
    uint heightAxis = AXIS_HEIGHT(normalAxis);

    // Position
    uint vertexID1 = vertexID & 1u;
    uint vertexID2 = vertexID >> 1;
    uint revertWidth = uint(normalAxis != 0) ^ NORMAL_NEGATIVE(normalID);
    uint incWidth = vertexID1 ^ revertWidth;
    uint incHeight = vertexID2;
    float positionArr[3] = { position.x, position.y, position.z };
    positionArr[normalAxis] += NORMAL_POSITIVE(normalID);
    if (incWidth) positionArr[widthAxis] += width;
    if (incHeight) positionArr[heightAxis] += height;
    position = float3(positionArr[0], positionArr[1], positionArr[2]);

    // Interleaving
    float tangent1Arr[3] = { 0, 0, 0 };
    tangent1Arr[widthAxis] += 2 * (float)(vertexID2 ^ revertWidth) - 1;
    float3 tangent1 = float3(tangent1Arr[0], tangent1Arr[1], tangent1Arr[2]);
    float tangent2Arr[3] = { 0, 0, 0 };
    tangent2Arr[heightAxis] += 1 - 2 * (float)vertexID1;
    float3 tangent2 = float3(tangent2Arr[0], tangent2Arr[1], tangent2Arr[2]);

    // Transform
#ifdef _VOXEL_TRANSFORM_ON
#ifdef _VOXEL_INSTANCE_ON
    float4x4 transform = renderedTransforms[instanceID];
#else
    float4x4 transform = renderedTransforms[cmd];
#endif
    position = mul(transform, float4(position, 1)).xyz;
    tangent1 = mul(transform, float4(tangent1, 0)).xyz;
    tangent2 = mul(transform, float4(tangent2, 0)).xyz;
#endif

    VoxelData o;
    o.position = position;
    o.normalID = normalID;
    o.width = width;
    o.height = height;
    o.lightLevel = lightLevels[normalID];
    o.tangent1 = tangent1;
    o.tangent2 = tangent2;
#ifdef _VOXEL_TEXTURE_ON
    o.facePosition = uint2(incWidth, incHeight) * uint2(width, height);
    o.colorIndex = colorID + offset.color;
#else
    o.color = getColor(colors[colorID]);
#endif
    return o;
}


// Get vertex to fragment data
VoxelV2F voxelVertex(VoxelData d) {
    VoxelV2F o;
    o.vertex = float4(d.position, 1);
#ifdef _VOXEL_TEXTURE_ON
    o.uv = d.facePosition;
    o.texData = uint3(d.colorIndex, d.width, d.lightLevel);
#else
    o.color = d.color;
    o.color.xyz *= d.lightLevel / 15.0; // Simple lighting depending on the orientation
#endif
    o.vertex = mul(UNITY_MATRIX_VP, o.vertex);

    // Interleaving
    float4 extruded1 = o.vertex + mul(UNITY_MATRIX_VP, float4(d.tangent1, 0));
    extruded1 /= extruded1.w;
    float4 extruded2 = o.vertex + mul(UNITY_MATRIX_VP, float4(d.tangent2, 0));
    extruded2 /= extruded2.w;
    float2 vertex = o.vertex.xy / o.vertex.w;
    float2 tangent1 = extruded1.xy - vertex;
    float2 tangent2 = extruded2.xy - vertex;
    float2 normal1 = float2(tangent1.y, -tangent1.x);
    float2 normal2 = float2(tangent2.y, -tangent2.x);
    normal1 /= length(normal1 * _ScreenParams.xy);
    normal2 /= length(normal2 * _ScreenParams.xy);
    float2 extrude = (normal1 + normal2) * quadsInterleaving * 2 * o.vertex.w;
    o.vertex.xy += extrude;

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