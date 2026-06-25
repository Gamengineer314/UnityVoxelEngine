Shader "Voxels/Terrain" {
    Properties {
        
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Voxels/VoxelShader.cginc"

            struct v2f {
                VoxelV2F voxel;
                float3 voxelPos : TEXCOORD0;
            };

            // Random value between 0 and 1
            uniform float seed;
            float random(uint3 pos) {
                float3 vec = frac(float3(pos) * 0.1031 + seed);
                vec += dot(vec, vec.yzx + 33.33);
                return frac((vec.x + vec.y) * vec.z);
            }

            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
                VoxelData v = unpackVertex(vertexID, instanceID);
                v2f o;
                o.voxel = voxelVertex(v);
                o.voxelPos = v.pos - getNormal(v.normalID) * 0.5;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 color = voxelFragment(i.voxel);
                uint3 pos = (uint3)i.voxelPos;
                color.xyz *= 1 + color.w * ((round(random(pos) * 8) / 8) - 0.5); // Random slight color variation
                color.w = 1;
                return color;
            }
            ENDCG
        }
    }
}