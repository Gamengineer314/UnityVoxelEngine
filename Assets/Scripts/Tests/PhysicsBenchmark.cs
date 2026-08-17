using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Physics;
using Random = UnityEngine.Random;

public class PhysicsBenchmark : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI result;
    [SerializeField] private GameObject[] colliders;
    [SerializeField] private int nColliders;
    [SerializeField] private float maxPosition;
    [SerializeField] private float maxScale;
    [SerializeField] private int nRaycasts;

    private Benchmark benchmark;

  
    private void Start() {
        InstantiatePrefabs();
        benchmark = new Benchmark();
        benchmark.Add(() => BenchmarkRaycast(), "Raycast", 5);
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.B))
            result.text = benchmark.Run() + "\n" + BenchmarkRaycast();
    }


    private void InstantiatePrefabs() {
        Random.InitState(314);
        for (int i = 0; i < nColliders; i++) {
            GameObject collider = colliders[Random.Range(0, colliders.Length)];
            Vector3 position = new(
                Random.Range(0, maxPosition),
                Random.Range(0, maxPosition),
                Random.Range(0, maxPosition)
            );
            Vector3 scale = new(
                collider.transform.localScale.x * LogRange(1, maxScale),
                collider.transform.localScale.y * LogRange(1, maxScale),
                collider.transform.localScale.z * LogRange(1, maxScale)
            );
            Vector3 angles = new(
                90 * Random.Range(0, 4),
                90 * Random.Range(0, 4),
                90 * Random.Range(0, 4)
            );
            GameObject instance = Instantiate(collider, position, Quaternion.Euler(angles));
            instance.transform.localScale = scale;
        }
    }


    private int BenchmarkRaycast() {
        int h = 0;
        Random.InitState(314);
        for (int i = 0; i < nRaycasts; i++) {
            Vector3 origin = new(
                Random.Range(0, maxPosition),
                Random.Range(0, maxPosition),
                Random.Range(0, maxPosition)
            );
            Vector3 direction = Random.onUnitSphere;
            float maxDistance = LogRange(1, maxPosition);
            bool hit = VoxelPhysics.Instance.Raycast(new Ray(origin, direction), maxDistance, -1, out VoxelRaycastHit info);
            h = HashAdd(h, hit);
            h = HashAdd(h, info.point);
            h = HashAdd(h, info.normal);
        }
        return h;
    }


    private float LogRange(float min, float max)
        => Mathf.Exp(Random.Range(Mathf.Log(min), Mathf.Log(max)));

    private int HashAdd<T>(int h, T x)
        => h * 31 + x.GetHashCode();
}