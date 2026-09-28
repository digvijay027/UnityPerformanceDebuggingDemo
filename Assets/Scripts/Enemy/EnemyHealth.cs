using System;
using UnityEngine;
using PerformanceDemo.Core;
using PerformanceDemo.Performance;
using Unity.Profiling;

namespace PerformanceDemo.Enemy
{
    /// <summary>
    /// Enemy health component supporting dual lifecycle:
    /// - Baseline Mode: Destroys enemy GameObject via Destroy(gameObject).
    /// - Optimized Mode: Implements IPoolable to return enemy to EnemyPool without destruction.
    /// - Phase 4.4: Integrates with EnemyRegistry for zero-allocation active enemy tracking.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IPoolable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private float currentHealth;

        private bool isPooled;

        public static event Action<EnemyHealth> OnEnemyDied;

        private static readonly ProfilerMarker s_EnemyDestroyMarker = new ProfilerMarker("Enemy.Destroy");

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public bool IsPooled => isPooled;

        public void SetPooled(bool pooled)
        {
            isPooled = pooled;
        }

        private void Awake()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        private void OnEnable()
        {
            // Register with EnemyRegistry when active in the scene
            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            // Unregister from EnemyRegistry when deactivated or returned to pool
            EnemyRegistry.Unregister(this);
        }

        public void OnSpawnFromPool()
        {
            isPooled = true;
            currentHealth = maxHealth;
            EnemyRegistry.Register(this);
        }

        public void OnReturnToPool()
        {
            currentHealth = maxHealth;
            EnemyRegistry.Unregister(this);
        }

        public void TakeDamage(float amount)
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }

            if (IsDead) return;

            currentHealth -= amount;

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        private void Die()
        {
            OnEnemyDied?.Invoke(this);
            EnemyRegistry.Unregister(this);

            if (isPooled && PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnEnemy(gameObject);
            }
            else
            {
                using (s_EnemyDestroyMarker.Auto())
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
