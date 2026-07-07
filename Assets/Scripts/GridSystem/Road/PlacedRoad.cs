using CityScape.GridSystem.Core;
using UnityEngine;

namespace CityScape.GridSystem.Road
{
    /// <summary>
    /// Plain C# record for a single road tile that has been placed on the grid.
    ///
    /// Mirrors the purpose of PlacedBuilding but is intentionally kept lightweight:
    /// a road always occupies exactly ONE cell and has no economy data.
    ///
    /// Stored in RoadPlacer._placedRoads keyed by GridCoordinates.
    /// </summary>
    public class PlacedRoad
    {
        // ─────────────────────────────────────────────
        //  Data
        // ─────────────────────────────────────────────

        /// <summary>Grid cell this road tile occupies.</summary>
        public GridCoordinates Coordinates { get; }

        /// <summary>The instantiated road GameObject in the scene.</summary>
        public GameObject GameObject { get; set; }

        /// <summary>The RoadTileType assigned at placement (for save/load).</summary>
        public RoadTileType TileType { get; set; }

        // ─────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────

        public PlacedRoad(GridCoordinates coordinates, GameObject gameObject, RoadTileType tileType)
        {
            Coordinates = coordinates;
            GameObject  = gameObject;
            TileType    = tileType;
        }

        public override string ToString()
            => $"Road[{Coordinates}|{TileType}]";
    }

    // ─────────────────────────────────────────────
    //  Enum — Road Tile Shape
    // ─────────────────────────────────────────────

    /// <summary>
    /// Describes the connectivity shape of a road tile.
    /// Used by RoadTileSelector to choose the correct prefab and rotation.
    /// </summary>
    public enum RoadTileType
    {
        /// <summary>No neighbours — defaults to straight visual.</summary>
        Isolated,

        /// <summary>One neighbour — dead-end cap (uses straight, rotated toward neighbour).</summary>
        DeadEnd,

        /// <summary>Two opposite neighbours — straight through tile.</summary>
        Straight,

        /// <summary>Two adjacent (90°) neighbours — corner / turn.</summary>
        Corner,

        /// <summary>Three neighbours — T-junction.</summary>
        TJunction,

        /// <summary>All four neighbours — crossroads.</summary>
        Cross
    }
}
