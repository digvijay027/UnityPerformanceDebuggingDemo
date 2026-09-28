using UnityEngine;
using PerformanceDemo.Combat;
using PerformanceDemo.Performance;
using Unity.Profiling;

namespace PerformanceDemo.Player
{
    /// <summary>
    /// Player shooting controller with dual-mode projectile generation:
    /// - Baseline Mode ON (Legacy): Direct unpooled Instantiate() calls producing continuous object churn.
    /// - Baseline Mode OFF: Reusable Projectile ObjectPool returning zero-allocation instances.
    /// </summary>
    public class PlayerShooting : MonoBehaviour
    {
        [Header("Shooting Settings")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 0.2f;

        private float nextFireTime;

        private static readonly ProfilerMarker s_ShootMarker = new ProfilerMarker("PlayerShooting.Shoot");
        private static readonly ProfilerMarker s_InstantiateMarker = new ProfilerMarker("PlayerShooting.Instantiate");

        public GameObject ProjectilePrefab
        {
            get => projectilePrefab;
            set => projectilePrefab = value;
        }

        public Transform FirePoint
        {
            get => firePoint;
            set => firePoint = value;
        }

        private void Start()
        {
            // Initialize pool early if available
            if (projectilePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.InitializeProjectilePool(projectilePrefab);
            }
        }

        private void Update()
        {
            float currentRate = fireRate;
            bool allowAutoFire = true;

            var settings = PerformanceTestSettings.Instance;
            if (settings != null)
            {
                currentRate = settings.ActiveFireRate;
                allowAutoFire = settings.AutoFire;
            }

            bool fireInput = allowAutoFire || Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space);

            if (fireInput && Time.time >= nextFireTime)
            {
                Shoot(currentRate);
            }
        }

        public GameObject Shoot()
        {
            float currentRate = fireRate;
            var settings = PerformanceTestSettings.Instance;
            if (settings != null)
            {
                currentRate = settings.ActiveFireRate;
            }
            return Shoot(currentRate);
        }

        public GameObject Shoot(float effectiveFireRate)
        {
            using (s_ShootMarker.Auto())
            {
                nextFireTime = Time.time + effectiveFireRate;

                Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward;
                Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

                if (projectilePrefab == null) return null;

                var settings = PerformanceTestSettings.Instance;
                bool isLegacyLifecycle = settings != null && settings.BaselineLifecycleMode;

                if (isLegacyLifecycle)
                {
                    // --- LEGACY BASELINE LIFECYCLE: INTENTIONAL UNPOOLED INSTANTIATE CHURN ---
                    using (s_InstantiateMarker.Auto())
                    {
                        GameObject spawned = Instantiate(projectilePrefab, spawnPos, spawnRot);
                        Projectile proj = spawned.GetComponent<Projectile>();
                        if (proj != null)
                        {
                            proj.SetPooled(false);
                        }
                        return spawned;
                    }
                }
                else
                {
                    // --- PHASE 4.3 REUSABLE OBJECT POOLING ACTIVE ---
                    if (PoolManager.Instance != null)
                    {
                        PoolManager.Instance.InitializeProjectilePool(projectilePrefab);
                        return PoolManager.Instance.GetProjectile(spawnPos, spawnRot);
                    }
                    else
                    {
                        return Instantiate(projectilePrefab, spawnPos, spawnRot);
                    }
                }
            }
        }
    }
}
