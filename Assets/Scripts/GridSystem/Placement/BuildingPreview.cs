using CityScape.GridSystem.Core;
using CityScape.GridSystem.Data;
using CityScape.GridSystem.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace CityScape.GridSystem.Placement
{
    /// <summary>
    /// Manages the transparent "ghost" building preview that follows the mouse.
    ///
    /// How it works:
    ///   1. Call SetBuilding(data) when the player selects a new building type.
    ///      This clones the prefab under a hidden parent, strips non-renderer
    ///      components, and applies a transparent preview material.
    ///   2. Call UpdatePreview(position, rotation, isValid) every Update to move
    ///      and re-colour the ghost.
    ///   3. Call ClearPreview() when the player deselects or cancels.
    ///
    /// Performance notes:
    ///   - Renderer[] is cached on SetBuilding — no GC allocation per frame.
    ///   - Material arrays are pre-built and swapped, not rebuilt.
    ///   - Colliders on the ghost are disabled so they don't interfere with raycasts.
    ///
    /// URP compatibility:
    ///   The script creates runtime transparent copies of the provided materials.
    ///   Assign URP/Lit-based transparent materials in the Inspector.
    /// </summary>
    public class BuildingPreview : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Preview Materials (URP / Transparent)")]
        [Tooltip("Transparent green material — applied when placement is valid.")]
        [SerializeField] private Material validPreviewMaterial;

        [Tooltip("Transparent red material — applied when placement is invalid.")]
        [SerializeField] private Material invalidPreviewMaterial;

        [Header("Settings")]
        [Tooltip("Y offset so the ghost sits exactly on ground level.")]
        [SerializeField] private float groundYOffset = 0f;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private GameObject    _ghostRoot;       // Parent of the cloned prefab
        private Renderer[]    _ghostRenderers;  // Cached to avoid per-frame GetComponentsInChildren
        private BuildingData  _currentData;
        private bool          _isValid;

        // Per-renderer material arrays — built once in SetBuilding, swapped in Apply
        private Material[][]  _validMatArrays;
        private Material[][]  _invalidMatArrays;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void OnDestroy()
        {
            // Avoid leaking runtime material instances
            ClearPreview();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Instantiates a ghost copy of the given building and prepares it
        /// for preview rendering.  Replaces any previously active ghost.
        /// </summary>
        public void SetBuilding(BuildingData data)
        {
            ClearPreview(); // Destroy old ghost if any

            if (data == null || data.prefab == null)
            {
                Debug.LogWarning("[BuildingPreview] SetBuilding called with null data or prefab.");
                return;
            }

            _currentData = data;

            // Clone the real prefab into a ghost root
            _ghostRoot = new GameObject($"Ghost_{data.buildingName}");
            _ghostRoot.transform.SetParent(transform, false);

            var visual = Instantiate(data.prefab, _ghostRoot.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Disable all colliders so the ghost doesn't block raycasts
            foreach (var col in _ghostRoot.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // Disable all scripts on the ghost to prevent unintended behaviour
            foreach (var mono in _ghostRoot.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mono != this) mono.enabled = false;
            }

            // Cache renderers and build material arrays
            _ghostRenderers   = _ghostRoot.GetComponentsInChildren<Renderer>();
            _validMatArrays   = new Material[_ghostRenderers.Length][];
            _invalidMatArrays = new Material[_ghostRenderers.Length][];

            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                int matCount = _ghostRenderers[i].sharedMaterials.Length;

                // Fill every material slot with the preview material
                _validMatArrays[i]   = new Material[matCount];
                _invalidMatArrays[i] = new Material[matCount];

                for (int m = 0; m < matCount; m++)
                {
                    _validMatArrays[i][m]   = validPreviewMaterial;
                    _invalidMatArrays[i][m] = invalidPreviewMaterial;
                }
            }

            _ghostRoot.SetActive(true);

            // Apply initial material
            ApplyPreviewMaterials(true);
        }

        /// <summary>
        /// Moves and rotates the ghost to the given world position,
        /// and switches to valid/invalid materials.
        /// </summary>
        /// <param name="worldPosition">Snapped world centre of the footprint.</param>
        /// <param name="rotationStep">0=0°, 1=90°, 2=180°, 3=270°.</param>
        /// <param name="isValid">True → green ghost, false → red ghost.</param>
        public void UpdatePreview(Vector3 worldPosition, int rotationStep, bool isValid)
        {
            if (_ghostRoot == null) return;

            _ghostRoot.transform.position = worldPosition + Vector3.up * groundYOffset;
            _ghostRoot.transform.rotation = Quaternion.Euler(
                0f,
                GridUtility.RotationStepToAngle(rotationStep),
                0f);

            if (isValid != _isValid)
            {
                _isValid = isValid;
                ApplyPreviewMaterials(isValid);
            }
        }

        /// <summary>Destroys the ghost and clears internal state.</summary>
        public void ClearPreview()
        {
            if (_ghostRoot != null)
            {
                Destroy(_ghostRoot);
                _ghostRoot      = null;
                _ghostRenderers = null;
                _currentData    = null;
            }
        }

        /// <summary>Returns true if a ghost is currently active.</summary>
        public bool HasPreview => _ghostRoot != null && _ghostRoot.activeSelf;

        // ─────────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────────

        private void ApplyPreviewMaterials(bool valid)
        {
            if (_ghostRenderers == null) return;

            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                if (_ghostRenderers[i] == null) continue;
                _ghostRenderers[i].sharedMaterials = valid
                    ? _validMatArrays[i]
                    : _invalidMatArrays[i];
            }
        }
    }
}
