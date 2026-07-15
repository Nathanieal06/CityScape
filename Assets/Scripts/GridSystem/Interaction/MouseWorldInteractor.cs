using CityScape.GridSystem.Core;
using UnityEngine;
using UnityEngine.InputSystem;  // New Input System

namespace CityScape.GridSystem.Interaction
{
    /// <summary>
    /// Casts a ray from the camera through the mouse position and returns
    /// either the world-space hit point or the equivalent grid coordinates.
    ///
    /// Designed to be used by BuildingPlacer which calls TryGetWorldPosition
    /// once per Update when in placement mode — no constant polling overhead.
    ///
    /// Configure the 'Terrain Layer' in the Inspector to match whatever layer
    /// your ground plane / terrain is on.
    /// </summary>
    public class MouseWorldInteractor : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Camera used for raycasting. Defaults to Camera.main if left empty.")]
        [SerializeField] private Camera raycastCamera;

        [Header("Raycast")]
        [Tooltip("Layer(s) the raycast should hit. Set this to your Terrain/Ground layer.")]
        [SerializeField] private LayerMask terrainLayerMask = ~0; // Default: Everything

        [Tooltip("Maximum raycast distance (metres).")]
        [SerializeField, Min(10f)] private float maxRaycastDistance = 500f;

        // ─────────────────────────────────────────────
        //  Cached State
        // ─────────────────────────────────────────────

        /// <summary>The last successfully computed world-space hit position.</summary>
        public Vector3 LastWorldPosition { get; private set; }

        /// <summary>True if the last Update's raycast successfully hit terrain.</summary>
        public bool HasValidHit { get; private set; }

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;

            if (raycastCamera == null)
                Debug.LogError("[MouseWorldInteractor] No camera found. " +
                               "Assign one in the Inspector or tag it as MainCamera.", this);
        }

        private void Update()
        {
            // Cache the result every frame so callers don't re-raycast.
            HasValidHit = PerformRaycast(out Vector3 hit);
            if (HasValidHit)
                LastWorldPosition = hit;
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns true and sets <paramref name="worldPosition"/> if the mouse
        /// ray hit a terrain surface this frame.
        /// </summary>
        public bool TryGetWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = LastWorldPosition;
            return HasValidHit;
        }

        /// <summary>
        /// Returns true and sets <paramref name="coords"/> if the mouse ray
        /// hit terrain this frame and GridManager.Instance is available.
        /// </summary>
        public bool TryGetGridCoordinates(out GridCoordinates coords)
        {
            coords = GridCoordinates.Zero;

            if (!HasValidHit || GridManager.Instance == null)
                return false;

            coords = GridManager.Instance.WorldToGrid(LastWorldPosition);
            return true;
        }

        // ─────────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────────

        private bool _loggedMissingCamera = false;

        private bool PerformRaycast(out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;

            Camera activeCam = raycastCamera;
            if (activeCam == null || !activeCam.gameObject.activeInHierarchy)
            {
                activeCam = Camera.main;
            }

            if (activeCam == null)
            {
                if (!_loggedMissingCamera)
                {
                    Debug.LogError("[MouseWorldInteractor] Cannot interact with the grid! There is no active Camera tagged as 'MainCamera' in the scene.");
                    _loggedMissingCamera = true;
                }
                return false;
            }
            _loggedMissingCamera = false;

            // New Input System: read mouse screen position from Mouse.current
            if (Mouse.current == null) return false;

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Ray ray = activeCam.ScreenPointToRay(mouseScreenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, terrainLayerMask))
            {
                hitPoint = hit.point;
                return true;
            }
            
            // Fallback: If the user clicked on a building (which might not be on the terrain layer),
            // the raycast above will miss it and hit the ground behind it, causing deletion to fail.
            // This fallback catches hits against buildings/anything else.
            if (Physics.Raycast(ray, out RaycastHit fallbackHit, maxRaycastDistance))
            {
                hitPoint = fallbackHit.point;
                return true;
            }

            return false;
        }
    }
}
