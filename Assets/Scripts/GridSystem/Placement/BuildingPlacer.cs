using CityScape.GridSystem.Core;
using CityScape.GridSystem.Data;
using CityScape.GridSystem.Highlight;
using CityScape.GridSystem.Interaction;
using CityScape.GridSystem.Road;
using CityScape.GridSystem.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;  // New Input System

namespace CityScape.GridSystem.Placement
{
    /// <summary>
    /// Orchestrates the entire building placement workflow:
    ///   - Reads mouse input via MouseWorldInteractor
    ///   - Updates the ghost preview via BuildingPreview
    ///   - Updates the cell highlight via GridHighlighter
    ///   - Validates placement via PlacementValidator
    ///   - Places / deletes buildings and mutates the grid via GridManager
    ///
    /// Input summary:
    ///   Left-click  → Place selected building
    ///   Right-click → Delete building under cursor
    ///   R key       → Rotate preview 90° clockwise
    ///   Escape      → Cancel / deselect building
    ///
    /// Public API (for UI buttons / hotkeys):
    ///   SelectBuilding(BuildingData data)
    ///   ClearSelection()
    ///
    /// Events (for Economy, UI, Audio):
    ///   OnBuildingPlaced(BuildingData, GridCoordinates)
    ///   OnBuildingRemoved(BuildingData, GridCoordinates)
    ///   OnSelectionChanged(BuildingData)    ← null when deselected
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        /// <summary>Static singleton so managers can access placed buildings without FindObjectOfType.</summary>
        public static BuildingPlacer Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector References
        // ─────────────────────────────────────────────

        [Header("System References")]
        [Tooltip("GridManager singleton — can be auto-found on Awake.")]
        [SerializeField] private GridManager gridManager;

        [Tooltip("Handles mouse-to-world raycasting.")]
        [SerializeField] private MouseWorldInteractor mouseInteractor;

        [Tooltip("Manages the transparent ghost preview.")]
        [SerializeField] private BuildingPreview buildingPreview;

        [Tooltip("Renders the coloured footprint highlight quad.")]
        [SerializeField] private GridHighlighter gridHighlighter;

        [Header("Input")]
        [Tooltip("Key to rotate the preview clockwise (New Input System Key enum).")]
        [SerializeField] private Key rotateKey = Key.R;

        [Tooltip("Key to cancel placement / deselect (New Input System Key enum).")]
        [SerializeField] private Key cancelKey = Key.Escape;

        [Header("Placed Buildings Container")]
        [Tooltip("Optional parent Transform for all placed building GameObjects. " +
                 "Keeps the hierarchy tidy. Leave empty to use the scene root.")]
        [SerializeField] private Transform placedBuildingsContainer;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>
        /// Fired after a building is successfully placed.
        /// Useful for: Economy deduction, UI feedback, audio, analytics.
        /// </summary>
        public event Action<BuildingData, GridCoordinates> OnBuildingPlaced;

        /// <summary>
        /// Fired after a building is deleted.
        /// Useful for: Economy refund, UI feedback, audio.
        /// </summary>
        public event Action<BuildingData, GridCoordinates> OnBuildingRemoved;

        /// <summary>
        /// Fired when the selected building type changes (null = deselected).
        /// Useful for: UI highlight the active palette button.
        /// </summary>
        public event Action<BuildingData> OnSelectionChanged;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private PlacementValidator _validator;

        /// <summary>The building type the player currently intends to place.</summary>
        private BuildingData _selectedBuilding;

        /// <summary>Current rotation: 0=0°, 1=90°, 2=180°, 3=270° (clockwise).</summary>
        private int _rotationStep;

        /// <summary>
        /// Master record of all placed buildings, keyed by the PRIMARY (origin) cell.
        /// Secondary cells point back to the same entry via the GridCell's OccupiedBy.
        /// </summary>
        private readonly Dictionary<GridCoordinates, PlacedBuilding> _placedBuildings
            = new Dictionary<GridCoordinates, PlacedBuilding>();

        /// <summary>Cached last-frame grid origin to reduce redundant validator calls.</summary>
        private GridCoordinates _lastPreviewOrigin;
        private bool _lastPreviewValid;

