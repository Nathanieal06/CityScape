using CityScape.GridSystem.Core;
using CityScape.GridSystem.Data;
using CityScape.GridSystem.Utility;
using System.Collections.Generic;

namespace CityScape.GridSystem.Placement
{
    /// <summary>
    /// Validates whether a building can be placed at a given grid origin.
    ///
    /// This class is deliberately NOT a MonoBehaviour — it is a pure logic
    /// class that is injected with a GridManager reference (Dependency Injection).
    /// This makes it independently unit-testable and keeps it decoupled from
    /// the GameObject lifecycle.
    ///
    /// SOLID: Single Responsibility — knows only about validity rules.
    ///         Open/Closed — add new rule types (e.g. zone restrictions) by
    ///         extending this class, not by modifying BuildingPlacer.
    /// </summary>
    public class PlacementValidator
    {
        // ─────────────────────────────────────────────
        //  Dependencies
        // ─────────────────────────────────────────────

        private readonly GridManager _gridManager;

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        /// <summary>
        /// Creates a PlacementValidator bound to the given GridManager.
        /// </summary>
        public PlacementValidator(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns true if the building described by <paramref name="data"/>
        /// can legally be placed with its bottom-left cell at
        /// <paramref name="origin"/> with the given <paramref name="rotationStep"/>.
        ///
        /// Checks:
        ///   1. All required cells are inside the grid bounds.
        ///   2. No required cell is already occupied.
        /// </summary>
        public bool CanPlace(
            GridCoordinates origin,
            BuildingData    data,
            int             rotationStep)
        {
            var cells = GetOccupiedCells(origin, data, rotationStep);

            foreach (var cell in cells)
            {
                // Bounds check
                if (!_gridManager.IsInsideGrid(cell))
                    return false;

                // Occupancy check
                var gridCell = _gridManager.GetCell(cell);
                if (gridCell == null || gridCell.IsBlocked)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the list of all grid cells that the building would occupy
        /// when placed at <paramref name="origin"/> with the given rotation.
        ///
        /// The list is freshly allocated per call. BuildingPlacer caches it
        /// at the moment of confirmed placement to avoid repeated allocations.
        /// </summary>
        public List<GridCoordinates> GetOccupiedCells(
            GridCoordinates origin,
            BuildingData    data,
            int             rotationStep)
        {
            var (w, h) = GridUtility.RotateFootprint(
                data.footprintWidth,
                data.footprintHeight,
                rotationStep);

            return GridUtility.GetCellsForFootprint(origin, w, h);
        }

        // ─────────────────────────────────────────────
        //  Extended Rule Hooks (future)
        // ─────────────────────────────────────────────
        // To add zone restrictions, economy gating, or terrain checks,
        // add private helper methods here and call them from CanPlace().
        // Example:
        //   private bool IsZoneCompatible(GridCoordinates cell, BuildingCategory category) { ... }
    }
}
