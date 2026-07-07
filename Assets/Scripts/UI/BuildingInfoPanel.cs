using CityScape.GridSystem.Data;
using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Left-side panel that displays detailed information about the
    /// currently selected building and a "Place Building" button.
    ///
    /// Show/Hide is controlled by UIManager.
    /// </summary>
    public class BuildingInfoPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI buildingNameLabel;
        [SerializeField] private Image           buildingIcon;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private TextMeshProUGUI maintenanceLabel;
        [SerializeField] private TextMeshProUGUI populationLabel;
        [SerializeField] private TextMeshProUGUI categoryLabel;
        [SerializeField] private TextMeshProUGUI sizeLabel;
        [SerializeField] private TextMeshProUGUI powerLabel;
        [SerializeField] private TextMeshProUGUI waterLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;

        [Header("Place Button")]
        [SerializeField] private Button          placeButton;
        [SerializeField] private TextMeshProUGUI placeButtonLabel;

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private BuildingData _currentData;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            placeButton?.onClick.AddListener(OnPlaceButtonClicked);
            Hide();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Populates all fields and shows the panel.</summary>
        public void ShowBuilding(BuildingData data)
        {
            if (data == null) { Hide(); return; }
            _currentData = data;

            if (panelRoot != null) panelRoot.SetActive(true);

            // Header
            if (buildingNameLabel != null) buildingNameLabel.text = data.buildingName;
            if (buildingIcon      != null)
            {
                buildingIcon.sprite  = data.icon;
                buildingIcon.enabled = data.icon != null;
            }

            // Stats
            SetLabel(costLabel,        $"<sprite=0> {data.placementCost}");
            SetLabel(maintenanceLabel, $"<sprite=0> {data.maintenanceCostPerMin} / min");
            SetLabel(populationLabel,  $"+{data.populationCapacity} Population");
            SetLabel(categoryLabel,    data.category.ToString());
            SetLabel(sizeLabel,        data.SizeLabel);
            SetLabel(descriptionLabel, data.description);

            // Power
            if (powerLabel != null)
            {
                int net = data.NetPower;
                powerLabel.text  = net >= 0 ? $"+{net}" : $"{net}";
                powerLabel.color = net >= 0 ? Color.green : Color.red;
            }

            // Water
            if (waterLabel != null)
            {
                int net = data.NetWater;
                waterLabel.text  = net >= 0 ? $"+{net}" : $"{net}";
                waterLabel.color = net >= 0 ? Color.cyan : Color.red;
            }

            // Place button affordability
            RefreshPlaceButton();
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            _currentData = null;
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        private void RefreshPlaceButton()
        {
            if (placeButton == null || _currentData == null) return;
            bool canAfford = EconomyManager.Instance != null &&
                             EconomyManager.Instance.CanAfford(_currentData.placementCost);

            placeButton.interactable = canAfford;
            if (placeButtonLabel != null)
                placeButtonLabel.text = canAfford ? "PLACE BUILDING" : "NOT ENOUGH MONEY";
        }

        private void OnPlaceButtonClicked()
        {
            if (_currentData == null) return;
            SelectionManager.Instance?.SelectBuilding(_currentData);
        }

        private static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
