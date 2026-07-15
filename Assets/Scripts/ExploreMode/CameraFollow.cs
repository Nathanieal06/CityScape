using UnityEngine;
using UnityEngine.InputSystem;

namespace CityScape.ExploreMode
{
    /// <summary>
    /// Custom third-person orbit camera.
    /// Follows a target (player) and orbits based on Mouse Look input.
    /// Prevents clipping through terrain/buildings using a SphereCast.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector Variables
        // ─────────────────────────────────────────────
        [Header("Targeting")]
        [Tooltip("The player transform to follow.")]
        [SerializeField] private Transform target;
        [Tooltip("Offset from the target's origin (e.g., to look at the head/shoulders).")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        
        [Header("Orbit Settings")]
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float mouseSensitivity = 0.2f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 60f;
        
        [Header("Collision")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float cameraRadius = 0.3f;
        
        [Header("Input")]
        [SerializeField] private InputActionReference lookAction;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────
        private float _currentX;
        private float _currentY;
        private Vector2 _lookInput;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────
        private void OnEnable()
        {
            if (lookAction != null)
                lookAction.action.Enable();

            // When enabled, force camera to start directly behind the player
            if (target != null)
            {
                _currentX = target.eulerAngles.y;
                _currentY = 15f; // Slight downward angle
            }
        }

        private void OnDisable()
        {
            if (lookAction != null)
                lookAction.action.Disable();
        }

        private void Update()
        {
            if (lookAction != null)
                _lookInput = lookAction.action.ReadValue<Vector2>();

            // Orbit logic
            _currentX += _lookInput.x * mouseSensitivity;
            _currentY -= _lookInput.y * mouseSensitivity;
            
            // Clamp vertical angle
            _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 focusPoint = target.position + targetOffset;
            
            // Calculate desired rotation and position
            Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);
            Vector3 direction = rotation * new Vector3(0, 0, -cameraDistance);
            Vector3 desiredPosition = focusPoint + direction;

            // Collision Check
            float targetDistance = cameraDistance;
            
            // Raycast from the focus point towards the desired camera position
            if (Physics.SphereCast(focusPoint, cameraRadius, direction.normalized, out RaycastHit hit, cameraDistance, collisionLayers))
            {
                targetDistance = Mathf.Clamp(hit.distance, minDistance, cameraDistance);
            }
            
            // Apply final position (with collision adjustments)
            transform.position = focusPoint + direction.normalized * targetDistance;
            transform.rotation = rotation;
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _currentX = target.eulerAngles.y;
            }
        }
    }
}
