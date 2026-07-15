using UnityEngine;
using CityScape.Managers;
using CityScape.GridSystem.Placement;
using CityScape.GridSystem.Road;

namespace CityScape.ExploreMode
{
    /// <summary>
    /// Orchestrates the transition between Build and Explore modes.
    /// Disables building tools, hides UI, and enables the player character.
    /// </summary>
    public class ExploreModeManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector Variables
        // ─────────────────────────────────────────────
        [Header("Player Settings")]
        [Tooltip("The player character GameObject.")]
        [SerializeField] private GameObject playerObject;
        
        [Tooltip("Optional: A transform representing where the player should spawn or teleport to. " + 
                 "If empty, the player remains at their last known position.")]
        [SerializeField] private Transform playerSpawnPoint;

        [Header("Build UI")]
        [Tooltip("Drag the main Build UI GameObjects here (e.g., Toolbar, Building Selection panel) to hide them in Explore Mode.")]
        [SerializeField] private GameObject[] buildUIElements;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────
        private BuildingPlacer _buildingPlacer;
        private RoadPlacer _roadPlacer;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Start()
        {
            // Find tools in the scene
            _buildingPlacer = FindFirstObjectByType<BuildingPlacer>();
            _roadPlacer = FindFirstObjectByType<RoadPlacer>();

            // Subscribe to CameraManager
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.OnCameraModeChanged += HandleModeChanged;
                // Initialize based on current mode
                HandleModeChanged(CameraManager.Instance.CurrentMode);
            }
            else
            {
                Debug.LogWarning("[ExploreModeManager] CameraManager instance not found on Start. Mode switching will not work.");
            }
        }

        private void OnDestroy()
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.OnCameraModeChanged -= HandleModeChanged;
            }
        }

        // ─────────────────────────────────────────────
        //  Mode Switching
        // ─────────────────────────────────────────────
        private void HandleModeChanged(CameraMode mode)
        {
            bool isExplore = (mode == CameraMode.Explore);

            // 1. Toggle Player
            if (playerObject != null)
            {
                playerObject.SetActive(isExplore);

                // Teleport to spawn point if defined and we just entered explore mode
                if (isExplore && playerSpawnPoint != null)
                {
                    playerObject.transform.position = playerSpawnPoint.position;
                    playerObject.transform.rotation = playerSpawnPoint.rotation;
                }
            }

            // 2. Disable Build Tools
            if (_buildingPlacer != null)
            {
                _buildingPlacer.enabled = !isExplore;
            }

            if (_roadPlacer != null)
            {
                // If we are entering explore mode and road mode is active, exit it safely first
                if (isExplore && _roadPlacer.IsRoadModeActive)
                {
                    _roadPlacer.ExitRoadMode();
                }
                _roadPlacer.enabled = !isExplore;
            }

            // 3. Hide/Show Build UI
            foreach (var ui in buildUIElements)
            {
                if (ui != null)
                {
                    ui.SetActive(!isExplore);
                }
            }
            
            // Cursor lock state is automatically managed by CameraManager Update loop
        }
    }
}
