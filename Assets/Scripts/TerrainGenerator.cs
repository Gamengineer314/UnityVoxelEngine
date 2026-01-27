using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

// Example terrain generator
[BurstCompile]
public class TerrainGenerator : MonoBehaviour {
    private const float amplitude = 80;
    private const float blockPeriod = 500;
    private const int idHeight = 50;

    private static readonly Color32[] colors = {
        new(245, 245, 60, 25), // Sand
        new(15, 220, 0, 25), // Grass
        new(130, 130, 135, 15), // Stone
        new(235, 235, 235, 15) // Snow
    };

    public VoxelColumns<Color32> GenerateTerrain() {
        Native2DArray<Voxel<Color32>> heightMap = new(WorldManager.horizontalSize, WorldManager.horizontalSize, Allocator.Persistent);
        GenerateHeightMap(ref heightMap);
        VoxelColumns<Color32> voxels = new(heightMap);
        heightMap.Dispose();
        return voxels;
    }

    [BurstCompile]
    private static void GenerateHeightMap(ref Native2DArray<Voxel<Color32>> heightMap) {
        // Sine height map
        for (int z = 0; z < WorldManager.horizontalSize; z++) {
            for (int x = 0; x < WorldManager.horizontalSize; x++) {
                int height = 1 + (int)(amplitude * (math.sin(2 * math.PI * x / blockPeriod) * math.sin(2 * math.PI * z / blockPeriod) + 1));
                heightMap[x, z] = new(height, colors[height / idHeight]);
            }
        }
    }
}
