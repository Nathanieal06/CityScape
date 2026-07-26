using CityScape.GridSystem.Core;
using UnityEngine;

namespace CityScape.GridSystem.Environment
{
    /// <summary>
    /// Generates a grid of ground tiles matching the GridManager's
    /// Width x Height layout and cell size.
    ///
    /// Two spawn modes:
    ///  - Prefab    : instantiates a user-supplied Block Prefab for every cell.
    ///  - Primitive : falls back to Unity built-in Cube when no prefab is assigned.
    ///
    /// Usage:
    ///  1. Add this component to any GameObject in the scene.
    ///  2. Assign a Block Prefab (any cube-like GameObject).
    ///  3. Optionally assign TileColorA / TileColorB for the checkerboard tint.
    ///  4. Press Play - or click "Generate Ground" in the Inspector (Edit Mode).
    ///
    /// The generator reads GridWidth, GridHeight, and CellSize from GridManager
    /// so it always stays in sync with the grid dimensions and origin.
    /// </summary>
    [ExecuteAlways]
    public class GroundGridGenerator : MonoBehaviour
    {
        // -------------------------------------------------------
        //  Inspector Fields
        // -------------------------------------------------------

        [Header("Block Prefab")]
        [Tooltip("Prefab instantiated for every grid cell.\n" +
                 "Leave empty to fall back to Unity built-in Cube primitive.")]
        [SerializeField] private GameObject blockPrefab;

        [Tooltip("When enabled the prefab Renderer is tinted with the checkerboard\n" +
                 "colours (TileColorA / TileColorB).")]
        [SerializeField] private bool applyCheckerboardTint = true;

        [Header("Checkerboard Colours")]
        [Tooltip("Colour for even tiles  (x + y) % 2 == 0.")]
        [SerializeField] private Color tileColorA = new Color(0.35f, 0.72f, 0.31f);

        [Tooltip("Colour for odd tiles  (x + y) % 2 == 1.")]
        [SerializeField] private Color tileColorB = new Color(0.25f, 0.55f, 0.22f);

        [Header("Checkerboard Materials (Primitive Fallback Only)")]
        [Tooltip("Material for even tiles when NO block prefab is assigned.")]
        [SerializeField] private Material tileMatA;

        [Tooltip("Material for odd tiles when NO block prefab is assigned.")]
        [SerializeField] private Material tileMatB;

        [Header("Tile Shape")]
        [Tooltip("Thickness (Y scale) of each ground cube in world units.\n" +
                 "Applied to primitive tiles; for prefabs the prefab Y scale is used.")]
        [SerializeField, Min(0.01f)] private float tileHeight = 0.5f;

        [Tooltip("Tiny gap between tiles (0 = touching).")]
        [SerializeField, Range(0f, 0.1f)] private float tileGap = 0.02f;

        [Tooltip("XZ scale multiplier on top of cellSize.\n" +
                 "Useful when your prefab base size is not 1 unit.")]
        [SerializeField, Min(0.01f)] private float prefabScaleMultiplier = 1f;
        [Tooltip("Vertical offset applied to each prefab tile (0 = sit at grid origin Y).")]
        [SerializeField] private float blockYOffset = 0f;

        [Header("Generation")]
        [Tooltip("Parent all tile GameObjects under a dedicated child container.")]
        [SerializeField] private bool groupUnderChild = true;

        [Tooltip("Name of the child container object.")]
        [SerializeField] private string containerName = "GroundTiles";

        [Tooltip("Regenerate automatically when entering Play Mode.")]
        [SerializeField] private bool regenerateOnPlay = true;

        // -------------------------------------------------------
        //  Private State
        // -------------------------------------------------------

        private GameObject _container;

        // -------------------------------------------------------
        //  Unity Lifecycle
        // -------------------------------------------------------

        private void Start()
        {
            if (regenerateOnPlay && Application.isPlaying)
                GenerateGround();
        }

        // -------------------------------------------------------
        //  Public API
        // -------------------------------------------------------

