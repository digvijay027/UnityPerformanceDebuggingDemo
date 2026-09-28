using UnityEngine;

namespace PerformanceDemo.Performance
{
    /// <summary>
    /// Centralized settings for configuring intentional baseline workloads,
    /// high object churn, enemy population, GC allocations, and Phase 4 optimizations.
    /// </summary>
    public class PerformanceTestSettings : MonoBehaviour
    {
        public static PerformanceTestSettings Instance { get; private set; }

        [Header("Master Controls")]
        [Tooltip("When TRUE: Baseline Mode ON (scene-wide GameObject.FindGameObjectsWithTag queries).\nWhen FALSE: Baseline Mode OFF (Phase 4.4 EnemyRegistry active).")]
        [SerializeField] private bool baselineMode = true;

        [Tooltip("Legacy lifecycle toggle: when TRUE runs unpooled Instantiate/Destroy. Keep FALSE during Phase 4.4 to preserve Object Pooling.")]
        [SerializeField] private bool baselineLifecycleMode = false;

        [Tooltip("Legacy flag: forces full Phase 3 baseline (250 CPU iterations and 540 KB GC alloc/frame). Keep FALSE during Phase 4.4.")]
        [SerializeField] private bool legacyPhase3Baseline = false;

        [Header("Enemy Population")]
        [Tooltip("Target active enemy population during the stress test (50-100).")]
        [Range(10, 150)]
        [SerializeField] private int targetEnemyPopulation = 75;

        [Tooltip("Spawn interval in seconds when replenishing enemy population.")]
        [SerializeField] private float spawnInterval = 0.15f;

        [Header("Phase 4.1 Optimized CPU Workload Settings (ACTIVE)")]
        [Tooltip("Reduced math iterations in optimized mode.")]
        [Range(1, 50)]
        [SerializeField] private int optimizedCpuIterations = 15;

        [Tooltip("Frame cadence interval for calculation throttling (staggered across frames).")]
        [Range(1, 10)]
        [SerializeField] private int calculationIntervalFrames = 4;

        [Tooltip("Use static precomputed trigonometric lookup table (LUT) instead of repeated Mathf.Sin/Cos.")]
        [SerializeField] private bool useTrigLookupTable = true;

        [Header("Phase 4.2 GC Workload Settings (ACTIVE)")]
        [Tooltip("Enable managed allocations workload.")]
        [SerializeField] private bool enableManagedAllocations = true;

        [Tooltip("Number of telemetry samples processed per enemy per frame.")]
        [Range(0, 50)]
        [SerializeField] private int allocationCountPerEnemy = 10;

        [Header("Baseline Workload References (Historical)")]
        [SerializeField] private bool enableArtificialWorkload = true;
        [SerializeField] private int artificialWorkloadIterations = 250;

        [Header("Projectile & Churn Stress")]
        [Tooltip("Fire rate in stress test (seconds between shots).")]
        [SerializeField] private float stressFireRate = 0.08f;

        [Tooltip("Auto-fire during performance stress test.")]
        [SerializeField] private bool autoFire = true;

        // Public Properties
        public bool BaselineMode
        {
            get => baselineMode;
            set => baselineMode = value;
        }

        public bool BaselineLifecycleMode
        {
            get => baselineLifecycleMode;
            set => baselineLifecycleMode = value;
        }

        public bool LegacyPhase3Baseline
        {
            get => legacyPhase3Baseline;
            set => legacyPhase3Baseline = value;
        }

        public int TargetEnemyPopulation
        {
            get => targetEnemyPopulation;
            set => targetEnemyPopulation = value;
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set => spawnInterval = value;
        }

        public bool EnableArtificialWorkload => enableArtificialWorkload;
        public int ArtificialWorkloadIterations => artificialWorkloadIterations;

        // Optimized CPU workload properties (Phase 4.1 preserved)
        public int OptimizedCpuIterations => optimizedCpuIterations;
        public int CalculationIntervalFrames => calculationIntervalFrames;
        public bool UseTrigLookupTable => useTrigLookupTable;

        // GC workload properties (Phase 4.2 preserved)
        public bool EnableManagedAllocations => enableManagedAllocations;
        public int AllocationCountPerEnemy => allocationCountPerEnemy;

        public float ActiveFireRate => stressFireRate;
        public bool AutoFire => autoFire;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Hotkey 'B' toggles Baseline Mode (Tag Query vs EnemyRegistry)
            if (Input.GetKeyDown(KeyCode.B))
            {
                baselineMode = !baselineMode;
                Debug.Log($"[PerformanceTestSettings] Baseline Mode (Tag Query vs Registry) toggled: {baselineMode}");
            }
        }
    }
}
