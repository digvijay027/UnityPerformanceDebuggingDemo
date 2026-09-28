using UnityEngine;
using PerformanceDemo.Player;
using PerformanceDemo.Core;

namespace PerformanceDemo.Enemy
{
    public class EnemyAttack : MonoBehaviour, IPoolable
    {
        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackCooldown = 1f;

        private float nextAttackTime;

        public void OnSpawnFromPool()
        {
            nextAttackTime = Time.time + 0.5f;
        }

        public void OnReturnToPool()
        {
            nextAttackTime = 0f;
        }

        private void OnTriggerStay(Collider other)
        {
            TryAttack(other.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryAttack(collision.gameObject);
        }

        private void TryAttack(GameObject targetObject)
        {
            if (Time.time < nextAttackTime) return;

            if (targetObject.CompareTag("Player"))
            {
                PlayerHealth playerHealth = targetObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                    nextAttackTime = Time.time + attackCooldown;
                }
            }
        }
    }
}