        /// <summary>
        /// Destroys any existing tiles and spawns a fresh ground grid.
        /// Safe to call in both Edit Mode and Play Mode.
        /// </summary>
        public void GenerateGround()
        {
            GridManager gm = GridManager.Instance != null
                ? GridManager.Instance
                : FindFirstObjectByType<GridManager>();

            if (gm == null)
            {
                Debug.LogError("[GroundGridGenerator] No GridManager found. " +
                               "Add a GridManager to the scene first.");
                return;
            }

            ClearGround();

            Transform container = GetOrCreateContainer();

            int     width    = gm.GridWidth;
            int     height   = gm.GridHeight;
            float   cellSize = gm.CellSize;
            Vector3 origin   = gm.GridOrigin;

            bool  usePrefab  = blockPrefab != null;
            float tileSize   = cellSize - tileGap;
            float halfHeight = tileHeight * 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool   isEven   = (x + y) % 2 == 0;
                    string tileName = $"GroundTile_{x}_{y}";

                    if (usePrefab)
                        SpawnPrefabTile(x, y, tileName, container, origin, cellSize, isEven);
                    else
                        SpawnPrimitiveTile(x, y, tileName, container, origin, cellSize, tileSize, halfHeight, isEven);
                }
            }

            Debug.Log($"[GroundGridGenerator] Generated {width}x{height} ground tiles " +
                      $"(mode={(usePrefab ? "Prefab" : "Primitive")}, cellSize={cellSize}).");
        }

        /// <summary>Destroys all existing ground tiles without regenerating.</summary>
        public void ClearGround()
        {
            Transform existing = transform.Find(containerName);
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            _container = null;
        }

        // -------------------------------------------------------
        //  Tile Spawners
        // -------------------------------------------------------

        private void SpawnPrefabTile(int x, int y, string tileName,
                                     Transform container, Vector3 origin,
                                     float cellSize, bool isEven)
        {
            // Centre of cell at ground level
            Vector3 spawnPos = new Vector3(
                origin.x + x * cellSize + cellSize * 0.5f,
                origin.y + blockYOffset,
                origin.z + y * cellSize + cellSize * 0.5f
            );

            GameObject tile;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                tile = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(blockPrefab, container);
                tile.transform.position = spawnPos;
            }
            else
            {
                tile = Instantiate(blockPrefab, spawnPos, Quaternion.identity, container);
            }
#else
            tile = Instantiate(blockPrefab, spawnPos, Quaternion.identity, container);
#endif

            tile.name = tileName;

            // Scale XZ to fill one grid cell; keep prefab Y scale
            float xzScale = cellSize * prefabScaleMultiplier;
            Vector3 origScale = tile.transform.localScale;
            tile.transform.localScale = new Vector3(xzScale, origScale.y, xzScale);

            // Checkerboard tint via material instance
            if (applyCheckerboardTint)
            {
                Renderer rend = tile.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    Material mat = new Material(rend.sharedMaterial);
                    mat.color = isEven ? tileColorA : tileColorB;
                    rend.sharedMaterial = mat;
                }
            }
        }

        private void SpawnPrimitiveTile(int x, int y, string tileName,
                                        Transform container, Vector3 origin,
                                        float cellSize, float tileSize,
                                        float halfHeight, bool isEven)
        {
            Vector3 worldPos = new Vector3(
                origin.x + x * cellSize + cellSize * 0.5f,
                origin.y - halfHeight,
                origin.z + y * cellSize + cellSize * 0.5f
            );

            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = tileName;
            tile.transform.SetParent(container, worldPositionStays: false);
            tile.transform.localPosition = worldPos;
            tile.transform.localScale    = new Vector3(tileSize, tileHeight, tileSize);

            DestroyImmediate(tile.GetComponent<Collider>());

            Renderer rend = tile.GetComponent<Renderer>();
            if (isEven)
            {
                if (tileMatA != null) rend.sharedMaterial = tileMatA;
                else ApplyColorTint(rend, tileColorA);
            }
            else
            {
                if (tileMatB != null) rend.sharedMaterial = tileMatB;
                else ApplyColorTint(rend, tileColorB);
            }
        }

        // -------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------

        private Transform GetOrCreateContainer()
        {
            if (groupUnderChild)
            {
                _container = new GameObject(containerName);
                _container.transform.SetParent(transform, worldPositionStays: false);
                _container.transform.localPosition = Vector3.zero;
                _container.transform.localRotation = Quaternion.identity;
                _container.transform.localScale    = Vector3.one;
                return _container.transform;
            }
            return transform;
        }

        private static void ApplyColorTint(Renderer rend, Color color)
        {
            Material mat = new Material(rend.sharedMaterial) { color = color };
            rend.sharedMaterial = mat;
        }
    }
}


