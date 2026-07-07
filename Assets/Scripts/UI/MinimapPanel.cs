using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Placeholder minimap panel in the top-right corner.
    /// Currently renders a static RenderTexture. Future: sync with a
    /// dedicated minimap orthographic camera.
    /// </summary>
    public class MinimapPanel : MonoBehaviour
    {
        [Header("Minimap Elements")]
        [SerializeField] private RawImage minimapDisplay;
        [SerializeField] private RenderTexture minimapRenderTexture;

        [Header("Player Indicator")]
        [Tooltip("Small marker showing player position on minimap.")]
        [SerializeField] private RectTransform playerIndicator;

        private Camera _minimapCamera;

        private void Start()
        {
            // Try to find a camera tagged "MinimapCamera"
            _minimapCamera = GameObject.FindWithTag("MinimapCamera")?.GetComponent<Camera>();

            if (_minimapCamera != null && minimapRenderTexture != null)
            {
                _minimapCamera.targetTexture = minimapRenderTexture;
                if (minimapDisplay != null)
                    minimapDisplay.texture = minimapRenderTexture;
            }
        }
    }
}
