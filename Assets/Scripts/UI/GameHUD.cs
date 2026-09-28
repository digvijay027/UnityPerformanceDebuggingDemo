using UnityEngine;
using PerformanceDemo.Player;
using PerformanceDemo.Combat;
using PerformanceDemo.Enemy;
using PerformanceDemo.Performance;

namespace PerformanceDemo.UI
{
    /// <summary>
    /// Lightweight diagnostic HUD for the Performance Debugging Demo.
    /// Exposes real-time framerate, population, object pool reuses, and lookup mode.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private PlayerHealth playerHealth;
        private float frameCount;
        private float dt;
        private float fps;
        private const float FpsUpdateRate = 4.0f; // 4 updates per second

        private void Start()
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
        }

        private void Update()
        {
            frameCount++;
            dt += Time.unscaledDeltaTime;
            if (dt > 1.0f / FpsUpdateRate)
            {
                fps = frameCount / dt;
                frameCount = 0;
                dt = 0;
            }
        }

        private void OnGUI()
        {
            var settings = PerformanceTestSettings.Instance;
            bool isBaseline = settings != null && settings.BaselineMode;

            // Panel 1: Performance Diagnostics
            GUI.Box(new Rect(10, 10, 330, 185), "Performance Diagnostics Overlay");

            string modeStatus = isBaseline 
                ? "<color=#FF5555>BASELINE (Unoptimized Churn & Tag Query)</color>" 
                : "<color=#55FF55>OPTIMIZED (Pooled + O(1) Registry)</color>";

            GUIStyle richStyle = new GUIStyle(GUI.skin.label) { richText = true };

            GUI.Label(new Rect(20, 30, 310, 20), $"<b>Framerate:</b> {fps:F1} FPS", richStyle);

            // In Baseline Mode: executes scene-wide tag lookup. In Optimized Mode: queries maintained registry.
            int enemyCount = isBaseline 
                ? GameObject.FindGameObjectsWithTag("Enemy").Length 
                : EnemyRegistry.ActiveEnemyCount;

            int projCount = FindObjectsOfType<Projectile>().Length;
            int targetEnemies = settings != null ? settings.TargetEnemyPopulation : 75;

            GUI.Label(new Rect(20, 50, 310, 20), $"<b>Active Enemies:</b> {enemyCount} / {targetEnemies}", richStyle);
            GUI.Label(new Rect(20, 70, 310, 20), $"<b>Active Projectiles:</b> {projCount}", richStyle);
            GUI.Label(new Rect(20, 90, 310, 20), $"<b>Performance Mode:</b> {modeStatus}", richStyle);

            // Registry and Pool metrics
            if (PoolManager.Instance != null && PoolManager.Instance.ProjectilePool != null)
            {
                var pPool = PoolManager.Instance.ProjectilePool;
                var ePool = PoolManager.Instance.EnemyPool;
                GUI.Label(new Rect(20, 110, 310, 20), $"<b>Enemy Registry:</b> {EnemyRegistry.ActiveEnemyCount} registered (O(1))", richStyle);
                if (ePool != null)
                {
                    GUI.Label(new Rect(20, 130, 310, 20), $"<b>Pool Status:</b> Proj ({pPool.ActiveCount}/{pPool.TotalCreatedCount}) | Enemy ({ePool.ActiveCount}/{ePool.TotalCreatedCount})", richStyle);
                }
            }

            GUI.Label(new Rect(20, 155, 310, 20), "<color=yellow>[B] Toggle Baseline / Optimized Mode</color>", richStyle);

            // Panel 2: Player Status & Controls
            GUI.Box(new Rect(10, 205, 330, 80), "Player Status & Controls");
            float hp = playerHealth != null ? playerHealth.CurrentHealth : 0f;
            float maxHp = playerHealth != null ? playerHealth.MaxHealth : 100f;
            GUI.Label(new Rect(20, 225, 310, 20), $"Health: {hp:F0} / {maxHp:F0}");
            GUI.Label(new Rect(20, 250, 310, 25), "WASD: Move | Left Click / Space: Rapid Fire");
        }
    }
}
