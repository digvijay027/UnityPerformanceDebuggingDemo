using System.Collections.Generic;
using UnityEngine;
using PerformanceDemo.Core;
using Unity.Profiling;

namespace PerformanceDemo.Performance
{
    /// <summary>
    /// REUSABLE OBJECT POOL COMPONENT:
    /// 
    /// [WHY INSTANTIATE / DESTROY IS A PERFORMANCE BOTTLENECK]:
    /// 1. Engine Overhead: GameObject.Instantiate allocates native C++ engine objects,
    ///    constructs managed C# wrapper objects, registers components with internal engine
    ///    systems (Physics, Transform hierarchy, Rendering), and deserializes prefab data.
    /// 2. Heap Garbage: Destroy() leaves managed references for garbage collection and
    ///    triggers native deallocation/cleanup passes, inducing GC spikes and memory fragmentation.
    /// 3. In high-churn loops (e.g., 10-20 projectiles/sec, 75 enemies cycling), these continuous
    ///    allocations degrade Main Thread frame time and cause recurrent micro-stutter.
    /// 
    /// [HOW OBJECT POOLING ELIMINATES LIFECYCLE OVERHEAD]:
    /// - Pre-allocates a fixed capacity of instances during initialization (amortizing allocation cost).
    /// - Toggles GameObject.SetActive(true/false) instead of creating/destroying entities.
    /// - Reuses existing memory buffers and transforms across infinite gameplay iterations.
    /// - Keeps steady-state Instantiate() and Destroy() calls at exactly 0.
    /// 
    /// [POOL EXPANSION / EXHAUSTION HANDLING]:
    /// - If demand exceeds initial capacity, the pool dynamically expands by instantiating
    ///   new instances on demand to ensure uninterrupted gameplay.
    /// - Once steady-state is reached, no further instantiations occur.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialCapacity = 30;
        [SerializeField] private bool allowDynamicExpansion = true;
        [SerializeField] private int maxCapacity = 150;

        [Header("Diagnostic Tracking")]
        [SerializeField] private int totalCreatedCount;
        [SerializeField] private int activeCount;
        [SerializeField] private long totalReuseCount;

        private readonly Queue<GameObject> availableQueue = new Queue<GameObject>();
        private readonly HashSet<GameObject> activeObjects = new HashSet<GameObject>();
        private Transform poolContainer;

        // Profiler markers for exact instrumentation in the Unity Profiler hierarchy
        private static readonly ProfilerMarker s_PoolGetMarker = new ProfilerMarker("ObjectPool.Get");
        private static readonly ProfilerMarker s_PoolReturnMarker = new ProfilerMarker("ObjectPool.Return");
        private static readonly ProfilerMarker s_PoolExpandMarker = new ProfilerMarker("ObjectPool.ExpandInstance");

        public GameObject Prefab => prefab;
        public int TotalCreatedCount => totalCreatedCount;
        public int ActiveCount => activeCount;
        public long TotalReuseCount => totalReuseCount;
        public int AvailableCount => availableQueue.Count;

        public void Initialize(GameObject poolPrefab, int capacity, string containerName = null)
        {
            prefab = poolPrefab;
            initialCapacity = capacity;

            if (poolContainer == null)
            {
                string cName = containerName ?? $"[Pool]_{prefab.name}";
                GameObject containerObj = new GameObject(cName);
                containerObj.transform.SetParent(transform);
                poolContainer = containerObj.transform;
            }

            Prewarm(initialCapacity);
        }

        private void Awake()
        {
            if (prefab != null && totalCreatedCount == 0)
            {
                Initialize(prefab, initialCapacity);
            }
        }

        /// <summary>
        /// Pre-warms the pool by instantiating instances upfront to avoid runtime stalls.
        /// </summary>
        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CreateNewInstance();
            }
        }

        private GameObject CreateNewInstance()
        {
            using (s_PoolExpandMarker.Auto())
            {
                GameObject obj = Instantiate(prefab, poolContainer);
                obj.name = $"{prefab.name}_{totalCreatedCount}";
                obj.SetActive(false);
                availableQueue.Enqueue(obj);
                totalCreatedCount++;
                return obj;
            }
        }

        /// <summary>
        /// Retrieves an inactive instance from the pool, positions/rotates it,
        /// and invokes IPoolable.OnSpawnFromPool() on all attached components.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            using (s_PoolGetMarker.Auto())
            {
                GameObject obj = null;

                while (availableQueue.Count > 0)
                {
                    obj = availableQueue.Dequeue();
                    if (obj != null) break;
                }

                // Handle pool exhaustion: dynamically expand if permitted
                if (obj == null)
                {
                    if (allowDynamicExpansion && (maxCapacity <= 0 || totalCreatedCount < maxCapacity))
                    {
                        Debug.Log($"[ObjectPool] Capacity reached for {prefab.name}. Dynamically expanding pool to {totalCreatedCount + 1}.");
                        obj = CreateNewInstance();
                        availableQueue.Dequeue(); // remove from queue for immediate use
                    }
                    else
                    {
                        Debug.LogWarning($"[ObjectPool] Exhausted capacity for {prefab.name} and expansion limit reached ({maxCapacity}).");
                        return null;
                    }
                }

                // Place and activate
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);

                activeObjects.Add(obj);
                activeCount = activeObjects.Count;
                totalReuseCount++;

                // Reset state through IPoolable
                IPoolable[] poolables = obj.GetComponentsInChildren<IPoolable>(true);
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnSpawnFromPool();
                }

                return obj;
            }
        }

        /// <summary>
        /// Deactivates and returns an active instance to the pool, invoking
        /// IPoolable.OnReturnToPool() to guarantee state cleanup.
        /// </summary>
        public void Return(GameObject obj)
        {
            using (s_PoolReturnMarker.Auto())
            {
                if (obj == null) return;

                // Prevent double-return bug
                if (!activeObjects.Contains(obj) && !obj.activeSelf)
                {
                    return;
                }

                // Invoke cleanup hooks
                IPoolable[] poolables = obj.GetComponentsInChildren<IPoolable>(true);
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnReturnToPool();
                }

                activeObjects.Remove(obj);
                activeCount = activeObjects.Count;

                obj.SetActive(false);
                if (poolContainer != null)
                {
                    obj.transform.SetParent(poolContainer);
                }

                availableQueue.Enqueue(obj);
            }
        }
    }
}
