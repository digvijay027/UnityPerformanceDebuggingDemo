using UnityEngine;
using Unity.Profiling;
using PerformanceDemo.Combat;
using PerformanceDemo.Enemy;

namespace PerformanceDemo.Performance
{
    /// <summary>
    /// Precision profiling sampler for Phase 4.4.
    /// Uses Unity.Profiling.ProfilerRecorder to sample query markers,
    /// spawner update times, main thread frame times, and GC allocations.
    /// </summary>
    public class Phase43BenchmarkHelper : MonoBehaviour
    {
        public static Phase43BenchmarkHelper Instance { get; private set; }

        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private ProfilerRecorder spawnerUpdateRecorder;
        private ProfilerRecorder findTagRecorder;
        private ProfilerRecorder registryLookupRecorder;
        private ProfilerRecorder shootRecorder;
        private ProfilerRecorder poolGetRecorder;
        private ProfilerRecorder poolReturnRecorder;

        [Header("Live Metrics")]
        public float currentFps;
        public float avgMainThreadMs;
        public float avgGcAllocKb;
        public float avgSpawnerMs;
        public float avgFindTagMs;
        public float avgRegistryLookupMs;
        public long findTagSampleCount;
        public float avgShootMs;
        public int activeEnemies;
        public int activeProjectiles;
        public bool isBaselineMode;

        private int sampleCount;
        private const int MaxSamples = 120;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", MaxSamples);
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", MaxSamples);
            spawnerUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "EnemySpawner.Update", MaxSamples);
            findTagRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "GameObject.FindGameObjectsWithTag", MaxSamples);
            registryLookupRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "EnemyRegistry.Lookup", MaxSamples);
            shootRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PlayerShooting.Shoot", MaxSamples);
            poolGetRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "ObjectPool.Get", MaxSamples);
            poolReturnRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "ObjectPool.Return", MaxSamples);
        }

        private void OnDisable()
        {
            mainThreadRecorder.Dispose();
            gcAllocRecorder.Dispose();
            spawnerUpdateRecorder.Dispose();
            findTagRecorder.Dispose();
            registryLookupRecorder.Dispose();
            shootRecorder.Dispose();
            poolGetRecorder.Dispose();
            poolReturnRecorder.Dispose();
        }

        private void Update()
        {
            var settings = PerformanceTestSettings.Instance;
            isBaselineMode = settings == null || settings.BaselineMode;

            currentFps = 1.0f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            activeEnemies = isBaselineMode 
                ? GameObject.FindGameObjectsWithTag("Enemy").Length 
                : EnemyRegistry.ActiveEnemyCount;
            activeProjectiles = FindObjectsOfType<Projectile>().Length;

            sampleCount++;
            if (sampleCount >= 20)
            {
                avgMainThreadMs = GetRecorderAverageMs(mainThreadRecorder);
                avgGcAllocKb = GetRecorderAverageKb(gcAllocRecorder);
                avgSpawnerMs = GetRecorderAverageMs(spawnerUpdateRecorder);
                avgFindTagMs = GetRecorderAverageMs(findTagRecorder);
                avgRegistryLookupMs = GetRecorderAverageMs(registryLookupRecorder);
                findTagSampleCount = findTagRecorder.Valid ? findTagRecorder.Count : 0;
                avgShootMs = GetRecorderAverageMs(shootRecorder);
            }
        }

        private float GetRecorderAverageMs(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0f;
            long sum = 0;
            int count = 0;
            for (int i = 0; i < recorder.Count; i++)
            {
                long val = recorder.GetSample(i).Value;
                if (val > 0)
                {
                    sum += val;
                    count++;
                }
            }
            if (count == 0) return 0f;
            double avgNs = (double)sum / count;
            return (float)(avgNs / 1000000.0);
        }

        private float GetRecorderAverageKb(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0f;
            long sum = 0;
            for (int i = 0; i < recorder.Count; i++)
            {
                sum += recorder.GetSample(i).Value;
            }
            double avgBytes = (double)sum / recorder.Count;
            return (float)(avgBytes / 1024.0);
        }
    }
}
