
namespace CityScape.GridSystem.Core
{
    /// <summary>
    /// Represents a single cell on the world grid.
    ///
    /// This is a plain C# class (not a MonoBehaviour) to keep memory overhead
    /// minimal. The grid can support tens of thousands of cells without GC
    /// pressure from per-cell components.
    ///
    /// Designed for future extensibility: additional utility flags (power,
    /// water, road) can be toggled independently, which the simulation layer
    /// can query without coupling to the placement system.
    /// </summary>
    public class GridCell
    {
        // ─────────────────────────────────────────────
        //  Grid Position (read-only)
        // ─────────────────────────────────────────────

        /// <summary>The cell's address in grid space. Assigned once at init.</summary>
        public GridCoordinates Coordinates { get; }

        // ─────────────────────────────────────────────
        //  Occupancy
        // ─────────────────────────────────────────────

        /// <summary>True when a building occupies this cell.</summary>
        public bool IsOccupied { get; private set; }

        /// <summary>
        /// Reference to the placed building that owns this cell.
        /// Null when IsOccupied is false.
        /// </summary>
        public PlacedBuilding OccupiedBy { get; private set; }

        // ─────────────────────────────────────────────
        //  Road & Infrastructure (future systems)
        // ─────────────────────────────────────────────

        /// <summary>Whether a road tile has been placed here. (Future: Road System)</summary>
        public bool HasRoad     { get; set; }

        /// <summary>The procedural tree/grass GameObject spawned on this cell.</summary>
        public UnityEngine.GameObject NatureObject { get; set; }

        /// <summary>True if this cell contains a spawned tree or grass.</summary>
        public bool HasNature => NatureObject != null;

        /// <summary>Whether this cell has a power connection. (Future: Utility System)</summary>
        public bool HasPower    { get; set; }

        /// <summary>Whether this cell has a water connection. (Future: Utility System)</summary>
        public bool HasWater    { get; set; }

        /// <summary>Terrain type tag, used by NPC pathfinding (Future: NavMesh bake).</summary>
        public TerrainType Terrain { get; set; } = TerrainType.Ground;

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        public GridCell(GridCoordinates coordinates)
        {
            Coordinates = coordinates;
        }

        // ─────────────────────────────────────────────
        //  Occupancy API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Marks this cell as occupied by a building.
        /// Called by GridManager — do not call directly from gameplay code.
        /// </summary>
        /// <param name="building">The building that now owns this cell.</param>
        public void Occupy(PlacedBuilding building)
        {
            IsOccupied  = true;
            OccupiedBy  = building;
        }

        /// <summary>
        /// Clears occupancy state so the cell can be built upon again.
        /// Called by GridManager — do not call directly from gameplay code.
        /// </summary>
        public void Free()
        {
            IsOccupied  = false;
            OccupiedBy  = null;
        }

        // ─────────────────────────────────────────────
        //  Utility Queries
        // ─────────────────────────────────────────────

        /// <summary>True if nothing can be placed on this cell.</summary>
        public bool IsBlocked => IsOccupied || HasRoad;

        /// <summary>
        /// Returns a snapshot of this cell's data — safe for serialisation
        /// without requiring the PlacedBuilding object reference.
        /// </summary>
        public GridCellSaveData ToSaveData()
        {
            return new GridCellSaveData
            {
                X          = Coordinates.X,
                Y          = Coordinates.Y,
                HasRoad    = HasRoad,
                HasPower   = HasPower,
                HasWater   = HasWater,
                Terrain    = Terrain,
                IsOccupied = IsOccupied
            };
        }

        public override string ToString()
            => $"Cell{Coordinates} [Occupied:{IsOccupied}|Road:{HasRoad}|Power:{HasPower}|Water:{HasWater}]";
    }

    // ─────────────────────────────────────────────
    //  Enumerations (co-located for cohesion)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Terrain classification per cell.
    /// Drives pathfinding costs and valid placement rules in future systems.
    /// </summary>
    public enum TerrainType
    {
        Ground,
        Water,
        Elevated,
        Slope
    }

    // ─────────────────────────────────────────────
    //  Serialisable Snapshot (Save/Load ready)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Plain data struct used by the save/load system.
    /// Contains no Unity object references so it can be serialised to JSON.
    /// </summary>
    [System.Serializable]
    public struct GridCellSaveData
    {
        public int         X;
        public int         Y;
        public bool        IsOccupied;
        public bool        HasRoad;
        public bool        HasPower;
        public bool        HasWater;
        public TerrainType Terrain;
    }
}
