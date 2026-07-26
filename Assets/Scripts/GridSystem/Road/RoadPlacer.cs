using CityScape.GridSystem.Core;
using CityScape.GridSystem.Interaction;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CityScape.GridSystem.Road
{
    public class RoadPlacer : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector Fields
        // ─────────────────────────────────────────────

        [Header("System References")]
        [SerializeField] private MouseWorldInteractor mouseInteractor;

        [Header("Road Prefabs")]
        [SerializeField] private GameObject straightRoadPrefab;
        [SerializeField] private GameObject cornerRoadPrefab;
        [SerializeField] private GameObject tJunctionRoadPrefab;
        [Tooltip("Used when four roads connect. Assign a crossroad or circular roundabout prefab here.")]
        [UnityEngine.Serialization.FormerlySerializedAs("crossRoadPrefab")]
        [SerializeField] private GameObject fourWayIntersectionPrefab;
        [SerializeField] private GameObject deadEndRoadPrefab;

        [Header("Placement Settings")]
        [SerializeField] private float roadHeightOffset = 0f;
        [SerializeField, Min(1)] private int roadFootprintSize = 2;

        [Header("Input")]
        [SerializeField] private Key toggleRoadModeKey = Key.B;

        [Header("Road Container")]
        [SerializeField] private Transform roadContainer;

        [Header("Hover Highlight")]
        [Tooltip("Semi-transparent material for the 3D ghost preview.")]
        [SerializeField] private Material hoverHighlightMaterial;
        [Tooltip("Optional: Material used for the red delete highlight quad (auto-generated if empty).")]
        [SerializeField] private Material deleteHighlightMaterial;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        public event Action<bool> OnRoadModeChanged;
        public event Action<GridCoordinates> OnRoadPlaced;
        public event Action<GridCoordinates> OnRoadRemoved;

        // ─────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────

        public bool IsRoadModeActive { get; private set; }

        private readonly Dictionary<GridCoordinates, PlacedRoad> _placedRoads
            = new Dictionary<GridCoordinates, PlacedRoad>();

        // Placement dragging
        private bool _isDraggingPlacement;
        private GridCoordinates _dragStartBlock;
        private List<GridCoordinates> _currentPreviewPath = new List<GridCoordinates>();

        // Removal dragging
        private bool _isDraggingRemoval;

        // Visuals
        private GameObject _deleteQuadObj;
        private MeshRenderer _deleteQuadRenderer;
        
        private Transform _ghostContainer;
        private List<GameObject> _ghostPool = new List<GameObject>();

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
            LogMissingRefs();
            BuildDeleteQuad();
            
            _ghostContainer = new GameObject("RoadGhosts").transform;
            _ghostContainer.SetParent(transform, false);
            
            Debug.Log("[RoadPlacer] Ready. Press B to toggle road mode.");
        }

        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            var existing = new List<GridCoordinates>(_placedRoads.Keys);
            foreach (var coords in existing)
            {
                TryRemoveRoad(coords, gm);
            }

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

            var allRoads = new List<GridCoordinates>(_placedRoads.Keys);
            foreach (var coords in allRoads)
            {
                SpawnOrUpdateRoadTile(coords, gm);
            }
        }

        private void Update()
        {
            HandleToggleInput();

            if (!IsRoadModeActive) return;

            // Handle logic
            HandleRemoval();
            HandlePlacement();
            UpdateHoverVisuals();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        public void EnterRoadMode()
        {
            if (IsRoadModeActive) return;
            IsRoadModeActive = true;
            OnRoadModeChanged?.Invoke(true);
            Debug.Log("[RoadPlacer] Road mode ON — click-drag on the grid to draw roads.");
        }

        public void ExitRoadMode()
        {
            if (!IsRoadModeActive) return;
            IsRoadModeActive = false;
            
            _isDraggingPlacement = false;
            _isDraggingRemoval = false;
            _currentPreviewPath.Clear();
            HideAllGhosts();
            if (_deleteQuadObj != null) _deleteQuadObj.SetActive(false);
            
            OnRoadModeChanged?.Invoke(false);
            Debug.Log("[RoadPlacer] Road mode OFF.");
        }

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

        private GridCoordinates _lastDragBlock;

        // ─────────────────────────────────────────────
        //  Input: Road Placement (click + drag)
        // ─────────────────────────────────────────────

        private void HandlePlacement()
        {
            if (Mouse.current == null) return;
            if (_isDraggingRemoval) return; // Don't build while deleting

            GridManager gm = GridManager.Instance;
            if (gm == null || mouseInteractor == null || !mouseInteractor.HasValidHit)
            {
                return;
            }

            GridCoordinates rawCell = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
            GridCoordinates currentBlock = ClampAndSnapToGrid(rawCell, gm);

            // ── On Mouse Down ───────────────────────────────
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    ExitRoadMode();
                    return;
                }

                _isDraggingPlacement = true;
                _dragStartBlock = currentBlock;
                _lastDragBlock = currentBlock;
                _currentPreviewPath.Clear();
                _currentPreviewPath.Add(currentBlock);
            }

            // ── While Mouse Held ─────────────────────────────
            if (_isDraggingPlacement)
            {
                if (currentBlock.X != _lastDragBlock.X || currentBlock.Y != _lastDragBlock.Y)
                {
                    List<GridCoordinates> segment = GetLShapePath(_lastDragBlock, currentBlock);
                    
                    for (int i = 1; i < segment.Count; i++)
                    {
                        var block = segment[i];
                        if (_currentPreviewPath.Count > 1 && block.Equals(_currentPreviewPath[_currentPreviewPath.Count - 2]))
                        {
                            // Backtracking exactly to the previous block
                            _currentPreviewPath.RemoveAt(_currentPreviewPath.Count - 1);
                        }
                        else if (!_currentPreviewPath.Contains(block))
                        {
                            _currentPreviewPath.Add(block);
                        }
                    }
                    _lastDragBlock = currentBlock;
                }
            }

            // ── On Mouse Up ──────────────────────────────────
            if (Mouse.current.leftButton.wasReleasedThisFrame && _isDraggingPlacement)
            {
                _isDraggingPlacement = false;
                
                List<GridCoordinates> actuallyPlaced = new List<GridCoordinates>();

                // 1. Commit all roads
                foreach (var block in _currentPreviewPath)
                {
                    if (TryPlaceRoadData(block, gm))
                    {
                        actuallyPlaced.Add(block);
                    }
                }

                // 2. Refresh visuals for all placed roads and their neighbours
                HashSet<GridCoordinates> toRefresh = new HashSet<GridCoordinates>(actuallyPlaced);
                int s = roadFootprintSize;
                foreach (var block in actuallyPlaced)
                {
                    foreach (var n in RoadTileSelector.GetBlockNeighbours(block, s))
                    {
                        toRefresh.Add(n);
                    }
                }

                foreach (var block in toRefresh)
                {
                    RefreshNeighbour(block, gm);
                }
                
                // Fire events
                foreach (var block in actuallyPlaced)
                {
                    OnRoadPlaced?.Invoke(block);
                }

                _currentPreviewPath.Clear();
            }
        }

        // ─────────────────────────────────────────────
        //  Input: Road Removal
        // ─────────────────────────────────────────────

        private void HandleRemoval()
        {
            if (Mouse.current == null) return;
            if (_isDraggingPlacement) return; // Don't delete while building

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                _isDraggingRemoval = true;
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                _isDraggingRemoval = false;
            }

            if (_isDraggingRemoval)
            {
                if (mouseInteractor == null || !mouseInteractor.HasValidHit) return;
                GridManager gm = GridManager.Instance;
                if (gm == null) return;

                GridCoordinates rawCell = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
                TryRemoveRoad(ClampAndSnapToGrid(rawCell, gm), gm);
            }
        }

        // ─────────────────────────────────────────────
        //  Road Placement Logic
        // ─────────────────────────────────────────────

        /// <summary>Places road data but doesn't instantly refresh neighbours (for batch placement)</summary>
        private bool TryPlaceRoadData(GridCoordinates blockOrigin, GridManager gm)
        {
            int s = roadFootprintSize;
            for (int dy = 0; dy < s; dy++)
            {
                for (int dx = 0; dx < s; dx++)
                {
                    GridCoordinates c = new GridCoordinates(blockOrigin.X + dx, blockOrigin.Y + dy);
                    GridCell cell = gm.GetCell(c);
                    if (cell == null || cell.HasRoad || cell.IsOccupied)
                        return false; 
                }
            }

            for (int dy = 0; dy < s; dy++)
            {
                for (int dx = 0; dx < s; dx++)
                {
                    GridCoordinates c = new GridCoordinates(blockOrigin.X + dx, blockOrigin.Y + dy);
                    gm.ClearNature(c);
                    gm.PlaceRoad(c);
                }
            }

            return true;
        }

        private void TryRemoveRoad(GridCoordinates blockOrigin, GridManager gm)
        {
            GridCell cell = gm.GetCell(blockOrigin);
            if (cell == null || !cell.HasRoad) return;

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

            Vector3    pos = gm.GetFootprintCenter(blockOrigin, s, s) + Vector3.up * roadHeightOffset;
            Quaternion rot = Quaternion.Euler(0f, rotY, 0f);

            GameObject go = Instantiate(prefab, pos, rot, roadContainer);
            go.name = $"Road_{blockOrigin.X}_{blockOrigin.Y}";

            _placedRoads[blockOrigin] = new PlacedRoad(blockOrigin, go, type);
        }

        private void RefreshNeighbour(GridCoordinates blockOrigin, GridManager gm)
        {
            GridCell cell = gm.GetCell(blockOrigin);
            if (cell == null || !cell.HasRoad) return;
            SpawnOrUpdateRoadTile(blockOrigin, gm);
        }

        private GameObject SelectPrefab(RoadTileType type) => type switch
        {
            RoadTileType.Corner    => cornerRoadPrefab    ?? straightRoadPrefab,
            RoadTileType.TJunction => tJunctionRoadPrefab ?? straightRoadPrefab,
            RoadTileType.Cross     => fourWayIntersectionPrefab ?? straightRoadPrefab,
            RoadTileType.DeadEnd   => deadEndRoadPrefab   ?? straightRoadPrefab,
            _                      => straightRoadPrefab,
        };

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        private GridCoordinates SnapToBlock(GridCoordinates raw)
        {
            int s = roadFootprintSize;
            return new GridCoordinates(
                (raw.X / s) * s,
                (raw.Y / s) * s);
        }

        private GridCoordinates ClampAndSnapToGrid(GridCoordinates raw, GridManager gm)
        {
            int s = roadFootprintSize;
            int maxX = gm.GridWidth - s;
            int maxY = gm.GridHeight - s;
            
            int clampedX = Mathf.Clamp(raw.X, 0, maxX);
            int clampedY = Mathf.Clamp(raw.Y, 0, maxY);
            
            return SnapToBlock(new GridCoordinates(clampedX, clampedY));
        }

        // ─────────────────────────────────────────────
        //  L-Shape Path
        // ─────────────────────────────────────────────

        private List<GridCoordinates> GetLShapePath(GridCoordinates start, GridCoordinates end)
        {
            List<GridCoordinates> path = new List<GridCoordinates>();
            int s = roadFootprintSize;
            
            int x0 = start.X, y0 = start.Y;
            int x1 = end.X,   y1 = end.Y;

            // Move along X first
            int stepX = x0 < x1 ? s : -s;
            for (int x = x0; x != x1; x += stepX)
            {
                path.Add(new GridCoordinates(x, y0));
            }
            
            // Add the corner
            path.Add(new GridCoordinates(x1, y0));

            // Move along Y
            int stepY = y0 < y1 ? s : -s;
            for (int y = y0 + stepY; (stepY > 0 ? y <= y1 : y >= y1); y += stepY)
            {
                path.Add(new GridCoordinates(x1, y));
            }

            return path;
        }

        // ─────────────────────────────────────────────
        //  Visuals & Previews
        // ─────────────────────────────────────────────

        private void UpdateHoverVisuals()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null || mouseInteractor == null || !mouseInteractor.HasValidHit)
            {
                HideAllGhosts();
                if (_deleteQuadObj != null) _deleteQuadObj.SetActive(false);
                return;
            }

            GridCoordinates rawCell = gm.WorldToGrid(mouseInteractor.LastWorldPosition);
            GridCoordinates currentBlock = ClampAndSnapToGrid(rawCell, gm);

            // Handle Deletion Quad
            if (!_isDraggingPlacement)
            {
                GridCell cell = gm.GetCell(currentBlock);
                if (cell != null && cell.HasRoad)
                {
                    // Show delete quad over this road
                    HideAllGhosts();
                    
                    if (_deleteQuadObj != null)
                    {
                        _deleteQuadObj.SetActive(true);
                        int s = roadFootprintSize;
                        Vector3 wp = gm.GetFootprintCenter(currentBlock, s, s);
                        _deleteQuadObj.transform.position = new Vector3(wp.x, wp.y + 0.04f, wp.z);
                    }
                    return;
                }
            }

            // Hide delete quad if we are placing or hovering over empty grass
            if (_deleteQuadObj != null) _deleteQuadObj.SetActive(false);

            // Update Ghosts
            if (_isDraggingPlacement)
            {
                UpdateGhostPreviewPath(_currentPreviewPath, gm);
            }
            else
            {
                // Just hovering, show single ghost
                UpdateGhostPreviewPath(new List<GridCoordinates> { currentBlock }, gm);
            }
        }

        private void UpdateGhostPreviewPath(List<GridCoordinates> path, GridManager gm)
        {
            int s = roadFootprintSize;
            
            for (int i = 0; i < _ghostPool.Count; i++)
            {
                if (i < path.Count)
                {
                    var block = path[i];
                    Vector3 wp = gm.GetFootprintCenter(block, s, s) + Vector3.up * roadHeightOffset;
                    
                    _ghostPool[i].transform.position = wp;
                    _ghostPool[i].SetActive(true);
                }
                else
                {
                    _ghostPool[i].SetActive(false);
                }
            }

            // Create new ghosts if we need more
            while (_ghostPool.Count < path.Count)
            {
                int i = _ghostPool.Count;
                var block = path[i];
                Vector3 wp = gm.GetFootprintCenter(block, s, s) + Vector3.up * roadHeightOffset;

                GameObject newGhost = CreateGhostInstance();
                newGhost.transform.position = wp;
                newGhost.SetActive(true);
                _ghostPool.Add(newGhost);
            }
        }

        private GameObject CreateGhostInstance()
        {
            if (straightRoadPrefab == null) return new GameObject("EmptyGhost");
            
            GameObject ghost = Instantiate(straightRoadPrefab, _ghostContainer);
            
            // Strip logic
            foreach (var col in ghost.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var mono in ghost.GetComponentsInChildren<MonoBehaviour>()) Destroy(mono);

            // Apply material
            if (hoverHighlightMaterial != null)
            {
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = hoverHighlightMaterial;
                    r.sharedMaterials = mats;
                }
            }

            return ghost;
        }

        private void HideAllGhosts()
        {
            foreach (var g in _ghostPool)
            {
                if (g != null) g.SetActive(false);
            }
        }

        private void BuildDeleteQuad()
        {
            _deleteQuadObj = new GameObject("RoadDeleteHighlight");
            _deleteQuadObj.transform.SetParent(transform, false);

            var mf = _deleteQuadObj.AddComponent<MeshFilter>();
            _deleteQuadRenderer = _deleteQuadObj.AddComponent<MeshRenderer>();
            _deleteQuadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _deleteQuadRenderer.receiveShadows = false;

            float cs = GridManager.Instance != null ? GridManager.Instance.CellSize : 4f;
            float size = cs * roadFootprintSize * 0.96f;
            float h = size * 0.5f;

            var mesh = new Mesh { name = "RoadDeleteQuad" };
            mesh.vertices = new[] {
                new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
                new Vector3( h, 0f,  h), new Vector3(-h, 0f, h)
            };
            mesh.triangles = new[] { 0, 3, 1, 1, 3, 2 };
            mesh.uv = new[] {
                new Vector2(0,0), new Vector2(1,0),
                new Vector2(1,1), new Vector2(0,1)
            };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            if (deleteHighlightMaterial != null)
            {
                _deleteQuadRenderer.sharedMaterial = deleteHighlightMaterial;
            }
            else if (hoverHighlightMaterial != null)
            {
                Material redMat = new Material(hoverHighlightMaterial);
                if (redMat.HasProperty("_BaseColor")) redMat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, 0.6f));
                else if (redMat.HasProperty("_Color")) redMat.SetColor("_Color", new Color(1f, 0.2f, 0.2f, 0.6f));
                _deleteQuadRenderer.sharedMaterial = redMat;
            }

            _deleteQuadObj.SetActive(false);
        }

        private void LogMissingRefs()
        {
            if (mouseInteractor == null)
                Debug.LogError("[RoadPlacer] Mouse Interactor is NOT assigned in Inspector.");
            if (straightRoadPrefab == null)
                Debug.LogError("[RoadPlacer] Straight Road Prefab is NOT assigned.");
        }

        private void OnDestroy()
        {
            if (_deleteQuadObj != null) Destroy(_deleteQuadObj);
        }
    }
}
