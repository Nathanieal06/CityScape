using CityScape.GridSystem.Data;
using CityScape.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Manages all UI panel visibility and inter-panel coordination.
    /// Acts as the single orchestrator — no panel opens or closes itself.
    ///
    /// Attach to a persistent UI Root GameObject.
    /// Wire all panels in the Inspector.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static UIManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector — Panel References
        // ─────────────────────────────────────────────

        [Header("Game UI Panels")]
        [SerializeField] private HUDController      hudController;
        [SerializeField] private BuildingInfoPanel  buildingInfoPanel;
        [SerializeField] private BuildingSelectionPanel buildingSelectionPanel;
        [SerializeField] private CategoryToolbar    categoryToolbar;
        [SerializeField] private PlacementOverlay   placementOverlay;
        [SerializeField] private MinimapPanel       minimapPanel;
        [SerializeField] private BuildExploreToggle buildExploreToggle;

        [Header("Menu Panels")]
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private SettingsPanel       settingsPanel;

        [Header("Overlay Panels")]
        [SerializeField] private GameObject gameUIRoot;   // all game panels together
        [SerializeField] private GameObject pauseRoot;    // pause screen

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Subscribe to manager events
            SelectionManager.Instance.OnBuildingSelected += OnBuildingSelected;

            if (GameManager.Instance != null)
                GameManager.Instance.OnPauseToggled += OnPauseToggled;

            // Initial state
            ShowGameUI();
            HidePauseMenu();
        }

        // ─────────────────────────────────────────────
        //  Public API — Panel Control
        // ─────────────────────────────────────────────

        public void ShowGameUI()
        {
            if (gameUIRoot != null) gameUIRoot.SetActive(true);
        }

        public void HideGameUI()
        {
            if (gameUIRoot != null) gameUIRoot.SetActive(false);
        }

        public void ShowPauseMenu()
        {
            if (pauseRoot != null) pauseRoot.SetActive(true);
        }

        public void HidePauseMenu()
        {
            if (pauseRoot != null) pauseRoot.SetActive(false);
        }

        public void ShowSettings()
        {
            settingsPanel?.Show();
        }

        public void HideSettings()
        {
            settingsPanel?.Hide();
        }

        // ─────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────

        private void OnBuildingSelected(BuildingData data)
        {
            if (data != null)
            {
                buildingInfoPanel?.ShowBuilding(data);
                placementOverlay?.Show();
            }
            else
            {
                buildingInfoPanel?.Hide();
                placementOverlay?.Hide();
            }
        }

        private void OnPauseToggled(bool paused)
        {
            if (paused) ShowPauseMenu();
            else        HidePauseMenu();
        }
    }
}
