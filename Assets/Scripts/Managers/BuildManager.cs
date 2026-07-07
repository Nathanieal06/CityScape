using System;
using CityScape.GridSystem.Data;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>
    /// Controls the active building category shown in the toolbar and
    /// the building selection panel. Also manages bulldozer mode.
    ///
    /// Separate from SelectionManager: BuildManager = "what category/mode",
    /// SelectionManager = "what specific building is selected".
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static BuildManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>Currently active building category for the building panel.</summary>
        public BuildingCategory ActiveCategory { get; private set; } = BuildingCategory.Residential;

        /// <summary>True when bulldozer/delete mode is active.</summary>
        public bool IsBulldozerActive { get; private set; }

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired when the user switches to a different category tab.</summary>
        public event Action<BuildingCategory> OnCategoryChanged;

        /// <summary>Fired when bulldozer mode is toggled.</summary>
        public event Action<bool> OnBulldozerToggled;

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

        /// <summary>Switches the active category and refreshes the building panel.</summary>
        public void SetCategory(BuildingCategory category)
        {
            IsBulldozerActive = false;
            OnBulldozerToggled?.Invoke(false);

            ActiveCategory = category;
            SelectionManager.Instance?.ClearSelection();
            OnCategoryChanged?.Invoke(category);
        }

        /// <summary>Activates or deactivates bulldozer (delete) mode.</summary>
        public void SetBulldozerMode(bool active)
        {
            IsBulldozerActive = active;
            if (active) SelectionManager.Instance?.ClearSelection();
            OnBulldozerToggled?.Invoke(active);
        }

        /// <summary>Toggles bulldozer mode.</summary>
        public void ToggleBulldozer() => SetBulldozerMode(!IsBulldozerActive);
    }
}
