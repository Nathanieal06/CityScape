using CityScape.GridSystem.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CityScape.GridSystem.Road
{
    /// <summary>
    /// Pure-static helper that analyses the four cardinal neighbours of a road block
    /// and decides which road tile shape and Y-rotation to use.
    ///
    /// For NxN footprint roads, neighbours are checked at distance N (block-space),
    /// not at distance 1 (cell-space). This ensures T-junctions and corners resolve
    /// correctly when each road tile occupies multiple cells.
    ///
    /// Directions use grid-space conventions:
    ///   North = +Y (world +Z)
    ///   South = -Y (world -Z)
    ///   East  = +X (world +X)
    ///   West  = -X (world -X)
    ///
    /// Rotation angles are clockwise Y-axis Euler angles (Unity convention).
    /// </summary>
    public static class RoadTileSelector
    {
        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Analyses the neighbours of a road block whose bottom-left cell is
        /// <paramref name="blockOrigin"/> and returns the tile shape + Y-rotation.
        /// </summary>
        /// <param name="blockOrigin">Bottom-left cell of the NxN block.</param>
        /// <param name="blockSize">Cells per side of the road footprint (e.g. 2 for 2×2).</param>
        /// <param name="gridManager">GridManager for neighbour lookups.</param>
        /// <param name="rotationY">Output: Euler Y rotation in degrees.</param>
        public static RoadTileType Evaluate(
            GridCoordinates blockOrigin,
            int             blockSize,
            GridManager     gridManager,
            out float       rotationY)
        {
            int s = blockSize;

            // Check whether the four adjacent blocks each contain a road.
            // We look at the first cell of each neighbour block (its bottom-left corner).
            bool n = IsRoad(gridManager, blockOrigin.X,     blockOrigin.Y + s);  // North block
            bool s_ = IsRoad(gridManager, blockOrigin.X,    blockOrigin.Y - s);  // South block
            bool e = IsRoad(gridManager, blockOrigin.X + s, blockOrigin.Y    );  // East block
            bool w = IsRoad(gridManager, blockOrigin.X - s, blockOrigin.Y    );  // West block

            int count = (n ? 1 : 0) + (s_ ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

            // ── 4 neighbours ─────────────────────────────────────
            if (count == 4)
            {
                rotationY = 0f;
                return RoadTileType.Cross;
            }

            // ── 3 neighbours — T-junction ─────────────────────────
            if (count == 3)
            {
                if (!n)  rotationY = 180f;
                else if (!s_) rotationY = 0f;
                else if (!e)  rotationY = 270f;
                else          rotationY = 90f;
                return RoadTileType.TJunction;
            }

            // ── 2 neighbours ──────────────────────────────────────
            if (count == 2)
            {
                // Straight — two opposite directions
                if (n && s_) { rotationY = 0f;   return RoadTileType.Straight; }
                if (e && w)  { rotationY = 90f;  return RoadTileType.Straight; }

                // Corner — two adjacent directions
                if (n && e)  { rotationY = 0f;   return RoadTileType.Corner; }
                if (e && s_) { rotationY = 90f;  return RoadTileType.Corner; }
                if (s_ && w) { rotationY = 180f; return RoadTileType.Corner; }
                if (w && n)  { rotationY = 270f; return RoadTileType.Corner; }
            }

            // ── 1 neighbour — dead-end / cap ─────────────────────
            if (count == 1)
            {
                if (n)       rotationY = 180f;
                else if (s_) rotationY = 0f;
                else if (e)  rotationY = 270f;
                else         rotationY = 90f;
                return RoadTileType.DeadEnd;
            }

            // ── 0 neighbours — isolated ───────────────────────────
            rotationY = 0f;
            return RoadTileType.Isolated;
        }

        /// <summary>
        /// Returns the block-origin coordinates of the four cardinal neighbouring
        /// road blocks. Each neighbour block is at distance <paramref name="blockSize"/>
        /// from <paramref name="blockOrigin"/> in one cardinal direction.
        /// </summary>
        public static IEnumerable<GridCoordinates> GetBlockNeighbours(
            GridCoordinates blockOrigin, int blockSize)
        {
            int s = blockSize;
            yield return new GridCoordinates(blockOrigin.X,     blockOrigin.Y + s); // N
            yield return new GridCoordinates(blockOrigin.X,     blockOrigin.Y - s); // S
            yield return new GridCoordinates(blockOrigin.X + s, blockOrigin.Y    ); // E
            yield return new GridCoordinates(blockOrigin.X - s, blockOrigin.Y    ); // W
        }

        /// <summary>
        /// Legacy overload: checks single-cell neighbours (blockSize = 1).
        /// Kept for backwards compatibility.
        /// </summary>
        public static RoadTileType Evaluate(
            GridCoordinates coords,
            GridManager     gridManager,
            out float       rotationY)
            => Evaluate(coords, 1, gridManager, out rotationY);

        /// <summary>
        /// Legacy: returns single-cell cardinal neighbours (blockSize = 1).
        /// </summary>
        public static IEnumerable<GridCoordinates> GetCardinalNeighbours(GridCoordinates coords)
            => GetBlockNeighbours(coords, 1);

        // ─────────────────────────────────────────────
        //  Internal Helpers
        // ─────────────────────────────────────────────

        private static bool IsRoad(GridManager gm, int x, int y)
        {
            var cell = gm.GetCell(x, y);
            return cell != null && cell.HasRoad;
        }
    }
}
