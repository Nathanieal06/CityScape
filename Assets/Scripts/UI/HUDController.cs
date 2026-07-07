using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Controls the top HUD bar showing all city resources in real time.
    ///
    /// Subscribes to EconomyManager.OnResourcesChanged and
    /// GameManager.OnDayChanged — never polls in Update.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector — Resource Labels
        // ─────────────────────────────────────────────

        [Header("Money")]
        [SerializeField] private TextMeshProUGUI moneyLabel;
        [SerializeField] private TextMeshProUGUI moneyRateLabel;

        [Header("Population")]
        [SerializeField] private TextMeshProUGUI populationLabel;

        [Header("Happiness")]
        [SerializeField] private TextMeshProUGUI happinessLabel;

        [Header("Electricity")]
        [SerializeField] private TextMeshProUGUI electricityLabel;

        [Header("Water")]
        [SerializeField] private TextMeshProUGUI waterLabel;

        [Header("Waste")]
        [SerializeField] private TextMeshProUGUI wasteLabel;

        [Header("Time")]
        [SerializeField] private TextMeshProUGUI dayLabel;
        [SerializeField] private TextMeshProUGUI timeLabel;

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;

        [SerializeField] private Button settingsButton;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Subscribe to events (no Update polling)
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnResourcesChanged += RefreshResources;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged    += RefreshDay;
                GameManager.Instance.OnPauseToggled  += RefreshPauseButton;
                GameManager.Instance.OnSpeedChanged  += RefreshSpeedButtons;
            }

            // Wire buttons
            pauseButton?.onClick.AddListener(() => GameManager.Instance?.TogglePause());

            settingsButton?.onClick.AddListener(() => UIManager.Instance?.ShowSettings());

            // Initial refresh
            RefreshResources();
            RefreshDay(GameManager.Instance?.CurrentDay ?? 1);
        }

        private void OnDestroy()
        {
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnResourcesChanged -= RefreshResources;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged   -= RefreshDay;
                GameManager.Instance.OnPauseToggled -= RefreshPauseButton;
                GameManager.Instance.OnSpeedChanged -= RefreshSpeedButtons;
            }
        }

        // Updates time display every frame (lightweight string format only)
        private void Update()
        {
            if (timeLabel != null && GameManager.Instance != null)
                timeLabel.text = GameManager.Instance.TimeString;
        }

        // ─────────────────────────────────────────────
        //  Refresh Methods
        // ─────────────────────────────────────────────

        private void RefreshResources()
        {
            var eco = EconomyManager.Instance;
            if (eco == null) return;

            if (moneyLabel      != null) moneyLabel.text       = $"${eco.Money:N0}";
            if (populationLabel != null) populationLabel.text  = eco.Population.ToString();
            if (happinessLabel  != null) happinessLabel.text   = $"{eco.Happiness:F0}%";

            if (electricityLabel != null)
                electricityLabel.text = $"{eco.ElectricityUsed:F0} / {eco.ElectricityCapacity:F0}";

            if (waterLabel != null)
                waterLabel.text = $"{eco.WaterUsed:F0} / {eco.WaterCapacity:F0}";

            if (wasteLabel != null)
                wasteLabel.text = $"{eco.WastePercentage:F0}%";
        }

        private void RefreshDay(int day)
        {
            if (dayLabel != null) dayLabel.text = $"Day {day}";
        }

        private void RefreshPauseButton(bool paused)
        {
            // Optionally swap pause icon here
        }

        private void RefreshSpeedButtons(GameSpeed speed)
        {
            // Optionally highlight the active speed button
        }
    }
}
