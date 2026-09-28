using UnityEngine;
using PerformanceDemo.Enemy;
using PerformanceDemo.Core;
using PerformanceDemo.Performance;
using Unity.Profiling;

namespace PerformanceDemo.Combat
{
    /// <summary>
    /// Projectile component with dual-mode lifecycle support:
    /// - Baseline Mode: Direct unpooled Instantiate() and Destroy() lifecycle churn.
    /// - Optimized Mode: Reusable ObjectPool integration implementing IPoolable.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [Header("Projectile Settings")]
        [SerializeField] private float speed = 25f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float lifetime = 3f;

        private bool isPooled;
        private float currentLifetime;

        private static readonly ProfilerMarker s_ProjDestroyMarker = new ProfilerMarker("Projectile.Destroy");

        public float Damage => damage;
        public float Speed => speed;
        public bool IsPooled => isPooled;

        public void SetPooled(bool pooled)
        {
            isPooled = pooled;
        }

        public void OnSpawnFromPool()
        {
            isPooled = true;
            currentLifetime = 0f;
        }

        public void OnReturnToPool()
        {
            currentLifetime = 0f;
        }

        private void Start()
        {
            // In unoptimized Baseline Mode (direct Instantiate), schedule engine Destroy
            if (!isPooled)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void Update()
        {
            transform.Translate(Vector3.forward * (speed * Time.deltaTime));

            // In pooled mode, monitor lifetime manually to return to pool without Destroy()
            if (isPooled)
            {
                currentLifetime += Time.deltaTime;
                if (currentLifetime >= lifetime)
                {
                    Terminate();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore player or other projectiles
            if (other.CompareTag("Player") || other.GetComponent<Projectile>() != null)
            {
                return;
            }

            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                enemyHealth = other.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                Terminate();
                return;
            }

            // If hit arena walls or obstacles
            if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
            {
                Terminate();
            }
        }

        /// <summary>
        /// Handles projectile termination:
        /// Reusable pooling return in optimized mode; engine Destroy() in baseline mode.
        /// </summary>
        private void Terminate()
        {
            if (isPooled && PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnProjectile(gameObject);
            }
            else
            {
                using (s_ProjDestroyMarker.Auto())
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
