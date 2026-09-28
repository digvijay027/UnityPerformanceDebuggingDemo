namespace PerformanceDemo.Core
{
    /// <summary>
    /// Lifecycle interface for pooled GameObjects.
    /// Provides explicit hooks for resetting state when an object is taken from
    /// or returned to an ObjectPool, avoiding stale state retention.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Invoked immediately after the object is retrieved from the pool
        /// and placed at its spawn transform, before it begins active gameplay.
        /// </summary>
        void OnSpawnFromPool();

        /// <summary>
        /// Invoked immediately before the object is deactivated and returned
        /// to the pool queue.
        /// </summary>
        void OnReturnToPool();
    }
}
