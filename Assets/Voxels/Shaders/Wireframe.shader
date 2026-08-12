Shader "Voxels/Wireframe" {
    Properties {
        
    }
    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "VoxelShader.cginc"

            struct V2F {
                float4 vertex : SV_POSITION;
            };

            V2F vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
                VoxelData v = unpackVertex(vertexID, instanceID);
                V2F o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(v.position, 1));
                return o;
            }

            fixed4 frag(V2F i) : SV_Target {
                return fixed4(0, 0, 0, 1);
            }
            ENDCG
        }
    }
}
