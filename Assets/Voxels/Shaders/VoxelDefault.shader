Shader "Voxels/Default" {
    Properties {
        [Toggle] _VOXEL_TEXTURE ("Texture", Float) = 0
        [Toggle] _VOXEL_TRANSFORM ("Transform", Float) = 0
        [Toggle] _VOXEL_INSTANCE ("Instance", Float) = 0
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma shader_feature_local _VOXEL_TEXTURE_ON
            #pragma shader_feature_local _VOXEL_TRANSFORM_ON
            #pragma shader_feature_local _VOXEL_INSTANCE_ON
            
            #include "VoxelShader.cginc"

            VoxelV2F vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
                VoxelData v = unpackVertex(vertexID, instanceID);
                return voxelVertex(v);
            }

            fixed4 frag(VoxelV2F i) : SV_Target {
                return voxelFragment(i);
            }
            ENDCG
        }
    }
}
