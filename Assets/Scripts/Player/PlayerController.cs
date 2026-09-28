using UnityEngine;

namespace PerformanceDemo.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float gravity = 20f;

        private CharacterController characterController;
        private Camera mainCamera;
        private Vector3 verticalVelocity;

        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleRotation();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

            Vector3 motion = moveDirection * (moveSpeed * Time.deltaTime);

            if (characterController.isGrounded)
            {
                verticalVelocity = Vector3.down * 2f;
            }
            else
            {
                verticalVelocity.y -= gravity * Time.deltaTime;
            }

            motion += verticalVelocity * Time.deltaTime;
            characterController.Move(motion);
        }

        private void HandleRotation()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 lookDirection = hitPoint - transform.position;
                lookDirection.y = 0f;

                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
        }

        public void MoveDirect(Vector3 direction)
        {
            EnsureInitialized();
            characterController.Move(direction * (moveSpeed * Time.deltaTime));
        }
    }
}
