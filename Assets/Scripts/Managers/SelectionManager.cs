using System;
using CityScape.GridSystem.Data;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>
    /// Tracks the currently selected BuildingData and bridges the UI selection
    /// to the BuildingPlacer. A single source of truth for "what is selected".
    ///
    /// UI panels fire SelectBuilding() / ClearSelection().
    /// BuildingPlacer reacts to the events automatically.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static SelectionManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>Currently selected building, or null if nothing is selected.</summary>
        public BuildingData SelectedBuilding { get; private set; }

        /// <summary>True when a building is selected and placement mode is active.</summary>
        public bool IsInPlacementMode => SelectedBuilding != null;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired when a building is selected. Null = deselected.</summary>
        public event Action<BuildingData> OnBuildingSelected;

        /// <summary>Fired specifically when the selection is cleared.</summary>
        public event Action OnSelectionCleared;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Selects the given building for placement.
        /// Validates funds before entering placement mode.
        /// </summary>
        public void SelectBuilding(BuildingData data)
        {
            if (data == null) { ClearSelection(); return; }

            if (!EconomyManager.Instance.CanAfford(data.placementCost))
            {
                NotificationManager.Instance?.ShowNotification(
                    $"Not Enough Money! Need ${data.placementCost}", NotificationType.Warning);
                return;
            }

            SelectedBuilding = data;
            OnBuildingSelected?.Invoke(data);

            // Forward to BuildingPlacer
            var placer = GridSystem.Placement.BuildingPlacer.Instance;
            if (placer != null) placer.SelectBuilding(data);

            Debug.Log($"[SelectionManager] Selected: {data.buildingName}");
        }

        /// <summary>Clears the current selection and exits placement mode.</summary>
        public void ClearSelection()
        {
            SelectedBuilding = null;
            OnBuildingSelected?.Invoke(null);
            OnSelectionCleared?.Invoke();

            var placer = GridSystem.Placement.BuildingPlacer.Instance;
            if (placer != null) placer.ClearSelection();
        }
    }
}