        /// <summary>
        /// Set to true in Awake only when ALL required references are valid.
        /// Prevents NullReferenceException spam in Update if the Inspector
        /// hasn't been fully wired up yet.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// Cached reference to the RoadPlacer in the scene.
        /// BuildingPlacer yields input control to RoadPlacer when road mode is active.
        /// Found lazily in Awake — no Inspector wiring required.
        /// </summary>
        private RoadPlacer _roadPlacer;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // Singleton registration
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _isInitialized = false;


            // ── 1. Auto-find GridManager ──────────────────────────────────
            if (gridManager == null)
                gridManager = GridManager.Instance ?? FindFirstObjectByType<GridManager>();

            // ── 2. Validate all required Inspector references ─────────────
            bool valid = true;

            if (gridManager == null)
            {
                Debug.LogError(
                    "[BuildingPlacer] <b>GridManager</b> is not assigned and could not be " +
                    "found in the scene.\n" +
                    "→ Add a GridManager component to a GameObject in the scene.", this);
                valid = false;
            }

            if (mouseInteractor == null)
            {
                Debug.LogError(
                    "[BuildingPlacer] <b>Mouse Interactor</b> field is empty.\n" +
                    "→ In the Inspector, drag the GameObject that has <b>MouseWorldInteractor</b> " +
                    "into the 'Mouse Interactor' slot on BuildingPlacer.", this);
                valid = false;
            }

            if (buildingPreview == null)
            {
                Debug.LogError(
                    "[BuildingPlacer] <b>Building Preview</b> field is empty.\n" +
                    "→ In the Inspector, drag the GameObject that has <b>BuildingPreview</b> " +
                    "into the 'Building Preview' slot on BuildingPlacer.", this);
                valid = false;
            }

            if (gridHighlighter == null)
            {
                Debug.LogError(
                    "[BuildingPlacer] <b>Grid Highlighter</b> field is empty.\n" +
                    "→ In the Inspector, drag the GameObject that has <b>GridHighlighter</b> " +
                    "into the 'Grid Highlighter' slot on BuildingPlacer.", this);
                valid = false;
            }

            if (!valid)
            {
                Debug.LogError(
                    "[BuildingPlacer] One or more required references are missing. " +
                    "BuildingPlacer has been <b>disabled</b> until all references are assigned.", this);
                enabled = false;
                return;
            }

            // ── 3. All good — create validator and mark ready ─────────────
            _validator     = new PlacementValidator(gridManager);
            _isInitialized = true;

            // ── 4. Guard against Key enum reset ───────────────────────────
            // When the field type changed from KeyCode → Key, Unity may have
            // serialized Key.None (value 0). Reset to safe defaults if so.
            if (rotateKey == Key.None)
            {
                rotateKey = Key.R;
                Debug.LogWarning("[BuildingPlacer] rotateKey was Key.None — reset to Key.R. " +
                                 "Set it manually in the Inspector if you want a different key.");
            }
            if (cancelKey == Key.None)
            {
                cancelKey = Key.Escape;
                Debug.LogWarning("[BuildingPlacer] cancelKey was Key.None — reset to Key.Escape. " +
                                 "Set it manually in the Inspector if you want a different key.");
            }

