using CityScape.GridSystem.Data;
using System.Collections.Generic;
using UnityEngine;

// NOTE: PlacedBuilding lives in the Core namespace (not Placement) to avoid
// a circular dependency: Core.GridCell and Core.GridManager both need to
// reference PlacedBuilding, so it must sit in or below Core.
namespace CityScape.GridSystem.Core
{
    /// <summary>
    /// Immutable record of a building that has been successfully placed on the grid.
    ///
    /// Holds a reference to the spawned GameObject, the source data, the grid
    /// origin, the rotation at placement time, and the exact list of cells it
    /// occupies so the deletion path can free them without recalculating.
    ///
    /// The class is intentionally read-only after construction (SRP / immutability).
    /// </summary>
    public class PlacedBuilding
    {
        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>The ScriptableObject definition that was used to place this building.</summary>
        public BuildingData Data { get; }

        /// <summary>
        /// The bottom-left grid cell of the building footprint at the time of placement.
        /// This is the "origin" from which all other occupied cells are derived.
        /// </summary>
        public GridCoordinates Origin { get; }

        /// <summary>
        /// Rotation step at placement: 0=0°, 1=90°, 2=180°, 3=270° (clockwise).
        /// </summary>
        public int RotationStep { get; }

        /// <summary>The instantiated GameObject currently in the scene.</summary>
        public GameObject GameObject { get; }

        /// <summary>
        /// All grid cells occupied by this building.
        /// Cached at placement time so deletion is O(footprint) not O(grid).
        /// </summary>
        public IReadOnlyList<GridCoordinates> OccupiedCells { get; }

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        /// <summary>
        /// Creates a new PlacedBuilding record. All parameters are required.
        /// </summary>
        public PlacedBuilding(
            BuildingData              data,
            GridCoordinates           origin,
            int                       rotationStep,
            GameObject                gameObject,
            List<GridCoordinates>     occupiedCells)
        {
            Data          = data;
            Origin        = origin;
            RotationStep  = rotationStep;
            GameObject    = gameObject;
            OccupiedCells = occupiedCells.AsReadOnly();
        }

        // ─────────────────────────────────────────────
        //  Serialisation Snapshot
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns a serialisable snapshot for save/load.
        /// The BuildingData.name (asset name) is used to re-look-up the
        /// ScriptableObject via a BuildingRegistry at load time.
        /// </summary>
        public PlacedBuildingSaveData ToSaveData()
        {
            return new PlacedBuildingSaveData
            {
                buildingDataName = Data.name,
                originX          = Origin.X,
                originY          = Origin.Y,
                rotationStep     = RotationStep
            };
        }

        public override string ToString()
            => $"[{Data.buildingName}] at {Origin}, rot={RotationStep * 90}°";
    }

    // ─────────────────────────────────────────────
    //  Serialisable Snapshot (Save/Load ready)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Pure data struct — no Unity references — safe for JSON serialisation.
    /// The save system stores this and re-creates PlacedBuilding on load.
    /// </summary>
    [System.Serializable]
    public struct PlacedBuildingSaveData
    {
        /// <summary>The ScriptableObject asset name used as lookup key in a BuildingRegistry.</summary>
        public string buildingDataName;
        public int    originX;
        public int    originY;
        public int    rotationStep;
    }
}
