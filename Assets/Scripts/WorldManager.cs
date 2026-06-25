using UnityEngine;
using Voxels.Collections;
using Voxels.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

public class WorldManager : MonoBehaviour {
    public const int horizontalSize = 4096; // Number of blocks in x and z dimensions

    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private VoxelMesh terrain;
    [SerializeField] private Material terrainMaterial;
    private VoxelColumns voxels;

    private void Awake() {
        terrainMaterial.SetFloat("seed", Random.value);
    }

    private void Start() {
        // Generate terrain
        Stopwatch watch = Stopwatch.StartNew();
        voxels = terrainGenerator.GenerateTerrain();
        Debug.Log($"Terrain generated in {watch.ElapsedMilliseconds} ms");

        // Generate mesh
        watch.Restart();
        terrain.SetVoxels(voxels, Vector3.zero);
        terrain.CompleteGeneration();
        
        Debug.Log($"Mesh generated in {watch.ElapsedMilliseconds} ms");
    }

    private void OnDestroy() {
        voxels.Dispose();
    }
}
