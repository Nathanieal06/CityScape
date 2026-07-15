using UnityEngine;
using UnityEngine.InputSystem;

namespace CityScape.ExploreMode
{
    /// <summary>
    /// Rigidbody-based Third-Person Player Controller.
    /// Handles movement relative to the camera, jumping, and sprinting.
    /// Assumes the player has a Rigidbody with Freeze Rotation X/Z enabled,
    /// Continuous Collision Detection, and Interpolation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector Variables
        // ─────────────────────────────────────────────
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private bool runByDefault = true;
        [SerializeField] private float rotationSmoothTime = 0.1f;

        [Header("Animation")]
        [Tooltip("The Animator component on your 3D character.")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator float parameter for overall movement speed magnitude.")]
        [SerializeField] private string speedParam = "Speed";
        [Tooltip("Animator float parameter for local X velocity (strafing).")]
        [SerializeField] private string velocityXParam = "VelocityX";
        [Tooltip("Animator float parameter for local Z velocity (forward/back).")]
        [SerializeField] private string velocityZParam = "VelocityZ";
        [Tooltip("Animator bool parameter for grounded state.")]
        [SerializeField] private string isGroundedParam = "IsGrounded";
        [Tooltip("Animator trigger parameter for jumping.")]
        [SerializeField] private string jumpParam = "Jump";
        
        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 5f;
        [Tooltip("Time in seconds before the player can jump again.")]
        [SerializeField] private float jumpCooldown = 0.25f;
        [SerializeField] private float gravityMultiplier = 2f;
        
        [Header("Ground Check")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private LayerMask groundLayer;

        [Header("References")]
        [Tooltip("The main camera transform to calculate movement relative to camera look direction.")]
        [SerializeField] private Transform mainCameraTransform;
        
        [Header("Input Actions")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────
        private Rigidbody _rb;
        private bool _isGrounded;
        private float _currentRotationVelocity;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isSprinting;
        private bool _jumpRequested;
        private float _animationBlend;
        private float _jumpTimer;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            
            // Try to find animator if not assigned
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            
            // Enforce recommended Rigidbody settings
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            _rb.isKinematic = false; // Ensures velocity can be set safely
        }

        private void OnEnable()
        {
            if (moveAction != null) moveAction.action.Enable();
            if (lookAction != null) lookAction.action.Enable();
            if (jumpAction != null)
            {
                jumpAction.action.Enable();
                jumpAction.action.performed += OnJumpPerformed;
            }
            if (sprintAction != null) sprintAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null) moveAction.action.Disable();
            if (lookAction != null) lookAction.action.Disable();
            if (jumpAction != null)
            {
                jumpAction.action.Disable();
                jumpAction.action.performed -= OnJumpPerformed;
            }
            if (sprintAction != null) sprintAction.action.Disable();
        }

        private void Start()
        {
            if (mainCameraTransform == null && Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
            
            if (groundCheckPoint == null)
            {
                // Fallback if not assigned: use the bottom of the collider
                GameObject defaultCheck = new GameObject("GroundCheck");
                defaultCheck.transform.SetParent(transform);
                defaultCheck.transform.localPosition = new Vector3(0, 0.1f, 0);
                groundCheckPoint = defaultCheck.transform;
            }
        }

        private void Update()
        {
            // Read continuous input
            if (moveAction != null)
                _moveInput = moveAction.action.ReadValue<Vector2>();
                
            if (lookAction != null)
                _lookInput = lookAction.action.ReadValue<Vector2>();
                
            if (sprintAction != null)
                _isSprinting = sprintAction.action.ReadValue<float>() > 0.5f;

            if (_jumpTimer > 0)
                _jumpTimer -= Time.deltaTime;

            CheckGrounded();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            HandleMovement();
            ApplyCustomGravity();
            HandleJump();
        }

        // ─────────────────────────────────────────────
        //  Input Callbacks
        // ─────────────────────────────────────────────
        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            // Only allow jump if the button is pressed (not released), grounded, and cooldown has passed
            if (context.ReadValueAsButton() && _isGrounded && _jumpTimer <= 0f)
            {
                _jumpRequested = true;
                _jumpTimer = jumpCooldown;
            }
        }

        // ─────────────────────────────────────────────
        //  Physics Logic
        // ─────────────────────────────────────────────
        private void HandleMovement()
        {
            if (mainCameraTransform == null) return;

            // Calculate movement direction relative to camera
            Vector3 camForward = mainCameraTransform.forward;
            Vector3 camRight = mainCameraTransform.right;
            
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * _moveInput.y + camRight * _moveInput.x;

            bool isRunning = runByDefault ? !_isSprinting : _isSprinting;
            float currentSpeed = isRunning ? sprintSpeed : walkSpeed;

            if (moveDir.magnitude >= 0.1f)
            {
                // Rotate character to face movement direction
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, rotationSmoothTime);
                _rb.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                
                // Calculate target velocity
                Vector3 targetVelocity = moveDir * currentSpeed;
                
                // Preserve Y velocity (falling/jumping)
                targetVelocity.y = _rb.linearVelocity.y;
                
                _rb.linearVelocity = targetVelocity;
            }
            else
            {
                // Decelerate smoothly or immediately stop X/Z movement
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }
        }

        private void HandleJump()
        {
            if (_jumpRequested)
            {
                if (animator != null) animator.SetTrigger(jumpParam);
                
                // Reset Y velocity before applying jump force to ensure consistent jump height
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                _jumpRequested = false;
            }
        }

        private void ApplyCustomGravity()
        {
            // Apply extra gravity only when falling to make the jump feel less floaty
            if (_rb.linearVelocity.y < 0)
            {
                _rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        private void CheckGrounded()
        {
            if (groundCheckPoint == null) return;
            
            _isGrounded = false;
            Collider[] colliders = Physics.OverlapSphere(groundCheckPoint.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
            foreach (var col in colliders)
            {
                // Ignore our own colliders
                if (col.transform.root != transform.root)
                {
                    _isGrounded = true;
                    break;
                }
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            // Calculate target blend value based on input (0 = idle, 0.5 = walk, 1 = sprint/run)
            float targetBlend = 0f;
            if (_moveInput.magnitude > 0.1f)
            {
                // If run by default is true, holding sprint makes us walk (0.5). Otherwise, sprint makes us run (1.0).
                bool isRunning = runByDefault ? !_isSprinting : _isSprinting;
                targetBlend = isRunning ? 1f : 0.5f;
            }

            // Smoothly lerp the blend value to prevent jerky animation transitions
            _animationBlend = Mathf.Lerp(_animationBlend, targetBlend, Time.deltaTime * 10f);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            animator.SetFloat(speedParam, _animationBlend);
            animator.SetBool(isGroundedParam, _isGrounded);
            
            // Note: VelocityX and VelocityZ are still available if you decide to use a 2D blend tree later
            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            animator.SetFloat(velocityXParam, localVelocity.x);
            animator.SetFloat(velocityZParam, localVelocity.z);
        }

        // ─────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}
