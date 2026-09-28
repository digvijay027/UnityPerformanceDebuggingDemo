using UnityEngine;

namespace PerformanceDemo.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        public void TakeDamage(float amount)
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }

            if (IsDead) return;

            currentHealth -= amount;
            Debug.Log($"[PlayerHealth] Player took {amount} damage. Remaining HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        private void Die()
        {
            Debug.Log("[PlayerHealth] Player has died!");
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
        }
    }
}
