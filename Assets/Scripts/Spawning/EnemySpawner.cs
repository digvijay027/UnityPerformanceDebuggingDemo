using UnityEngine;
using PerformanceDemo.Enemy;
using PerformanceDemo.Performance;
using Unity.Profiling;

namespace PerformanceDemo.Spawning
{
    /// <summary>
    /// Spawner responsible for sustaining target enemy population (75 enemies).
    /// Supports dual query modes:
    /// - Baseline Mode ON: Direct scene-wide GameObject.FindGameObjectsWithTag("Enemy") queries.
    /// - Baseline Mode OFF: Phase 4.4 maintained EnemyRegistry.ActiveEnemyCount (O(1) Zero-Alloc).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnInterval = 3f;
        [SerializeField] private float spawnRadius = 16f;
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private int maxEnemies = 20;

        private float nextSpawnTime;

        private static readonly ProfilerMarker s_SpawnerMarker = new ProfilerMarker("EnemySpawner.Update");
        private static readonly ProfilerMarker s_FindTagMarker = new ProfilerMarker("GameObject.FindGameObjectsWithTag");
        private static readonly ProfilerMarker s_RegistryLookupMarker = new ProfilerMarker("EnemyRegistry.Lookup");
        private static readonly ProfilerMarker s_SpawnEnemyMarker = new ProfilerMarker("EnemySpawner.SpawnEnemy");
        private static readonly ProfilerMarker s_InstantiateMarker = new ProfilerMarker("EnemySpawner.Instantiate");

        public GameObject EnemyPrefab
        {
            get => enemyPrefab;
            set => enemyPrefab = value;
        }

        public bool AutoSpawn
        {
            get => autoSpawn;
            set => autoSpawn = value;
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set => spawnInterval = value;
        }

        private void Start()
        {
            nextSpawnTime = Time.time + 0.5f;

            if (enemyPrefab != null && PoolManager.Instance != null)
            {
                var settings = PerformanceTestSettings.Instance;
                int cap = settings != null ? settings.TargetEnemyPopulation + 10 : 85;
                PoolManager.Instance.InitializeEnemyPool(enemyPrefab, cap);
            }
        }

        private void Update()
        {
            if (!autoSpawn || enemyPrefab == null) return;

            using (s_SpawnerMarker.Auto())
            {
                int targetPopulation = maxEnemies;
                float interval = spawnInterval;

                var settings = PerformanceTestSettings.Instance;
                if (settings != null)
                {
                    targetPopulation = settings.TargetEnemyPopulation;
                    interval = settings.SpawnInterval;
                }

                if (Time.time >= nextSpawnTime)
                {
                    int currentEnemyCount;
                    bool isBaselineQuery = settings == null || settings.BaselineMode;

                    if (isBaselineQuery)
                    {
                        // --- BASELINE MODE ON: SCENE-WIDE TAG QUERY (Unoptimized Bottleneck) ---
                        using (s_FindTagMarker.Auto())
                        {
                            currentEnemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;
                        }
                    }
                    else
                    {
                        // --- BASELINE MODE OFF: PHASE 4.4 MAINTAINED ENEMY REGISTRY (O(1) Zero-Alloc) ---
                        using (s_RegistryLookupMarker.Auto())
                        {
                            currentEnemyCount = EnemyRegistry.ActiveEnemyCount;
                        }
                    }

                    if (currentEnemyCount < targetPopulation)
                    {
                        // Spawn up to 2 per tick if below target to rapidly stabilize population
                        int toSpawn = Mathf.Min(2, targetPopulation - currentEnemyCount);
                        for (int i = 0; i < toSpawn; i++)
                        {
                            SpawnEnemy();
                        }
                    }

                    nextSpawnTime = Time.time + interval;
                }
            }
        }

        public GameObject SpawnEnemy()
        {
            using (s_SpawnEnemyMarker.Auto())
            {
                if (enemyPrefab == null)
                {
                    Debug.LogWarning("[EnemySpawner] Enemy prefab is not assigned!");
                    return null;
                }

                Vector2 circle = Random.insideUnitCircle.normalized * spawnRadius;
                Vector3 spawnPosition = transform.position + new Vector3(circle.x, 0.5f, circle.y);

                var settings = PerformanceTestSettings.Instance;
                bool isLegacyLifecycle = settings != null && settings.BaselineLifecycleMode;

                if (isLegacyLifecycle)
                {
                    using (s_InstantiateMarker.Auto())
                    {
                        GameObject spawned = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                        EnemyHealth health = spawned.GetComponent<EnemyHealth>();
                        if (health != null)
                        {
                            health.SetPooled(false);
                        }
                        return spawned;
                    }
                }
                else
                {
                    // Phase 4.3 Reusable Object Pooling active
                    if (PoolManager.Instance != null)
                    {
                        PoolManager.Instance.InitializeEnemyPool(enemyPrefab);
                        return PoolManager.Instance.GetEnemy(spawnPosition, Quaternion.identity);
                    }
                    else
                    {
                        return Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                    }
                }
            }
        }
    }
}
