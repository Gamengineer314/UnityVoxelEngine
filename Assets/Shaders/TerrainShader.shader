Shader "Unlit/VoxelShader" {
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "VoxelStructs.cginc"
            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"


            struct v2f {
                float4 vertex : SV_POSITION;
                float4 voxelData : TEXCOORD0; // x,y,z : voxel pos, w : light level
                nointerpolation fixed4 color : COLOR; // x,y,z: color, w: random variation ammount
            };

            static const uint faceLightLevels[] = {
                12, // x-
                12, // x+
                9,  // y-
                15, // y+
                12, // z-
                12, // z+
            };

            StructuredBuffer<VoxelFace> faces;
            StructuredBuffer<uint> colors;
            StructuredBuffer<CommandOffset> offsets;

            uniform float quadsInterleaving; // Remove 1 pixel gaps between triangles


            // Random value between 0 and 1
            uniform float seed;
            float random(uint3 pos) {
                float3 vec = frac(float3(pos) * 0.1031 + seed);
                vec += dot(vec, vec.yzx + 33.33);
                return frac((vec.x + vec.y) * vec.z);
            }


            v2f vert(uint vertexID: SV_VertexID) {
                // Get data
                uint faceID = vertexID >> 2;
                VoxelFace face = faces[faceID];
                vertexID &= 3;
                uint cmd = GetCommandID(0);
                float3 position = offsets[cmd].position;

                // Unpack data
                float3 cubePos = FACE_XYZ(face) + position;
                uint normalID = FACE_NORMAL(face);
                float width = FACE_WIDTH(face);
                float height = FACE_HEIGHT(face);
                uint colorID = FACE_COLOR(face);
                uint normalAxis = NORMAL_AXIS(normalID);

                // Position
                float pos[3] = { cubePos.x, cubePos.y, cubePos.z };
                float interleaving = distance(_WorldSpaceCameraPos, cubePos) * quadsInterleaving * 0.001f;
                pos[normalAxis] += NORMAL_POSITIVE(normalID);
                pos[1u & ~normalAxis] += -interleaving + ((uint(vertexID) & 1u) ^ uint(normalAxis != 0) ^ NORMAL_NEGATIVE(normalID)) * (width + 2 * interleaving);
                pos[2u & ~normalAxis] += -interleaving + (vertexID >> 1) * (height + 2 * interleaving);
                float normal[3] = { 0, 0, 0 };
                normal[normalAxis] = NORMAL_SIGN(normalID);

                // Unpack color
                uint color32 = colors[colorID];
                fixed4 color;
                color.r = (color32 & 0xFF) / 255.0;
                color.g = ((color32 >> 8) & 0xFF) / 255.0;
                color.b = ((color32 >> 16) & 0xFF) / 255.0;
                color.a = ((color32 >> 24) & 0xFF) / 255.0;

                // Output
                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(pos[0], pos[1], pos[2], 1));
                o.voxelData = float4(pos[0] - normal[0] * 0.5f, pos[1] - normal[1] * 0.5f, pos[2] - normal[2] * 0.5f, faceLightLevels[normalID]);
                o.color = color;
                return o;
            }


            fixed4 frag(v2f i) : SV_Target {
                uint3 pos = (uint3)i.voxelData.xyz;
                float lightLevel = i.voxelData.w;
                fixed4 color = i.color;
                color *= lightLevel / 15; // Light (depending on face directions, better lighting could be added)
                color *= 1 + color.w * ((round(random(pos) * 8) / 8) - 0.5); // Random slight color variation
                color.w = 1;
                return color;
            }
            ENDCG
        }
    }
}
