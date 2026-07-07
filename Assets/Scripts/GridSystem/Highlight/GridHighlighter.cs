using CityScape.GridSystem.Core;
using CityScape.GridSystem.Utility;
using UnityEngine;

namespace CityScape.GridSystem.Highlight
{
    /// <summary>
    /// Renders a flat quad highlight under the building ghost.
    ///
    /// The highlight expands to match the building's rotated footprint and
    /// changes between a valid (green) and invalid (red) material.
    ///
    /// The mesh is generated procedurally so no external mesh asset is needed.
    /// The quad uses a custom URP material with Transparent Blend Mode.
    ///
    /// Usage:
    ///   1. Add this script to an empty GameObject in the scene.
    ///   2. Assign valid / invalid materials in the Inspector.
    ///   3. Call Show() or Hide() from BuildingPlacer.
    /// </summary>
    public class GridHighlighter : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Materials")]
        [Tooltip("Semi-transparent green — shown when placement is valid.")]
        [SerializeField] private Material validMaterial;

        [Tooltip("Semi-transparent red — shown when placement is invalid.")]
        [SerializeField] private Material invalidMaterial;

        [Header("Settings")]
        [Tooltip("How far above ground the highlight quad sits to avoid Z-fighting.")]
        [SerializeField, Min(0f)] private float heightOffset = 0.02f;

        [Tooltip("Scale factor applied to each cell so the highlight doesn't touch the grid lines.")]
        [SerializeField, Range(0.8f, 1f)] private float cellPadding = 0.96f;

        // ─────────────────────────────────────────────
        //  Components (auto-created)
        // ─────────────────────────────────────────────

        private MeshFilter   _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh         _mesh;

        // ─────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────

        private int _lastWidth  = -1;
        private int _lastHeight = -1;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // Ensure components exist on this GameObject
            _meshFilter   = gameObject.GetOrAddComponent<MeshFilter>();
            _meshRenderer = gameObject.GetOrAddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "GridHighlight" };
            _meshFilter.sharedMesh = _mesh;

            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows    = false;

            Hide();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Moves the highlight to the world-space centre of the given footprint
        /// and applies the correct colour for the validity state.
        /// </summary>
        /// <param name="worldCenter">Centre of the footprint in world space.</param>
        /// <param name="widthCells">Footprint width in cells (rotation-adjusted).</param>
        /// <param name="heightCells">Footprint height in cells (rotation-adjusted).</param>
        /// <param name="isValid">True → green material, false → red material.</param>
        /// <param name="cellSize">Cell size in world units (from GridManager).</param>
        public void Show(
            Vector3 worldCenter,
            int     widthCells,
            int     heightCells,
            bool    isValid,
            float   cellSize)
        {
            gameObject.SetActive(true);

            // Reposition
            transform.position = new Vector3(
                worldCenter.x,
                worldCenter.y + heightOffset,
                worldCenter.z);

            // Rebuild mesh only if footprint size changed
            if (widthCells != _lastWidth || heightCells != _lastHeight)
            {
                BuildMesh(widthCells * cellSize * cellPadding,
                          heightCells * cellSize * cellPadding);
                _lastWidth  = widthCells;
                _lastHeight = heightCells;
            }

            // Apply material
            _meshRenderer.sharedMaterial = isValid ? validMaterial : invalidMaterial;
        }

        /// <summary>Hides the highlight quad.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  Mesh Generation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Builds a flat XZ quad (horizontal plane) centred at the local origin.
        /// </summary>
        private void BuildMesh(float worldWidth, float worldDepth)
        {
            float hw = worldWidth  * 0.5f;
            float hd = worldDepth  * 0.5f;

            Vector3[] verts = new Vector3[4]
            {
                new Vector3(-hw, 0f, -hd), // BL
                new Vector3( hw, 0f, -hd), // BR
                new Vector3( hw, 0f,  hd), // TR
                new Vector3(-hw, 0f,  hd)  // TL
            };

            int[] tris = new int[6] { 0, 3, 1, 1, 3, 2 };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            _mesh.Clear();
            _mesh.vertices  = verts;
            _mesh.triangles = tris;
            _mesh.uv        = uvs;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }

    // ─────────────────────────────────────────────
    //  Extension Method (local utility)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Extension helper so GridHighlighter doesn't need a separate utility class
    /// just for GetOrAddComponent.
    /// </summary>
    internal static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
    }
}
