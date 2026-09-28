using System.Collections.Generic;
using UnityEngine;

namespace PerformanceDemo.Performance
{
    /// <summary>
    /// ENEMY WORKLOAD COMPONENT:
    /// 
    /// [HISTORICAL BOTTLENECK ANALYSIS]:
    /// - Phase 3 Profiling identified two massive bottlenecks:
    ///   1. CPU Math Workload: 72.15 ms / frame (Resolved in Phase 4.1 via Cadence Throttling + Static LUT).
    ///   2. Managed GC Allocations: 548.2 KB / frame total, with 540 KB / frame (98.5%) originating
    ///      from ExecuteGcAllocations() across 75 active enemies.
    /// 
    /// [PHASE 4.2 GC ALLOCATION SOURCES IDENTIFIED]:
    ///   1. Vector3.ToString(): ~352.5 KB/frame (~4.7 KB per enemy) from position and forward string formatting.
    ///   2. string.Concat: ~79.5 KB/frame (~1.06 KB per enemy) from string building in the update loop.
    ///   3. new List<string>(4): ~60.0 KB/frame (~0.80 KB per enemy) heap-allocated collection instances.
    ///   4. Internal string[] array expansions: ~48.0 KB/frame (~0.64 KB per enemy).
    ///   Total: ~540 KB / frame of short-lived garbage causing recurrent GC spikes every 1.5 - 2.5s.
    /// 
    /// [PHASE 4.2 SENIOR-LEVEL OPTIMIZATIONS]:
    ///   1. Elimination of Per-Frame Strings: Removed Vector3.ToString() and string concatenations.
    ///   2. Elimination of Per-Frame Collections: Removed "new List<string>()" from the hot path.
    ///   3. Preallocated Reusable Data Buffer: Replaced heap collections with a preallocated
    ///      reusable struct buffer (EnemyTelemetry[]) sized to capacity.
    ///   4. Arithmetic State Updates: Workload processes state telemetry via deterministic value-type
    ///      structs and arithmetic checksum updates (0 bytes allocated).
    ///   5. Cached Static Reference: Reuses cached identifier string allocated once in Awake.
    /// 
    /// [PHASE 4.3 STATUS]:
    /// - CPU Optimization: UNTOUCHED & ACTIVE (15 iterations, 4-frame cadence, LUT).
    /// - GC Optimization: UNTOUCHED & ACTIVE (0 B heap allocation from workload).
    /// </summary>
    public class EnemyWorkload : MonoBehaviour
    {
        [Header("Per-Instance Overrides (0 = use PerformanceTestSettings)")]
        [Tooltip("Override math iterations per frame. If 0, uses PerformanceTestSettings.Instance value.")]
        [SerializeField] private int overrideWorkloadIterations = 0;

        [Tooltip("Override allocation count per frame. If 0, uses PerformanceTestSettings.Instance value.")]
        [SerializeField] private int overrideAllocationCount = 0;

        // Diagnostic outputs to prevent compiler dead-code elimination
        [HideInInspector] public float calculatedWorkloadResult;
        [HideInInspector] public string lastAllocatedString;
        [HideInInspector] public int telemetryChecksum;

        // Caching for Phase 4.1 CPU workload (UNTOUCHED)
        private float cachedOptimizedResult;

        // Static Precomputed Trigonometric Lookup Table (LUT) for Phase 4.1 (UNTOUCHED)
        private const int LutSize = 256;
        private const float LutScale = LutSize / (Mathf.PI * 2f);
        private static readonly float[] SinCosLut;

        // Phase 4.2: Reusable zero-allocation telemetry buffer (UNTOUCHED)
        public struct EnemyTelemetry
        {
            public int enemyId;
            public int sampleIndex;
            public Vector3 position;
            public Vector3 forward;
        }

        private readonly EnemyTelemetry[] reusableTelemetryBuffer = new EnemyTelemetry[32];
        private int cachedInstanceId;
        private string cachedStaticIdString;

        static EnemyWorkload()
        {
            SinCosLut = new float[LutSize];
            for (int i = 0; i < LutSize; i++)
            {
                float rad = (i / (float)LutSize) * Mathf.PI * 2f;
                SinCosLut[i] = Mathf.Sin(rad) * Mathf.Cos(rad);
            }
        }

        private void Awake()
        {
            cachedInstanceId = gameObject.GetInstanceID();
            cachedStaticIdString = "Enemy_" + cachedInstanceId;
            lastAllocatedString = cachedStaticIdString;
        }

