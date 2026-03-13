Shader "Voxels/VoxelShader" {
    Properties {
        [KeywordEnum(Terrain, Objects)] Type("Type", int) = 0
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature TYPE_TERRAIN TYPE_OBJECTS
            
            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            #include "VoxelStructs.cginc"


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
            #ifdef TYPE_OBJECTS
            StructuredBuffer<float4x4> transforms;
            #endif

            uniform float quadsInterleaving; // Remove 1 pixel gaps between triangles


            struct v2f {
                float4 vertex : SV_POSITION;
            #ifdef TYPE_TERRAIN
                float3 voxelPos : TEXCOORD0;
                nointerpolation fixed4 color : COLOR; // x,y,z: color, w: random variation amount
            #endif
            #ifdef TYPE_OBJECTS
                float2 uv : TEXCOORD0;
                nointerpolation uint3 texData : TEXCOORD1; // x: offset, y: width, z: light level
            #endif
            };


            // Random value between 0 and 1
            uniform float seed;
            float random(uint3 pos) {
                float3 vec = frac(float3(pos) * 0.1031 + seed);
                vec += dot(vec, vec.yzx + 33.33);
                return frac((vec.x + vec.y) * vec.z);
            }


            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
                // Get data
            #if defined(SHADER_API_D3D11)
                vertexID += unity_IndirectDrawArgs.Load(GetCommandID(0) * 20 + 12);
            #endif
            #if defined(TYPE_OBJECTS) && !defined(SHADER_API_VULKAN)
                instanceID += unity_IndirectDrawArgs.Load(GetCommandID(0) * 20 + 16);
            #endif
                VoxelFace face = faces[vertexID >> 2];
                vertexID &= 3;
                CommandOffset offset = offsets[GetCommandID(0)];

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
                
                // Output
                v2f o;
                o.vertex = float4(pos, 1);
            #ifdef TYPE_TERRAIN
                o.voxelPos = pos - getNormal(normalID) * 0.5;
                o.color = getColor(colors[colorID]);
                o.color.xyz *= faceLightLevels[normalID] / 15.0; // Simple lighting depending on the orientation
            #endif
            #ifdef TYPE_OBJECTS
                o.vertex = mul(transforms[instanceID], o.vertex);
                o.uv = 0;
                if (incWidth) o.uv.x += width;
                if (incHeight) o.uv.y += height;
                o.texData = uint3(colorID + offset.color, width, faceLightLevels[normalID]);
            #endif
                o.vertex = mul(UNITY_MATRIX_VP, o.vertex);
                return o;
            }


            fixed4 frag(v2f i) : SV_Target {
                fixed4 color;
            #ifdef TYPE_TERRAIN
                uint3 pos = (uint3)i.voxelPos;
                color = i.color;
                color.xyz *= 1 + color.w * ((round(random(pos) * 8) / 8) - 0.5); // Random slight color variation
                color.w = 1;
            #endif
            #ifdef TYPE_OBJECTS
                uint2 uv = (uint2)i.uv;
                color = getColor(colors[i.texData.x + uv.y * i.texData.y + uv.x]);
                color.xyz *= i.texData.z / 15.0;
            #endif
                return color;
            }
            ENDCG
        }
    }
}
