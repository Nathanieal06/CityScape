using System;
using CityScape.GridSystem.Data;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>
    /// Tracks all city economy resources in real time.
    /// Subscribes to BuildingPlacer events to automatically adjust resources
    /// when buildings are placed or removed.
    ///
    /// Fires OnResourcesChanged so the HUD can refresh without polling.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static EconomyManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector — Starting Values
        // ─────────────────────────────────────────────

        [Header("Starting Resources")]
        [SerializeField] private int   startMoney               = 12500;
        [SerializeField] private int   startPopulation          = 0;
        [SerializeField] private float startHappiness           = 85f;
        [SerializeField] private float startElectricityCapacity = 250f;
        [SerializeField] private float startWaterCapacity       = 100f;

        [Header("Simulation")]
        [Tooltip("How often (seconds) the economy ticks (income, maintenance deducted).")]
        [SerializeField] private float tickIntervalSeconds = 60f;

        // ─────────────────────────────────────────────
        //  Read-Only Resource Properties
        // ─────────────────────────────────────────────

        public int   Money               { get; private set; }
        public int   Population          { get; private set; }
        public float Happiness           { get; private set; }
        public float ElectricityUsed     { get; private set; }
        public float ElectricityCapacity { get; private set; }
        public float WaterUsed           { get; private set; }
        public float WaterCapacity       { get; private set; }
        public float WastePercentage     { get; private set; }

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired whenever any resource value changes. HUD subscribes to this.</summary>
        public event Action OnResourcesChanged;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private float _tickTimer;
        private int   _totalMaintenanceCostPerTick;
        private int   _totalIncomePerTick;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetToDefaults();
        }

        private void Start()
        {
            // Subscribe to BuildingPlacer events
            var placer = GridSystem.Placement.BuildingPlacer.Instance
                         ?? FindFirstObjectByType<GridSystem.Placement.BuildingPlacer>();
            if (placer != null)
            {
                placer.OnBuildingPlaced  += HandleBuildingPlaced;
                placer.OnBuildingRemoved += HandleBuildingRemoved;
            }
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickIntervalSeconds)
            {
                _tickTimer = 0f;
                RunEconomyTick();
            }
        }

        [Header("Cheats")]
        [Tooltip("If true, placing buildings costs nothing and ignores budget checks.")]
        [SerializeField] private bool creativeMode = false;
        
        public bool CreativeMode 
        { 
            get => creativeMode;
            set { creativeMode = value; OnResourcesChanged?.Invoke(); } 
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Returns true if the player can afford the given cost.</summary>
        public bool CanAfford(int cost) => creativeMode || Money >= cost;

        /// <summary>Deducts money. Returns false if insufficient funds.</summary>
        public bool Deduct(int amount)
        {
            if (creativeMode) return true;
            if (Money < amount)
            {
                NotificationManager.Instance?.ShowNotification(
                    "Not Enough Money!", NotificationType.Warning);
                return false;
            }
            Money -= amount;
            OnResourcesChanged?.Invoke();
            return true;
        }

        /// <summary>Adds income to the player's balance.</summary>
        public void AddIncome(int amount)
        {
            Money += amount;
            OnResourcesChanged?.Invoke();
        }

        /// <summary>Restores all resources to their starting values (New Game).</summary>
        public void ResetToDefaults()
        {
            Money               = startMoney;
            Population          = startPopulation;
            Happiness           = startHappiness;
            ElectricityUsed     = 0f;
            ElectricityCapacity = startElectricityCapacity;
            WaterUsed           = 0f;
            WaterCapacity       = startWaterCapacity;
            WastePercentage     = 0f;
            _totalMaintenanceCostPerTick = 0;
            _totalIncomePerTick          = 0;
            OnResourcesChanged?.Invoke();
        }

        /// <summary>Applies a saved game snapshot to the economy.</summary>
        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            Money               = data.money;
            Population          = data.population;
            Happiness           = data.happiness;
            ElectricityUsed     = data.electricityUsed;
            ElectricityCapacity = data.electricityCapacity;
            WaterUsed           = data.waterUsed;
            WaterCapacity       = data.waterCapacity;
            WastePercentage     = data.wastePercentage;
            OnResourcesChanged?.Invoke();
        }

        // ─────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────

        private void HandleBuildingPlaced(BuildingData data, GridSystem.Core.GridCoordinates _)
        {
            // Deduct placement cost (BuildManager already validated CanAfford)
            if (!creativeMode) Money -= data.placementCost;

            // Update running economy totals
            _totalMaintenanceCostPerTick += data.maintenanceCostPerMin;
            _totalIncomePerTick          += data.incomePerTick;
            Population                   += data.populationCapacity;
            Happiness                    += data.happinessBonus;

            // Utility
            ElectricityUsed     += data.powerConsumption;
            ElectricityCapacity += data.powerProduction;
            WaterUsed           += data.waterConsumption;
            WaterCapacity       += data.waterProduction;
            WastePercentage      = Mathf.Clamp(WastePercentage + data.wasteProduction, 0f, 100f);

            // Clamp happiness
            Happiness = Mathf.Clamp(Happiness, 0f, 100f);
            OnResourcesChanged?.Invoke();

            // Notifications
            if (data.populationCapacity > 0)
                NotificationManager.Instance?.ShowNotification(
                    $"+{data.populationCapacity} Population", NotificationType.Success);
        }

        private void HandleBuildingRemoved(BuildingData data, GridSystem.Core.GridCoordinates _)
        {
            // Refund 50%
            if (!creativeMode) Money += data.placementCost / 2;

            _totalMaintenanceCostPerTick -= data.maintenanceCostPerMin;
            _totalIncomePerTick          -= data.incomePerTick;
            Population                   -= data.populationCapacity;
            Happiness                    -= data.happinessBonus;

            ElectricityUsed     -= data.powerConsumption;
            ElectricityCapacity -= data.powerProduction;
            WaterUsed           -= data.waterConsumption;
            WaterCapacity       -= data.waterProduction;
            WastePercentage      = Mathf.Clamp(WastePercentage - data.wasteProduction, 0f, 100f);

            Population  = Mathf.Max(0, Population);
            Happiness   = Mathf.Clamp(Happiness, 0f, 100f);
            OnResourcesChanged?.Invoke();
        }

        private void RunEconomyTick()
        {
            int net = _totalIncomePerTick - _totalMaintenanceCostPerTick;
            Money  += net;
            if (creativeMode) Money = Mathf.Max(startMoney, Money); // Keep afloat in creative mode
            else Money = Mathf.Max(0, Money);
            OnResourcesChanged?.Invoke();

            if (!creativeMode && _totalMaintenanceCostPerTick > _totalIncomePerTick)
                NotificationManager.Instance?.ShowNotification(
                    "City is losing money!", NotificationType.Warning);
        }
    }
}
