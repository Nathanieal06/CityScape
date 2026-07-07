using CityScape.GridSystem.Data;
using CityScape.GridSystem.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityScape.GridSystem.Core
{
    /// <summary>
    /// Central authority for the game's world grid.
    ///
    /// Responsibilities:
    ///  - Allocates and owns the 2-D array of GridCell objects.
    ///  - Provides world ↔ grid coordinate conversions.
    ///  - Exposes occupancy mutation methods (Occupy / Free) that fire events.
    ///  - Draws the grid Gizmo in the Scene view.
    ///
    /// SOLID: Single class per responsibility. Placement logic lives in
    ///        BuildingPlacer, validation in PlacementValidator.
    ///
    /// Singleton: Accessed via GridManager.Instance. Only one grid per scene.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static GridManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector-Configurable Fields
        // ─────────────────────────────────────────────

        [Header("Grid Dimensions")]
        [Tooltip("Number of columns (along world X).")]
        [SerializeField, Min(1)] private int gridWidth  = 20;

        [Tooltip("Number of rows (along world Z).")]
        [SerializeField, Min(1)] private int gridHeight = 20;

        [Tooltip("World-space size of each square cell (in metres).")]
        [SerializeField, Min(0.1f)] private float cellSize = 4f;

        [Tooltip("World-space position of the grid's bottom-left corner.")]
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Gizmos")]
        [Tooltip("Draw the grid gizmo even when the GridManager object is not selected.")]
        [SerializeField] private bool alwaysDrawGizmos = true;

        [Tooltip("Colour of empty grid lines in the Scene view.")]
        [SerializeField] private Color gizmoLineColor = new Color(1f, 1f, 1f, 0.15f);

        [Tooltip("Colour used to highlight occupied cells in the Scene view.")]
        [SerializeField] private Color gizmoOccupiedColor = new Color(1f, 0.2f, 0.2f, 0.35f);

        // ─────────────────────────────────────────────
        //  Properties (read-only access to config)
        // ─────────────────────────────────────────────

        public int   GridWidth  => gridWidth;
        public int   GridHeight => gridHeight;
        public float CellSize   => cellSize;
        public Vector3 GridOrigin => gridOrigin;

        // ─────────────────────────────────────────────
        //  Internal State
        // ─────────────────────────────────────────────

        /// <summary>The backing 2-D array of cells. [x, y] indexed.</summary>
        private GridCell[,] _cells;

        // ─────────────────────────────────────────────
        //  Events (for Economy, NPC, & future systems)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Fired when a cell is newly occupied by a building.
        /// Subscribe from Economy, Population, Utility systems.
        /// </summary>
        public event Action<GridCoordinates, PlacedBuilding> OnCellOccupied;

        /// <summary>
        /// Fired when a cell is freed (building deleted).
        /// </summary>
        public event Action<GridCoordinates> OnCellFreed;

        /// <summary>
        /// Fired when a road is placed on a cell.
        /// Subscribe from Road, Traffic, and Pathfinding systems.
        /// </summary>
        public event Action<GridCoordinates> OnRoadPlaced;

        /// <summary>
        /// Fired when a road is removed from a cell.
        /// </summary>
        public event Action<GridCoordinates> OnRoadRemoved;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // Enforce singleton
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GridManager] Duplicate instance destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitialiseGrid();
        }

        // ─────────────────────────────────────────────
        //  Initialisation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Allocates the cell array and populates each GridCell with its
        /// coordinates.  Called once on Awake.
        /// </summary>
        private void InitialiseGrid()
        {
            _cells = new GridCell[gridWidth, gridHeight];

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    _cells[x, y] = new GridCell(new GridCoordinates(x, y));
                }
            }

            Debug.Log($"[GridManager] Grid initialised: {gridWidth}×{gridHeight}, " +
                      $"cellSize={cellSize}, origin={gridOrigin}");
        }

        // ─────────────────────────────────────────────
        //  Cell Access
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the GridCell at the given coordinates,
        /// or null if the coordinates are out of bounds.
        /// </summary>
        public GridCell GetCell(GridCoordinates coords)
            => IsInsideGrid(coords) ? _cells[coords.X, coords.Y] : null;

        /// <summary>Returns the GridCell at (x, y) or null if out of bounds.</summary>
        public GridCell GetCell(int x, int y)
            => GetCell(new GridCoordinates(x, y));

        // ─────────────────────────────────────────────
        //  Coordinate Conversion
        // ─────────────────────────────────────────────

        /// <summary>
        /// Converts a world-space position to grid coordinates.
        /// The Y component of worldPosition is ignored (flat grid).
        /// </summary>
        public GridCoordinates WorldToGrid(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize);
            int y = Mathf.FloorToInt((worldPosition.z - gridOrigin.z) / cellSize);
            return new GridCoordinates(x, y);
        }

        /// <summary>
        /// Returns the world-space centre of the given grid cell.
        /// Y is set to the grid origin's Y (ground level).
        /// </summary>
        public Vector3 GridToWorld(GridCoordinates coords)
        {
            return new Vector3(
                gridOrigin.x + coords.X * cellSize + cellSize * 0.5f,
                gridOrigin.y,
                gridOrigin.z + coords.Y * cellSize + cellSize * 0.5f
            );
        }

        /// <summary>
        /// Returns the world-space centre of the combined footprint of
        /// multiple cells — used to position the building GameObject.
        /// </summary>
        public Vector3 GetFootprintCenter(GridCoordinates origin, int width, int height)
        {
            return new Vector3(
                gridOrigin.x + origin.X * cellSize + width  * cellSize * 0.5f,
                gridOrigin.y,
                gridOrigin.z + origin.Y * cellSize + height * cellSize * 0.5f
            );
        }

        // ─────────────────────────────────────────────
        //  Bounds Check
        // ─────────────────────────────────────────────

        /// <summary>Returns true if the given coordinates fall within the grid.</summary>
        public bool IsInsideGrid(GridCoordinates coords)
            => coords.X >= 0 && coords.X < gridWidth
            && coords.Y >= 0 && coords.Y < gridHeight;

        // ─────────────────────────────────────────────
        //  Occupancy Mutation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Marks a single cell as occupied and fires OnCellOccupied.
        /// Call this for every cell in the building's footprint.
        /// </summary>
        public void OccupyCell(GridCoordinates coords, PlacedBuilding building)
        {
            var cell = GetCell(coords);
            if (cell == null)
            {
                Debug.LogError($"[GridManager] OccupyCell: {coords} is out of bounds.");
                return;
            }

            cell.Occupy(building);
            OnCellOccupied?.Invoke(coords, building);
        }

        /// <summary>
        /// Frees a single cell and fires OnCellFreed.
        /// Call this for every cell in the building's footprint.
        /// </summary>
        public void FreeCell(GridCoordinates coords)
        {
            var cell = GetCell(coords);
            if (cell == null)
            {
                Debug.LogError($"[GridManager] FreeCell: {coords} is out of bounds.");
                return;
            }

            cell.Free();
            OnCellFreed?.Invoke(coords);
        }

        // ─────────────────────────────────────────────
        //  Nature Mutation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Destroys any tree or grass on this cell and clears the reference.
        /// Call this before placing buildings or roads.
        /// </summary>
        public void ClearNature(GridCoordinates coords)
        {
            var cell = GetCell(coords);
            if (cell != null && cell.HasNature)
            {
                if (Application.isPlaying)
                    Destroy(cell.NatureObject);
                else
                    DestroyImmediate(cell.NatureObject);
                
                cell.NatureObject = null;
            }
        }

        // ─────────────────────────────────────────────
        //  Road Mutation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Marks a cell as having a road and fires OnRoadPlaced.
        /// Returns false if the cell is out of bounds or already occupied by a building.
        /// </summary>
        public bool PlaceRoad(GridCoordinates coords)
        {
            var cell = GetCell(coords);
            if (cell == null)
            {
                Debug.LogWarning($"[GridManager] PlaceRoad: {coords} is out of bounds.");
                return false;
            }

            if (cell.IsOccupied)
            {
                Debug.LogWarning($"[GridManager] PlaceRoad: {coords} is occupied by a building — skipping.");
                return false;
            }

            cell.HasRoad = true;
            OnRoadPlaced?.Invoke(coords);
            return true;
        }

        /// <summary>
        /// Clears the road flag on a cell and fires OnRoadRemoved.
        /// Does NOT destroy the road GameObject — that is the caller's responsibility.
        /// </summary>
        public bool RemoveRoad(GridCoordinates coords)
        {
            var cell = GetCell(coords);
            if (cell == null)
            {
                Debug.LogWarning($"[GridManager] RemoveRoad: {coords} is out of bounds.");
                return false;
            }

            cell.HasRoad = false;
            OnRoadRemoved?.Invoke(coords);
            return true;
        }

        // ─────────────────────────────────────────────
        //  Bulk Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Occupies all cells in the provided list with the same building.
        /// Convenience wrapper around OccupyCell.
        /// </summary>
        public void OccupyCells(IEnumerable<GridCoordinates> cells, PlacedBuilding building)
        {
            foreach (var c in cells)
                OccupyCell(c, building);
        }

        /// <summary>
        /// Frees all cells in the provided list.
        /// Convenience wrapper around FreeCell.
        /// </summary>
        public void FreeCells(IEnumerable<GridCoordinates> cells)
        {
            foreach (var c in cells)
                FreeCell(c);
        }

        // ─────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!alwaysDrawGizmos && !Application.isPlaying) return;
            DrawGridGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (alwaysDrawGizmos) return; // already drawn
            DrawGridGizmos();
        }

        private void DrawGridGizmos()
        {
            // Draw vertical lines (along Z)
            Gizmos.color = gizmoLineColor;
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = gridOrigin + new Vector3(x * cellSize, 0f, 0f);
                Vector3 end   = start + new Vector3(0f, 0f, gridHeight * cellSize);
                Gizmos.DrawLine(start, end);
            }

            // Draw horizontal lines (along X)
            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = gridOrigin + new Vector3(0f, 0f, y * cellSize);
                Vector3 end   = start + new Vector3(gridWidth * cellSize, 0f, 0f);
                Gizmos.DrawLine(start, end);
            }

            // Highlight occupied cells (only in Play Mode to avoid cost in editor)
            if (Application.isPlaying && _cells != null)
            {
                Gizmos.color = gizmoOccupiedColor;
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        if (_cells[x, y].IsOccupied)
                        {
                            Vector3 center = GridToWorld(new GridCoordinates(x, y));
                            Gizmos.DrawCube(
                                center + Vector3.up * 0.02f,
                                new Vector3(cellSize * 0.95f, 0.04f, cellSize * 0.95f));
                        }
                    }
                }
            }

            DrawDebugFootprints();
        }

        // ─────────────────────────────
        //  Debug Footprint Gizmo
        // ─────────────────────────────

        [Header("Debug Calibration")]
        [Tooltip(
            "Drag BuildingData assets here and tick their 'Debug Show Footprint' checkbox\n" +
            "to display a yellow wireframe box showing the exact world-space footprint.\n" +
            "Use this to calibrate footprintWidth/Height and placementOffset.")]
        [SerializeField] private BuildingData[] debugBuildingDatas;

        /// <summary>
        /// Draws a yellow wireframe box at grid cell (0,0) for every BuildingData
        /// that has debugShowFootprint = true.  Use this to visually confirm that
        /// the footprint dimensions match the 3-D mesh, then uncheck the flag.
        /// </summary>
        private void DrawDebugFootprints()
        {
            if (debugBuildingDatas == null) return;

            foreach (BuildingData data in debugBuildingDatas)
            {
                if (data == null || !data.debugShowFootprint) continue;

                float worldW = data.footprintWidth  * cellSize;
                float worldH = data.footprintHeight * cellSize;

                // Centre of footprint at origin cell, plus the placement offset
                Vector3 boxCenter = new Vector3(
                    gridOrigin.x + worldW * 0.5f,
                    gridOrigin.y + 0.5f,
                    gridOrigin.z + worldH * 0.5f)
                    + data.placementOffset;

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(boxCenter, new Vector3(worldW, 1f, worldH));

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    boxCenter + Vector3.up,
                    $"{data.buildingName}\n" +
                    $"{data.footprintWidth}x{data.footprintHeight} cells = {worldW}x{worldH} units");
#endif
            }
        }
    }
}
