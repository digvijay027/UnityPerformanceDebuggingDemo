using UnityEngine;
using PerformanceDemo.Core;

namespace PerformanceDemo.Enemy
{
    public class EnemyMovement : MonoBehaviour, IPoolable
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float stoppingDistance = 1.0f;

        private Transform target;
        private Rigidbody rb;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            FindTarget();
        }

        public void OnSpawnFromPool()
        {
            ResetMovement();
        }

        public void OnReturnToPool()
        {
            ResetMovement();
        }

        public void ResetMovement()
        {
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            FindTarget();
        }

        private void Update()
        {
            // Simple target finding fallback if not yet assigned
            if (target == null)
            {
                FindTarget();
                if (target == null) return;
            }

            MoveTowardsTarget();
        }

        private void FindTarget()
        {
            // Direct unoptimized GameObject find for baseline
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void MoveTowardsTarget()
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            float distance = direction.magnitude;
            if (distance > stoppingDistance)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized);
                transform.position += direction.normalized * (moveSpeed * Time.deltaTime);
            }
        }
    }
}