            Debug.Log("[BuildingPlacer] Initialised successfully. " +
                      $"RotateKey={rotateKey}  CancelKey={cancelKey}");
        }

        private void Update()
        {
            // Safety guard: never run if Awake validation failed
            if (!_isInitialized) return;

            // Lazy-find RoadPlacer the first time it's needed. Doing this in
            // Update (not Awake) avoids Script Execution Order dependency — both
            // scripts can Awake in any order and the reference will still resolve.
            if (_roadPlacer == null)
                _roadPlacer = FindFirstObjectByType<RoadPlacer>();

            // Yield all input to RoadPlacer while road mode is active.
            // This prevents accidental building placement during road drawing.
            if (_roadPlacer != null && _roadPlacer.IsRoadModeActive) return;

            HandleRotationInput();
            HandleCancelInput();
            HandleDeletionInput();   // ← always active: right-click deletes regardless of selection

            if (_selectedBuilding != null)
            {
                HandlePreviewUpdate();
                HandlePlacementInput();
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Selects a building type to place. Call this from UI buttons or hotkeys.
        /// Passing the same data again resets the rotation.
        /// </summary>
        public void SelectBuilding(BuildingData data)
        {
            if (data == null)
            {
                ClearSelection();
                return;
            }

            _selectedBuilding = data;
            _rotationStep     = 0;

            buildingPreview.SetBuilding(data);
            OnSelectionChanged?.Invoke(data);

            Debug.Log($"[BuildingPlacer] Selected: {data.buildingName}");
        }

        /// <summary>
        /// Cancels placement mode and hides the preview / highlight.
        /// </summary>
        public void ClearSelection()
        {
            _selectedBuilding = null;
            _rotationStep     = 0;

            buildingPreview.ClearPreview();
            gridHighlighter.Hide();

            OnSelectionChanged?.Invoke(null);
        }

        /// <summary>
        /// Returns a read-only snapshot of all currently placed buildings.
        /// Useful for save/load — serialize via PlacedBuilding.ToSaveData().
        /// </summary>
        public IReadOnlyDictionary<GridCoordinates, PlacedBuilding> GetPlacedBuildings()
            => _placedBuildings;

        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            // Clear existing buildings
            var existing = new List<PlacedBuilding>(_placedBuildings.Values);
            foreach (var b in existing)
            {
                DeleteBuilding(b);
            }

            // Restore from save
            var prevSelected = _selectedBuilding;
            var prevRotation = _rotationStep;

            foreach (var bData in data.buildings)
            {
                var buildingData = Managers.BuildingDatabase.Instance?.GetByID(bData.buildingID);
                if (buildingData != null)
                {
                    _selectedBuilding = buildingData;
                    _rotationStep = bData.rotationStep;
                    GridCoordinates origin = new GridCoordinates(bData.originX, bData.originY);
                    
                    // Directly place the building using the stored parameters
                    if (_validator.CanPlace(origin, _selectedBuilding, _rotationStep))
                    {
                        PlaceBuilding(origin);
                    }
                }
            }

            // Restore selection state
            _selectedBuilding = prevSelected;
            _rotationStep = prevRotation;
        }

        // ─────────────────────────────────────────────
        //  Input Handlers
        // ─────────────────────────────────────────────

        private void HandleRotationInput()
        {
            // Keyboard.current can be null if no keyboard device is connected
            if (_selectedBuilding != null
                && Keyboard.current != null
                && Keyboard.current[rotateKey].wasPressedThisFrame)
            {
                _rotationStep = GridUtility.NextRotationStep(_rotationStep);
                Debug.Log($"[BuildingPlacer] Rotation: {_rotationStep * 90}°");
            }
        }

        private void HandleCancelInput()
        {
            if (Keyboard.current != null && Keyboard.current[cancelKey].wasPressedThisFrame)
                ClearSelection();
        }

        private void HandlePlacementInput()
        {
            // Mouse.current can be null if no mouse is connected
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Block clicks that land on UI elements (e.g. palette buttons)
                if (UnityEngine.EventSystems.EventSystem.current != null
                    && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                if (mouseInteractor.TryGetGridCoordinates(out GridCoordinates origin))
                {
                    origin = AdjustedOrigin(origin);
                    TryPlaceBuilding(origin);
                }
            }
        }

        private void HandleDeletionInput()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (mouseInteractor.TryGetGridCoordinates(out GridCoordinates coords))
                    TryDeleteBuilding(coords);
            }
        }

        private void HandlePreviewUpdate()
        {
            if (!mouseInteractor.TryGetGridCoordinates(out GridCoordinates rawOrigin))
            {
                gridHighlighter.Hide();
                return;
            }

            // Adjust origin so the footprint is centred on the cursor cell
            GridCoordinates origin = AdjustedOrigin(rawOrigin);

            // Get effective footprint dimensions (accounts for rotation)
            var (w, h) = GridUtility.RotateFootprint(
                _selectedBuilding.footprintWidth,
                _selectedBuilding.footprintHeight,
                _rotationStep);

            bool isValid = _validator.CanPlace(origin, _selectedBuilding, _rotationStep);

            // Compute the world-space footprint centre, then apply the per-building
            // placement offset. This corrects pivot misalignment (XZ) and road Y sinking.
            Vector3 worldCenter = gridManager.GetFootprintCenter(origin, w, h)
                                  + _selectedBuilding.placementOffset;

            // Update ghost
            buildingPreview.UpdatePreview(worldCenter, _rotationStep, isValid);

            // Update highlight (highlight stays at ground level — no Y offset needed)
            gridHighlighter.Show(
                new Vector3(worldCenter.x, gridManager.GridOrigin.y, worldCenter.z),
                w, h, isValid, gridManager.CellSize);
        }

        // ─────────────────────────────────────────────
        //  Placement Logic
        // ─────────────────────────────────────────────

        private void TryPlaceBuilding(GridCoordinates origin)
        {
            if (!_validator.CanPlace(origin, _selectedBuilding, _rotationStep))
            {
                Debug.Log("[BuildingPlacer] Cannot place here — cell blocked or out of bounds.");
                return;
            }

            PlaceBuilding(origin);
        }

        private void PlaceBuilding(GridCoordinates origin)
        {
            // Gather all cells this footprint will occupy
            List<GridCoordinates> cells = _validator.GetOccupiedCells(
                origin, _selectedBuilding, _rotationStep);

            // Clear any procedurally generated nature (trees/grass) first
            foreach (GridCoordinates c in cells)
            {
                gridManager.ClearNature(c);
            }

            // Get footprint dimensions for positioning
            var (w, h) = GridUtility.RotateFootprint(
                _selectedBuilding.footprintWidth,
                _selectedBuilding.footprintHeight,
                _rotationStep);

            // Spawn the real building prefab.
            // Apply the full placement offset (pivot correction + road Y lowering).
            Vector3    worldCenter = gridManager.GetFootprintCenter(origin, w, h)
                                    + _selectedBuilding.placementOffset;
            Quaternion rotation    = Quaternion.Euler(0f, GridUtility.RotationStepToAngle(_rotationStep), 0f);

            GameObject instance = Instantiate(
                _selectedBuilding.prefab,
                worldCenter,
                rotation,
                placedBuildingsContainer);

            // Create the record
            var placed = new PlacedBuilding(
                _selectedBuilding,
                origin,
                _rotationStep,
                instance,
                cells);

            // Register in our dictionary (by origin) and mark cells in the grid
            _placedBuildings[origin] = placed;
            gridManager.OccupyCells(cells, placed);

            // Notify listeners
            OnBuildingPlaced?.Invoke(_selectedBuilding, origin);

            Debug.Log($"[BuildingPlacer] Placed {placed}");
        }

        // ─────────────────────────────────────────────
        //  Deletion Logic
        // ─────────────────────────────────────────────

        private void TryDeleteBuilding(GridCoordinates clickedCoords)
        {
            // Look up which building (if any) owns this cell
            GridCell cell = gridManager.GetCell(clickedCoords);
            if (cell == null || !cell.IsOccupied) return;

            PlacedBuilding target = cell.OccupiedBy;
            if (target == null) return;

            DeleteBuilding(target);
        }

        private void DeleteBuilding(PlacedBuilding target)
        {
            // Free all occupied cells
            gridManager.FreeCells(target.OccupiedCells);

            // Remove from dictionary
            _placedBuildings.Remove(target.Origin);

            // Destroy the GameObject
            Destroy(target.GameObject);

            // Notify listeners
            OnBuildingRemoved?.Invoke(target.Data, target.Origin);

            Debug.Log($"[BuildingPlacer] Deleted {target.Data.buildingName} at {target.Origin}");
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Adjusts the raw grid origin (mouse cell) so the building footprint
        /// is as centred on the cursor as possible, then clamps to grid bounds.
        /// </summary>
        private GridCoordinates AdjustedOrigin(GridCoordinates rawOrigin)
        {
            var (w, h) = GridUtility.RotateFootprint(
                _selectedBuilding.footprintWidth,
                _selectedBuilding.footprintHeight,
                _rotationStep);

            // Shift so the footprint is centred on the hovered cell
            int adjustedX = rawOrigin.X - w / 2;
            int adjustedY = rawOrigin.Y - h / 2;

            return GridUtility.ClampToGrid(
                new GridCoordinates(adjustedX, adjustedY),
                w, h,
                gridManager.GridWidth,
                gridManager.GridHeight);
        }
    }
}
