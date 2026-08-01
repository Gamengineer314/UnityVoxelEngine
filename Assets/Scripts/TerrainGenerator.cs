using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

// Example terrain generator
[BurstCompile]
public class TerrainGenerator : MonoBehaviour {
    [SerializeField] private float amplitude = 80;
    [SerializeField] private float period = 500;
    [SerializeField] private int idHeight = 50;
    [SerializeField] private GameObject treePrefab;

    [SerializeField] private Color32[] colors = {
        new(245, 245, 60, 25), // Sand
        new(15, 220, 0, 25), // Grass
        new(130, 130, 135, 15), // Stone
        new(235, 235, 235, 15) // Snow
    };

    public VoxelColumns GenerateTerrain() {
        Native2DArray<Voxel> heightMap = new(WorldManager.horizontalSize, WorldManager.horizontalSize, Allocator.Persistent);
        GenerateHeightMap(ref heightMap, new NativeArray<Color32>(colors, Allocator.Temp), amplitude, period, idHeight);
        VoxelColumns voxels = new(heightMap, float3.zero);
        heightMap.Dispose();
        return voxels;
    }

    public void InstantiateTrees() {
        for (float x = period / 4; x < WorldManager.horizontalSize; x += period) {
            for (float z = period / 4; z < WorldManager.horizontalSize; z += period) {
                Instantiate(treePrefab, new Vector3(x, 2 * amplitude, z), Quaternion.identity);
                Instantiate(treePrefab, new Vector3(x + period / 2, 2 * amplitude, z + period / 2), Quaternion.identity);
            }
        }
    }

    [BurstCompile]
    private static void GenerateHeightMap(ref Native2DArray<Voxel> heightMap, in NativeArray<Color32> colors, float amplitude, float period, int idHeight) {
        // Sine height map
        for (int z = 0; z < heightMap.sizeY; z++) {
            for (int x = 0; x < heightMap.sizeX; x++) {
                int height = 1 + (int)(amplitude * (math.sin(2 * math.PI * x / period) * math.sin(2 * math.PI * z / period) + 1));
                heightMap[x, z] = new(height, colors[height / idHeight]);
            }
        }
    }
}
