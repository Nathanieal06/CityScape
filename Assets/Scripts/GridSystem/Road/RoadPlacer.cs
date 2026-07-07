using CityScape.GridSystem.Core;
using CityScape.GridSystem.Interaction;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CityScape.GridSystem.Road
{
    /// <summary>
    /// Drag-to-build road placement system.
    ///
    /// How to use:
    ///   1. Press B (or your chosen toggleRoadModeKey) OR call EnterRoadMode() from a UI button.
    ///   2. Left-click and drag across the grid — roads appear on every cell the cursor passes.
    ///   3. Right-click removes a road tile under the cursor.
    ///   4. Press Escape or call ExitRoadMode() to return to normal mode.
    ///
    /// Inspector wiring:
    ///   Mouse Interactor      → MouseWorldInteractor in scene
    ///   Straight Road Prefab  → MainRoad.prefab        (required)
    ///   Corner Road Prefab    → CurveTurnRoad.prefab   (required)
    ///   T Junction Prefab     → your T-junction prefab (optional, falls back to Straight)
    ///   Cross Road Prefab     → optional, falls back to Straight
    ///   Road Container        → optional empty GO to parent all road tiles under
    /// </summary>
    public class RoadPlacer : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector Fields
        // ─────────────────────────────────────────────

        [Header("System References")]
        [Tooltip("MouseWorldInteractor in the scene.")]
        [SerializeField] private MouseWorldInteractor mouseInteractor;

        [Header("Road Prefabs")]
        [Tooltip("Straight road tile (MainRoad.prefab). REQUIRED.")]
        [SerializeField] private GameObject straightRoadPrefab;

        [Tooltip("Corner / turn road tile (CurveTurnRoad.prefab). REQUIRED.")]
        [SerializeField] private GameObject cornerRoadPrefab;

        [Tooltip("T-Junction tile. Optional — falls back to Straight if empty.")]
        [SerializeField] private GameObject tJunctionRoadPrefab;

        [Tooltip("Cross / intersection tile. Optional — falls back to Straight if empty.")]
        [SerializeField] private GameObject crossRoadPrefab;

        [Tooltip("Dead-end cap tile. Optional — falls back to Straight if empty.")]
        [SerializeField] private GameObject deadEndRoadPrefab;

        [Header("Placement Settings")]
        [Tooltip("World-space Y offset added when spawning road tiles.")]
        [SerializeField] private float roadHeightOffset = 0f;

        [Tooltip("How many grid cells each road tile covers per side (e.g. 2 = 2x2 footprint). " +
                 "Must match the physical size of your road prefab.")]
        [SerializeField, Min(1)] private int roadFootprintSize = 2;

        [Header("Input")]
        [Tooltip("Key to toggle road build mode on/off. Default: B")]
        [SerializeField] private Key toggleRoadModeKey = Key.B;

        [Header("Road Container")]
        [Tooltip("Optional parent Transform for all road GameObjects.")]
        [SerializeField] private Transform roadContainer;

        [Header("Hover Highlight")]
        [Tooltip("Semi-transparent material shown under the cursor while in road mode.")]
        [SerializeField] private Material hoverHighlightMaterial;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired when road mode is toggled. True = entered, False = exited.</summary>
        public event Action<bool> OnRoadModeChanged;

        /// <summary>Fired when a road tile is successfully placed.</summary>
        public event Action<GridCoordinates> OnRoadPlaced;

        /// <summary>Fired when a road tile is removed.</summary>
        public event Action<GridCoordinates> OnRoadRemoved;

        // ─────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────

        /// <summary>Whether road build mode is currently active.</summary>
        public bool IsRoadModeActive { get; private set; }

        private readonly Dictionary<GridCoordinates, PlacedRoad> _placedRoads
            = new Dictionary<GridCoordinates, PlacedRoad>();

        // Per-stroke deduplication (cleared on each new press)
        private readonly HashSet<GridCoordinates> _visitedThisStroke
            = new HashSet<GridCoordinates>();

        // Bresenham gap-fill tracking (in block-space: divide by roadFootprintSize)
        private GridCoordinates _lastStrokeBlock;
        private bool            _hasLastStrokeBlock;
#pragma warning disable CS0414 // field assigned but value never used — reserved for future terrain-start guard
        private bool            _strokeStartedOnTerrain;
#pragma warning restore CS0414

        // Hover quad
        private GameObject   _hoverObj;
        private MeshRenderer _hoverRenderer;
        private bool         _hoverVisible;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        public static RoadPlacer Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Use Start (not Awake) so GridManager.Instance is guaranteed to exist.
            LogMissingRefs();
            BuildHoverQuad();
            Debug.Log("[RoadPlacer] Ready. Press B to toggle road mode.");
        }

        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            // Remove all existing roads
            var existing = new List<GridCoordinates>(_placedRoads.Keys);
            foreach (var coords in existing)
            {
                TryRemoveRoad(coords, gm);
            }

            _visitedThisStroke.Clear();

            // Force place roads
            foreach (var rData in data.roads)
            {
                GridCoordinates origin = new GridCoordinates(rData.gridX, rData.gridY);
                int s = roadFootprintSize;
                for (int dy = 0; dy < s; dy++)
                {
                    for (int dx = 0; dx < s; dx++)
                    {
                        GridCoordinates c = new GridCoordinates(origin.X + dx, origin.Y + dy);
                        gm.ClearNature(c);
                        gm.PlaceRoad(c);
                    }
                }
                SpawnOrUpdateRoadTile(origin, gm);
            }

            // Refresh all roads so they connect correctly
            var allRoads = new List<GridCoordinates>(_placedRoads.Keys);
            foreach (var coords in allRoads)
            {
                SpawnOrUpdateRoadTile(coords, gm);
            }
        }

        private void Update()
        {
            // Toggle key and Escape always work regardless of road mode state
            HandleToggleInput();

            // Right-click removes roads anytime, even outside road mode
            HandleRemoval();

            if (!IsRoadModeActive) return;

            // ── Hover highlight ──────────────────────────────────────
            UpdateHoverHighlight();

            // ── Left-click / drag → place roads ─────────────────────
            HandlePlacement();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Activates road build mode. Call from UI buttons.</summary>
        public void EnterRoadMode()
        {
            if (IsRoadModeActive) return;
            IsRoadModeActive = true;
            OnRoadModeChanged?.Invoke(true);
            Debug.Log("[RoadPlacer] Road mode ON — click-drag on the grid to draw roads.");
        }

        /// <summary>Deactivates road build mode.</summary>
        public void ExitRoadMode()
        {
            if (!IsRoadModeActive) return;
            IsRoadModeActive = false;
            _hasLastStrokeBlock = false;
            _visitedThisStroke.Clear();
            SetHoverVisible(false);
            OnRoadModeChanged?.Invoke(false);
            Debug.Log("[RoadPlacer] Road mode OFF.");
        }

        /// <summary>Read-only snapshot of all placed roads (for save/load).</summary>
        public IReadOnlyDictionary<GridCoordinates, PlacedRoad> GetPlacedRoads()
            => _placedRoads;

        // ─────────────────────────────────────────────
        //  Input: Toggle
        // ─────────────────────────────────────────────

        private void HandleToggleInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[toggleRoadModeKey].wasPressedThisFrame)
            {
                if (IsRoadModeActive) ExitRoadMode();
                else                  EnterRoadMode();
            }

            if (Keyboard.current[Key.Escape].wasPressedThisFrame)
                ExitRoadMode();
        }

        // ─────────────────────────────────────────────
        //  Input: Road Placement (click + drag)
        // ─────────────────────────────────────────────

        private void HandlePlacement()
        {
            if (Mouse.current == null) return;

            // ── On a new mouse press, reset the stroke ───────────────
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // If they click on a UI element (e.g. another button), exit road mode.
                // We ignore the click that ACTIVATES road mode because that typically
                // happens on mouse release, so wasPressedThisFrame is false when entering.
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    ExitRoadMode();
                    return;
                }

                _strokeStartedOnTerrain = true;
                _visitedThisStroke.Clear();
                _hasLastStrokeBlock = false;
            }

            // ── On button release, close the stroke ──────────────────
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _hasLastStrokeBlock = false;
                _visitedThisStroke.Clear();
                _strokeStartedOnTerrain = false;
            }

            // ── While the button is held, place roads ────────────────
            if (!Mouse.current.leftButton.isPressed) return;
            if (mouseInteractor == null || !mouseInteractor.HasValidHit) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            // Snap the cursor cell to the block grid
            GridCoordinates rawCell     = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
            GridCoordinates currentBlock = SnapToBlock(rawCell);

            if (_hasLastStrokeBlock)
            {
                // Bresenham in block-space so every block the cursor passes is filled
                int s = roadFootprintSize;
                GridCoordinates fromBS = new GridCoordinates(_lastStrokeBlock.X / s, _lastStrokeBlock.Y / s);
                GridCoordinates toBS   = new GridCoordinates(currentBlock.X   / s, currentBlock.Y   / s);

                foreach (GridCoordinates blockCoord in BresenhamLine(fromBS, toBS))
                    TryPlaceRoad(new GridCoordinates(blockCoord.X * s, blockCoord.Y * s), gm);
            }
            else
            {
                TryPlaceRoad(currentBlock, gm);
            }

            _lastStrokeBlock    = currentBlock;
            _hasLastStrokeBlock = true;
        }

        // ─────────────────────────────────────────────
        //  Input: Road Removal
        // ─────────────────────────────────────────────

        private void HandleRemoval()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.rightButton.wasPressedThisFrame) return;
            if (mouseInteractor == null || !mouseInteractor.HasValidHit) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            GridCoordinates rawCell = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
            TryRemoveRoad(SnapToBlock(rawCell), gm);
        }

        // ─────────────────────────────────────────────
        //  Road Placement Logic
        // ─────────────────────────────────────────────

        private void TryPlaceRoad(GridCoordinates blockOrigin, GridManager gm)
        {
            // blockOrigin is the bottom-left cell of the NxN footprint.
            // Deduplicate: skip if this block was already placed this stroke.
            if (_visitedThisStroke.Contains(blockOrigin)) return;
            _visitedThisStroke.Add(blockOrigin);

            // Validate every cell in the footprint before placing anything
            int s = roadFootprintSize;
            for (int dy = 0; dy < s; dy++)
            {
                for (int dx = 0; dx < s; dx++)
                {
                    GridCoordinates c = new GridCoordinates(blockOrigin.X + dx, blockOrigin.Y + dy);
                    GridCell cell = gm.GetCell(c);
                    if (cell == null || cell.HasRoad || cell.IsOccupied)
                        return;  // any blocked cell cancels the whole block
                }
            }

            if (straightRoadPrefab == null)
            {
                Debug.LogError("[RoadPlacer] Straight Road Prefab is not assigned!", this);
                return;
            }

            // Clear nature and mark all cells in the footprint as road
            for (int dy = 0; dy < s; dy++)
            {
                for (int dx = 0; dx < s; dx++)
                {
                    GridCoordinates c = new GridCoordinates(blockOrigin.X + dx, blockOrigin.Y + dy);
                    gm.ClearNature(c);
                    gm.PlaceRoad(c);
                }
            }

            // Spawn one prefab at the block centre
            SpawnOrUpdateRoadTile(blockOrigin, gm);

            // Refresh neighbouring blocks so auto-connect shapes update
            foreach (GridCoordinates n in RoadTileSelector.GetBlockNeighbours(blockOrigin, s))
                RefreshNeighbour(n, gm);

            OnRoadPlaced?.Invoke(blockOrigin);
        }

        private void TryRemoveRoad(GridCoordinates blockOrigin, GridManager gm)
        {
            // Find the block: the clicked cell might be anywhere inside it,
            // but blockOrigin is already snapped so just check its top-left cell.
            GridCell cell = gm.GetCell(blockOrigin);
            if (cell == null || !cell.HasRoad) return;

            // Clear all cells in the footprint
            int s = roadFootprintSize;
            for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                    gm.RemoveRoad(new GridCoordinates(blockOrigin.X + dx, blockOrigin.Y + dy));

            if (_placedRoads.TryGetValue(blockOrigin, out PlacedRoad pr))
            {
                if (pr.GameObject != null) Destroy(pr.GameObject);
                _placedRoads.Remove(blockOrigin);
            }

            foreach (GridCoordinates n in RoadTileSelector.GetBlockNeighbours(blockOrigin, s))
                RefreshNeighbour(n, gm);

            OnRoadRemoved?.Invoke(blockOrigin);
        }

        // ─────────────────────────────────────────────
        //  Tile Spawning
        // ─────────────────────────────────────────────

        private void SpawnOrUpdateRoadTile(GridCoordinates blockOrigin, GridManager gm)
        {
            if (_placedRoads.TryGetValue(blockOrigin, out PlacedRoad existing))
            {
                if (existing.GameObject != null) Destroy(existing.GameObject);
                _placedRoads.Remove(blockOrigin);
            }

            int  s      = roadFootprintSize;
            RoadTileType type   = RoadTileSelector.Evaluate(blockOrigin, s, gm, out float rotY);
            GameObject   prefab = SelectPrefab(type);

            // Position at the world-space centre of the NxN block
            Vector3    pos = gm.GetFootprintCenter(blockOrigin, s, s) + Vector3.up * roadHeightOffset;
            Quaternion rot = Quaternion.Euler(0f, rotY, 0f);

            GameObject go = Instantiate(prefab, pos, rot, roadContainer);
            go.name = $"Road_{blockOrigin.X}_{blockOrigin.Y}";

            _placedRoads[blockOrigin] = new PlacedRoad(blockOrigin, go, type);
        }

        private void RefreshNeighbour(GridCoordinates blockOrigin, GridManager gm)
        {
            // Only refresh if the top-left cell of this block is a road
            GridCell cell = gm.GetCell(blockOrigin);
            if (cell == null || !cell.HasRoad) return;
            SpawnOrUpdateRoadTile(blockOrigin, gm);
        }

        private GameObject SelectPrefab(RoadTileType type) => type switch
        {
            RoadTileType.Corner    => cornerRoadPrefab    ?? straightRoadPrefab,
            RoadTileType.TJunction => tJunctionRoadPrefab ?? straightRoadPrefab,
            RoadTileType.Cross     => crossRoadPrefab     ?? straightRoadPrefab,
            RoadTileType.DeadEnd   => deadEndRoadPrefab   ?? straightRoadPrefab,
            _                      => straightRoadPrefab,
        };

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Snaps a raw grid coordinate to the bottom-left corner of the
        /// NxN block it falls inside (e.g. cell (3,5) with size=2 → block (2,4)).
        /// </summary>
        private GridCoordinates SnapToBlock(GridCoordinates raw)
        {
            int s = roadFootprintSize;
            return new GridCoordinates(
                (raw.X / s) * s,
                (raw.Y / s) * s);
        }

        // ─────────────────────────────────────────────
        //  Bresenham Line Fill
        // ─────────────────────────────────────────────

        /// <summary>
        /// Yields every grid cell on the line from <paramref name="from"/> to
        /// <paramref name="to"/> using Bresenham's algorithm.
        /// Ensures fast mouse drags produce a solid connected road with no gaps.
        /// </summary>
        private static IEnumerable<GridCoordinates> BresenhamLine(
            GridCoordinates from, GridCoordinates to)
        {
            int x0 = from.X, y0 = from.Y;
            int x1 = to.X,   y1 = to.Y;

            int dx  = Math.Abs(x1 - x0);
            int dy  = Math.Abs(y1 - y0);
            int sx  = x0 < x1 ? 1 : -1;
            int sy  = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                yield return new GridCoordinates(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        // ─────────────────────────────────────────────
        //  Hover Highlight
        // ─────────────────────────────────────────────

        private void BuildHoverQuad()
        {
            _hoverObj = new GameObject("RoadHoverHighlight");
            _hoverObj.transform.SetParent(transform, false);

            var mf = _hoverObj.AddComponent<MeshFilter>();
            _hoverRenderer = _hoverObj.AddComponent<MeshRenderer>();
            _hoverRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hoverRenderer.receiveShadows    = false;

            // Size the quad to cover the full NxN footprint with a small padding
            float cs  = GridManager.Instance != null ? GridManager.Instance.CellSize : 4f;
            float size = cs * roadFootprintSize * 0.96f;
            float h   = size * 0.5f;

            var mesh = new Mesh { name = "RoadHover" };
            mesh.vertices  = new[] {
                new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
                new Vector3( h, 0f,  h), new Vector3(-h, 0f, h)
            };
            mesh.triangles = new[] { 0, 3, 1, 1, 3, 2 };
            mesh.uv        = new[] {
                new Vector2(0,0), new Vector2(1,0),
                new Vector2(1,1), new Vector2(0,1)
            };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            if (hoverHighlightMaterial != null)
                _hoverRenderer.sharedMaterial = hoverHighlightMaterial;

            _hoverObj.SetActive(false);
        }

        private void UpdateHoverHighlight()
        {
            if (_hoverObj == null) return;
            if (mouseInteractor == null || !mouseInteractor.HasValidHit)
            {
                SetHoverVisible(false);
                return;
            }

            GridManager gm = GridManager.Instance;
            if (gm == null) { SetHoverVisible(false); return; }

            // Snap the hovered cell to the block grid so the highlight
            // shows exactly where the 2x2 road would be placed.
            GridCoordinates rawCell    = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
            GridCoordinates blockOrigin = SnapToBlock(rawCell);

            if (gm.GetCell(blockOrigin) == null) { SetHoverVisible(false); return; }

            // Use the block centre so the quad sits over all NxN cells
            int     s    = roadFootprintSize;
            Vector3 wp   = gm.GetFootprintCenter(blockOrigin, s, s);
            _hoverObj.transform.position = new Vector3(wp.x, wp.y + 0.04f, wp.z);
            SetHoverVisible(true);
        }

        private void SetHoverVisible(bool visible)
        {
            if (_hoverObj == null) return;
            if (_hoverVisible == visible) return;
            _hoverObj.SetActive(visible);
            _hoverVisible = visible;
        }

        // ─────────────────────────────────────────────
        //  Diagnostics
        // ─────────────────────────────────────────────

        private void LogMissingRefs()
        {
            if (mouseInteractor == null)
                Debug.LogError("[RoadPlacer] Mouse Interactor is NOT assigned in Inspector. " +
                               "Drag the MouseWorldInteractor GameObject into the slot.", this);

            if (straightRoadPrefab == null)
                Debug.LogError("[RoadPlacer] Straight Road Prefab is NOT assigned. " +
                               "Drag MainRoad.prefab into the slot.", this);

            if (cornerRoadPrefab == null)
                Debug.LogError("[RoadPlacer] Corner Road Prefab is NOT assigned. " +
                               "Drag CurveTurnRoad.prefab into the slot.", this);
        }

        private void OnDestroy()
        {
            if (_hoverObj != null) Destroy(_hoverObj);
        }
    }
}
