using CityScape.GridSystem.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CityScape.GridSystem.Utility
{
    /// <summary>
    /// Static, stateless helpers for grid math.
    ///
    /// All methods are pure functions — they take inputs and return outputs
    /// without touching any global state, making them trivially testable.
    ///
    /// Add new helpers here when other systems need grid-related calculations.
    /// </summary>
    public static class GridUtility
    {
        // ─────────────────────────────────────────────
        //  Rotation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the effective footprint dimensions after applying the given
        /// rotation step. At 90° and 270° the width and height are swapped.
        /// </summary>
        /// <param name="width">Original footprint width (along world X).</param>
        /// <param name="height">Original footprint height (along world Z).</param>
        /// <param name="rotationStep">0=0°, 1=90°, 2=180°, 3=270° (clockwise).</param>
        /// <returns>Effective (width, height) after rotation.</returns>
        public static (int width, int height) RotateFootprint(int width, int height, int rotationStep)
        {
            // 90° and 270° swap the axes; 0° and 180° keep them the same.
            bool swapped = (rotationStep % 2) != 0;
            return swapped ? (height, width) : (width, height);
        }

        // ─────────────────────────────────────────────
        //  Cell Enumeration
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns all grid cell coordinates covered by a building footprint,
        /// given the bottom-left origin, effective width and height.
        ///
        /// The returned list is newly allocated each call — cache it if called
        /// every frame. BuildingPlacer caches it in the placement path.
        /// </summary>
        /// <param name="origin">Bottom-left grid cell of the footprint.</param>
        /// <param name="width">Effective footprint width (already rotation-adjusted).</param>
        /// <param name="height">Effective footprint height (already rotation-adjusted).</param>
        public static List<GridCoordinates> GetCellsForFootprint(
            GridCoordinates origin, int width, int height)
        {
            var cells = new List<GridCoordinates>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells.Add(new GridCoordinates(origin.X + x, origin.Y + y));
                }
            }

            return cells;
        }

        // ─────────────────────────────────────────────
        //  Grid Clamping
        // ─────────────────────────────────────────────

        /// <summary>
        /// Clamps the given coordinates so the full footprint fits inside
        /// the grid bounds.  Used by BuildingPlacer to keep the preview from
        /// going out of bounds when near an edge.
        /// </summary>
        /// <param name="origin">Desired bottom-left cell.</param>
        /// <param name="footprintWidth">Width of the building footprint.</param>
        /// <param name="footprintHeight">Height of the building footprint.</param>
        /// <param name="gridWidth">Total grid columns.</param>
        /// <param name="gridHeight">Total grid rows.</param>
        public static GridCoordinates ClampToGrid(
            GridCoordinates origin,
            int footprintWidth,
            int footprintHeight,
            int gridWidth,
            int gridHeight)
        {
            int clampedX = Mathf.Clamp(origin.X, 0, gridWidth  - footprintWidth);
            int clampedY = Mathf.Clamp(origin.Y, 0, gridHeight - footprintHeight);
            return new GridCoordinates(clampedX, clampedY);
        }

        // ─────────────────────────────────────────────
        //  World ↔ Grid (helper overloads)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Converts a world position to grid coordinates given grid parameters.
        /// Mirrors GridManager.WorldToGrid but accessible without an instance.
        /// Useful in Editor tools and tests.
        /// </summary>
        public static GridCoordinates WorldToGrid(
            Vector3 worldPosition,
            Vector3 gridOrigin,
            float   cellSize)
        {
            int x = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize);
            int y = Mathf.FloorToInt((worldPosition.z - gridOrigin.z) / cellSize);
            return new GridCoordinates(x, y);
        }

        /// <summary>
        /// Returns the world-space centre of a grid cell.
        /// Mirrors GridManager.GridToWorld — useful in Editor tools.
        /// </summary>
        public static Vector3 GridToWorld(
            GridCoordinates coords,
            Vector3         gridOrigin,
            float           cellSize)
        {
            return new Vector3(
                gridOrigin.x + coords.X * cellSize + cellSize * 0.5f,
                gridOrigin.y,
                gridOrigin.z + coords.Y * cellSize + cellSize * 0.5f
            );
        }

        // ─────────────────────────────────────────────
        //  Rotation Angle Helper
        // ─────────────────────────────────────────────

        /// <summary>
        /// Converts a rotation step (0–3) to a Euler Y-angle in degrees.
        /// </summary>
        public static float RotationStepToAngle(int rotationStep)
            => (rotationStep % 4) * 90f;

        /// <summary>
        /// Advances the rotation step by 1 (wraps at 4 back to 0).
        /// </summary>
        public static int NextRotationStep(int current) => (current + 1) % 4;
    }
}