        private void Update()
        {
            var settings = PerformanceTestSettings.Instance;

            // In Phase 4.3, CPU & GC optimizations remain active while lifecycle churn is toggled.
            // Only if legacyPhase3Baseline is explicitly enabled do we run the unoptimized Phase 3 CPU/GC workloads.
            bool isLegacyPhase3 = settings != null && settings.LegacyPhase3Baseline;

            // ---------------------------------------------------------
            // 1. CPU WORKLOAD EXECUTION (Phase 4.1 - UNCHANGED)
            // ---------------------------------------------------------
            if (isLegacyPhase3)
            {
                // Original unoptimized baseline CPU workload: 250 trig/sqrt iterations
                int baselineIterations = overrideWorkloadIterations > 0 
                    ? overrideWorkloadIterations 
                    : (settings != null ? settings.ArtificialWorkloadIterations : 250);

                if (baselineIterations > 0)
                {
                    ExecuteCpuWorkload(baselineIterations);
                }
            }
            else
            {
                // Phase 4.1 optimized CPU implementation: 15 iterations + 4-frame cadence + LUT
                int optIterations = settings != null ? settings.OptimizedCpuIterations : 15;
                int interval = settings != null ? settings.CalculationIntervalFrames : 4;
                bool useLut = settings == null || settings.UseTrigLookupTable;

                ExecuteOptimizedCpuWorkload(optIterations, interval, useLut);
            }

            // ---------------------------------------------------------
            // 2. MANAGED ALLOCATIONS WORKLOAD (Phase 4.2 - UNCHANGED)
            // ---------------------------------------------------------
            bool shouldRunAllocations = settings == null || settings.EnableManagedAllocations;
            int allocCount = overrideAllocationCount > 0 
                ? overrideAllocationCount 
                : (settings != null ? settings.AllocationCountPerEnemy : 10);

            if (shouldRunAllocations && allocCount > 0)
            {
                if (isLegacyPhase3)
                {
                    // Full legacy Phase 3 GC allocations
                    ExecuteGcAllocations(allocCount);
                }
                else
                {
                    // Phase 4.2 Zero-Allocation Optimized Workload (0 Bytes allocated)
                    ExecuteOptimizedGcWorkload(allocCount);
                }
            }
        }

        /// <summary>
        /// ORIGINAL UNOPTIMIZED BASELINE WORKLOAD (Phase 4.1 - UNCHANGED):
        /// 250 trigonometric and square-root calculations executed synchronously every frame.
        /// </summary>
        private void ExecuteCpuWorkload(int iterations)
        {
            float accumulator = 0f;
            Vector3 currentPos = transform.position;

            for (int i = 0; i < iterations; i++)
            {
                float offset = (i * 0.05f) + currentPos.x;
                accumulator += Mathf.Sin(offset) * Mathf.Cos(offset) + Mathf.Sqrt(i + 1f);
            }

            calculatedWorkloadResult = accumulator;
        }

        /// <summary>
        /// PHASE 4.1 OPTIMIZED CPU WORKLOAD (Phase 4.1 - UNCHANGED):
        /// Cadence Throttling + Static LUT + Iteration Reduction (15) + Inter-frame Caching.
        /// </summary>
        private void ExecuteOptimizedCpuWorkload(int iterations, int intervalFrames, bool useLut)
        {
            int instanceId = cachedInstanceId != 0 ? cachedInstanceId : gameObject.GetInstanceID();
            bool shouldRecalculate = intervalFrames <= 1 || ((instanceId + Time.frameCount) % intervalFrames) == 0;

            if (shouldRecalculate)
            {
                float accumulator = 0f;
                Vector3 currentPos = transform.position;

                for (int i = 0; i < iterations; i++)
                {
                    float offset = (i * 0.05f) + currentPos.x;

                    if (useLut)
                    {
                        int lutIndex = (int)(offset * LutScale) & (LutSize - 1);
                        accumulator += SinCosLut[lutIndex] + (i * 0.1f);
                    }
                    else
                    {
                        accumulator += Mathf.Sin(offset) * Mathf.Cos(offset) + (i * 0.1f);
                    }
                }

                cachedOptimizedResult = accumulator;
            }

            calculatedWorkloadResult = cachedOptimizedResult;
        }

        /// <summary>
        /// ORIGINAL UNOPTIMIZED MANAGED ALLOCATIONS (PRESERVED FOR BASELINE REFERENCE):
        /// Generates temporary strings, Vector3.ToString(), and new List<string>(4) allocations.
        /// Exactly reproduces the Phase 3 baseline allocation load (~540 KB / frame).
        /// </summary>
        private void ExecuteGcAllocations(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Managed string concatenation allocation
                string status = "Enemy_" + gameObject.GetInstanceID() + "_Idx_" + i + "_Pos_" + transform.position.ToString();

                // Managed generic List allocation
                List<string> frameLog = new List<string>(4);
                frameLog.Add(status);
                frameLog.Add(transform.forward.ToString());

                lastAllocatedString = frameLog[0];
            }
        }

        /// <summary>
        /// PHASE 4.2 OPTIMIZED ZERO-ALLOCATION WORKLOAD (UNCHANGED):
        /// Completely eliminates heap allocations in the hot path:
        /// - 0 temporary string allocations (no Vector3.ToString(), no string concatenation).
        /// - 0 temporary List instances (reuses preallocated struct buffer).
        /// - Preserves telemetry processing using deterministic value types and arithmetic checksums.
        /// - GC Alloc: 0 bytes per frame.
        /// </summary>
        private void ExecuteOptimizedGcWorkload(int count)
        {
            int clampedCount = Mathf.Min(count, reusableTelemetryBuffer.Length);
            Vector3 currentPos = transform.position;
            Vector3 forwardDir = transform.forward;
            int checksum = 0;

            for (int i = 0; i < clampedCount; i++)
            {
                // Reusable struct buffer - value type operations on the stack/heap array, zero new allocations
                reusableTelemetryBuffer[i].enemyId = cachedInstanceId;
                reusableTelemetryBuffer[i].sampleIndex = i;
                reusableTelemetryBuffer[i].position = currentPos;
                reusableTelemetryBuffer[i].forward = forwardDir;

                // Deterministic arithmetic state processing (simulating telemetry aggregation without heap churn)
                checksum ^= (cachedInstanceId * 397) ^ (i * 31) ^ (int)(currentPos.x * 100f);
            }

            telemetryChecksum = checksum;
            lastAllocatedString = cachedStaticIdString; // Reuses pre-cached string reference from Awake
        }
    }
}
