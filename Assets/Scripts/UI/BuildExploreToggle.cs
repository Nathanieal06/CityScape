using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Build Mode / Explore Mode toggle buttons in the bottom-left.
    /// Communicates with CameraManager to switch camera rigs.
    /// </summary>
    public class BuildExploreToggle : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button buildButton;
        [SerializeField] private Button exploreButton;

        [Header("Active Button Colors")]
        [SerializeField] private Image buildButtonBg;
        [SerializeField] private Image exploreButtonBg;
        [SerializeField] private Color activeColor   = new Color(0.85f, 0.65f, 0.1f);
        [SerializeField] private Color inactiveColor = new Color(0.15f, 0.15f, 0.2f);

        private void Start()
        {
            buildButton?.onClick.AddListener(  () => SetMode(CameraMode.Build));
            exploreButton?.onClick.AddListener(() => SetMode(CameraMode.Explore));

            if (CameraManager.Instance != null)
                CameraManager.Instance.OnCameraModeChanged += RefreshHighlight;

            RefreshHighlight(CameraMode.Build);
        }

        private void Update()
        {
            // Allow pressing Tab to quick-toggle between modes!
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CameraManager.Instance?.ToggleMode();
            }
        }

        private void OnDestroy()
        {
            if (CameraManager.Instance != null)
                CameraManager.Instance.OnCameraModeChanged -= RefreshHighlight;
        }

        private void SetMode(CameraMode mode)
        {
            CameraManager.Instance?.SwitchToMode(mode);
        }

        private void RefreshHighlight(CameraMode mode)
        {
            if (buildButtonBg   != null) buildButtonBg.color   = mode == CameraMode.Build   ? activeColor : inactiveColor;
            if (exploreButtonBg != null) exploreButtonBg.color = mode == CameraMode.Explore ? activeColor : inactiveColor;
        }
    }
}
