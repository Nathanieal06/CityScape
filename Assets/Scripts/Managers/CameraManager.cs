using System;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>Camera operating mode.</summary>
    public enum CameraMode
    {
        Build   = 0,
        Explore = 1
    }

    /// <summary>
    /// Manages switching between the isometric Build camera and the
    /// first-person Explore camera. Exposes position/rotation for save/load.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static CameraManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Camera References")]
        [SerializeField] private Camera buildCamera;
        [SerializeField] private Camera exploreCamera;

        [Header("Starting Mode")]
        [SerializeField] private CameraMode startingMode = CameraMode.Build;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        public CameraMode CurrentMode { get; private set; }

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        public event Action<CameraMode> OnCameraModeChanged;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            SwitchToMode(startingMode, force: true);
        }

        private void Update()
        {
            // If the game is paused, ALWAYS unlock the cursor so the Pause Menu can be clicked!
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            // StarterAssets (FirstPersonController) automatically tries to lock the cursor.
            // In Build Mode, we must override this so the mouse is always free to click UI/Grid.
            if (CurrentMode == CameraMode.Build)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                // In Explore mode, let the game/player controller manage the cursor.
                // Usually it locks it, but we can enforce it if needed:
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Switches to the given camera mode.</summary>
        public void SwitchToMode(CameraMode mode, bool force = false)
        {
            if (CurrentMode == mode && !force) return;
            CurrentMode = mode;

            if (buildCamera   != null) buildCamera.gameObject.SetActive(mode == CameraMode.Build);
            if (exploreCamera != null) exploreCamera.gameObject.SetActive(mode == CameraMode.Explore);

            OnCameraModeChanged?.Invoke(mode);
            Debug.Log($"[CameraManager] Switched to {mode} mode.");
        }

        /// <summary>Toggles between Build and Explore.</summary>
        public void ToggleMode()
            => SwitchToMode(CurrentMode == CameraMode.Build ? CameraMode.Explore : CameraMode.Build);

        // ─────────────────────────────────────────────
        //  Save / Load Helpers
        // ─────────────────────────────────────────────

        public Vector3    GetCameraPosition() => ActiveCamera?.transform.position ?? Vector3.zero;
        public Quaternion GetCameraRotation() => ActiveCamera?.transform.rotation ?? Quaternion.identity;

        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            Vector3 pos = new Vector3(data.cameraPosX, data.cameraPosY, data.cameraPosZ);
            Quaternion rot = new Quaternion(data.cameraRotX, data.cameraRotY, data.cameraRotZ, data.cameraRotW);
            CameraMode mode = (CameraMode)data.cameraModeIndex;
            RestoreCamera(pos, rot, mode);
        }

        public void RestoreCamera(Vector3 pos, Quaternion rot, CameraMode mode)
        {
            SwitchToMode(mode, force: true);
            if (ActiveCamera != null)
            {
                ActiveCamera.transform.position = pos;
                ActiveCamera.transform.rotation = rot;
            }
        }

        private Camera ActiveCamera
            => CurrentMode == CameraMode.Build ? buildCamera : exploreCamera;
    }
}
