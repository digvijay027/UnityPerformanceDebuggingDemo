using UnityEngine;

namespace PerformanceDemo.Performance
{
    /// <summary>
    /// Central manager for game object pools in Phase 4.3.
    /// Provides global access to projectile and enemy pools and exposes
    /// diagnostics for profiling and HUD metrics.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Header("Pool References")]
        [SerializeField] private ObjectPool projectilePool;
        [SerializeField] private ObjectPool enemyPool;

        [Header("Default Capacities")]
        [SerializeField] private int defaultProjectileCapacity = 35;
        [SerializeField] private int defaultEnemyCapacity = 85;

        public ObjectPool ProjectilePool => projectilePool;
        public ObjectPool EnemyPool => enemyPool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsurePoolsInitialized();
        }

        public void EnsurePoolsInitialized()
        {
            if (projectilePool == null)
            {
                GameObject projPoolObj = new GameObject("[PoolManager]_ProjectilePool");
                projPoolObj.transform.SetParent(transform);
                projectilePool = projPoolObj.AddComponent<ObjectPool>();
            }

            if (enemyPool == null)
            {
                GameObject enemyPoolObj = new GameObject("[PoolManager]_EnemyPool");
                enemyPoolObj.transform.SetParent(transform);
                enemyPool = enemyPoolObj.AddComponent<ObjectPool>();
            }
        }

        public void InitializeProjectilePool(GameObject prefab, int capacity = 0)
        {
            EnsurePoolsInitialized();
            if (projectilePool.Prefab == null && prefab != null)
            {
                projectilePool.Initialize(prefab, capacity > 0 ? capacity : defaultProjectileCapacity, "[Container]_Projectiles");
            }
        }

        public void InitializeEnemyPool(GameObject prefab, int capacity = 0)
        {
            EnsurePoolsInitialized();
            if (enemyPool.Prefab == null && prefab != null)
            {
                enemyPool.Initialize(prefab, capacity > 0 ? capacity : defaultEnemyCapacity, "[Container]_Enemies");
            }
        }

        public GameObject GetProjectile(Vector3 position, Quaternion rotation)
        {
            return projectilePool != null ? projectilePool.Get(position, rotation) : null;
        }

        public void ReturnProjectile(GameObject projectile)
        {
            if (projectilePool != null)
            {
                projectilePool.Return(projectile);
            }
        }

        public GameObject GetEnemy(Vector3 position, Quaternion rotation)
        {
            return enemyPool != null ? enemyPool.Get(position, rotation) : null;
        }

        public void ReturnEnemy(GameObject enemy)
        {
            if (enemyPool != null)
            {
                enemyPool.Return(enemy);
            }
        }
    }
}
