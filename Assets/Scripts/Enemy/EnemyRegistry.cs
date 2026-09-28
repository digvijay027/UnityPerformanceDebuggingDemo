using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace PerformanceDemo.Enemy
{
    /// <summary>
    /// ENEMY REGISTRY (Phase 4.4 Query / Lookup Optimization):
    /// 
    /// [WHY SCENE-WIDE LOOKUPS ARE A PERFORMANCE PROBLEM]:
    /// 1. Scene Traversal: GameObject.FindGameObjectsWithTag("Enemy") traverses the entire
    ///    active scene hierarchy, scaling with total scene object count (O(N)).
    /// 2. Heap Garbage: Every call allocates a new managed array (GameObject[]) on the heap,
    ///    generating persistent garbage collection pressure.
    /// 3. In high-frequency loops (e.g. spawner checks and HUD diagnostics), this causes
    ///    unnecessary Main Thread CPU spikes and GC allocation overhead.
    /// 
    /// [HOW THE MAINTAINED REGISTRY ELIMINATES LOOKUP OVERHEAD]:
    /// - Replaces O(N) scene scans with an O(1) in-memory HashSet<EnemyHealth>.
    /// - Zero managed heap allocations during steady-state gameplay (0 B/frame).
    /// - Enemies self-register upon activation/pool retrieval and unregister upon death/pool return.
    /// - Pool capacity (85) vs Active enemies (75) are strictly decoupled: inactive pooled
    ///   enemies are NOT in the registry.
    /// - Instant ActiveEnemyCount property allows EnemySpawner to query population in nanoseconds.
    /// </summary>
    public static class EnemyRegistry
    {
        private static readonly HashSet<EnemyHealth> activeEnemies = new HashSet<EnemyHealth>();

        private static readonly ProfilerMarker s_RegisterMarker = new ProfilerMarker("EnemyRegistry.Register");
        private static readonly ProfilerMarker s_UnregisterMarker = new ProfilerMarker("EnemyRegistry.Unregister");

        public static int ActiveEnemyCount => activeEnemies.Count;

        public static IReadOnlyCollection<EnemyHealth> ActiveEnemies => activeEnemies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            activeEnemies.Clear();
        }

        /// <summary>
        /// Registers an active enemy into the registry.
        /// Called when an enemy is retrieved from the pool or enabled.
        /// </summary>
        public static void Register(EnemyHealth enemy)
        {
            using (s_RegisterMarker.Auto())
            {
                if (enemy != null)
                {
                    activeEnemies.Add(enemy);
                }
            }
        }

        /// <summary>
        /// Unregisters an enemy from the registry.
        /// Called when an enemy dies, is deactivated, or returned to the pool.
        /// </summary>
        public static void Unregister(EnemyHealth enemy)
        {
            using (s_UnregisterMarker.Auto())
            {
                if (enemy != null)
                {
                    activeEnemies.Remove(enemy);
                }
            }
        }

        public static void Clear()
        {
            activeEnemies.Clear();
        }
    }
}
