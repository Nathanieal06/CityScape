using CityScape.Managers;
using TMPro;
using UnityEngine;

namespace CityScape.UI
{
    /// <summary>
    /// Small floating overlay that appears near the cursor during placement mode,
    /// showing "Left Click to Place | R – Rotate | Right Click to Cancel".
    /// Controlled by UIManager via Show() / Hide().
    /// </summary>
    public class PlacementOverlay : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject overlayRoot;

        [Header("Hint Text")]
        [SerializeField] private TextMeshProUGUI hintText;

        private void Awake()
        {
            if (hintText != null)
                hintText.text = "Left Click to Place\nR – Rotate\nRight Click to Cancel";
            Hide();
        }

        /// <summary>Shows the placement hint overlay.</summary>
        public void Show()
        {
            if (overlayRoot != null) overlayRoot.SetActive(true);
        }

        /// <summary>Hides the placement hint overlay.</summary>
        public void Hide()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }
    }
}
