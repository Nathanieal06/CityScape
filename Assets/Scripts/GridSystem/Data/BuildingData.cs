using UnityEngine;

namespace CityScape.GridSystem.Data
{
    // ─────────────────────────────────────────────
    //  ScriptableObject
    // ─────────────────────────────────────────────

    /// <summary>
    /// Data asset that fully describes a placeable building.
    /// Create via: Assets → Create → CityScape → Building Data
    ///
    /// SOLID note: This is pure data (SRP) — it owns no logic.
    /// The placement system reads it, keeping concerns separated.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBuildingData",
        menuName  = "CityScape/Building Data",
        order     = 0)]
    public class BuildingData : ScriptableObject
    {
        // ─────────────────────────────────────────────
        //  Identity
        // ─────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Unique string ID used for save/load lookup. Must be globally unique.")]
        public string buildingID = "building_001";

        [Tooltip("Human-readable name shown in the UI (e.g. 'Apartment Block').")]
        public string buildingName = "New Building";

        [Tooltip("Short description shown in the Building Info Panel.")]
        [TextArea(2, 4)]
        public string description = "A building in your city.";

        [Tooltip("Icon shown in the building palette UI (optional).")]
        public Sprite icon;

        [Tooltip("High-level category for economy and zone rules.")]
        public BuildingCategory category = BuildingCategory.Residential;

        // ─────────────────────────────────────────────
        //  Prefab
        // ─────────────────────────────────────────────

        [Header("Prefab")]
        [Tooltip("The real building prefab that is instantiated on placement.")]
        public GameObject prefab;

        // ─────────────────────────────────────────────
        //  Footprint
        // ─────────────────────────────────────────────

        [Header("Footprint (in grid cells)")]
        [Tooltip("How many grid cells wide this building occupies. E.g. House = 2, Factory = 4.")]
        [Min(1)]
        public int footprintWidth  = 2;

        [Tooltip("How many grid cells deep (along Z) this building occupies. E.g. House = 2, Factory = 4.")]
        [Min(1)]
        public int footprintHeight = 2;

        // ─────────────────────────────────────────────
        //  Economy
        // ─────────────────────────────────────────────

        [Header("Economy")]
        [Tooltip("Cost in the city's currency to place this building.")]
        [Min(0)]
        public int placementCost = 100;

        [Tooltip("Ongoing maintenance cost per in-game minute.")]
        [Min(0)]
        public int maintenanceCostPerMin = 5;

        [Tooltip("Income generated per simulation tick (future).")]
        [Min(0)]
        public int incomePerTick = 0;

        [Tooltip("Number of residents or workers this building supports.")]
        [Min(0)]
        public int populationCapacity = 0;

        [Tooltip("Happiness bonus/penalty this building applies to the city.")]
        public float happinessBonus = 0f;

        // ─────────────────────────────────────────────
        //  Utility Consumption / Production
        // ─────────────────────────────────────────────

        [Header("Utility — Consumption")]
        [Tooltip("Power consumed by this building per tick. Negative = produces power.")]
        public int powerConsumption = 0;

        [Tooltip("Water consumed by this building per tick. Negative = produces water.")]
        public int waterConsumption = 0;

        [Tooltip("Waste produced per tick (0–100 scale).")]
        [Range(0f, 100f)]
        public float wasteProduction = 0f;

        [Header("Utility — Production (Utilities only)")]
        [Tooltip("Power this building adds to the city's electricity capacity.")]
        [Min(0)]
        public int powerProduction = 0;

        [Tooltip("Water this building adds to the city's water capacity.")]
        [Min(0)]
        public int waterProduction = 0;

        // ─────────────────────────────────────────────
        //  Requirements
        // ─────────────────────────────────────────────

        [Header("Requirements")]
        [Tooltip("Whether this building requires a road connection to operate.")]
        public bool requiresRoadAccess = false;

        [Tooltip("Whether this building requires a water connection.")]
        public bool requiresWater = false;

        [Tooltip("Whether this building requires a power connection.")]
        public bool requiresPower = false;

        [Tooltip("Optional unlock requirement description shown in UI.")]
        public string unlockRequirement = "";

        [Tooltip("Time in seconds to construct this building (future build queue).")]
        [Min(0f)]
        public float buildTimeSeconds = 0f;

        // ─────────────────────────────────────────────
        //  Placement Offset / Pivot Correction
        // ─────────────────────────────────────────────

        [Header("Placement Offset (Pivot Correction)")]
        [Tooltip(
            "World-space XYZ offset added to the grid cell centre before spawning.\n\n" +
            "X / Z → correct left/right or forward/back pivot misalignment.\n" +
            "Y     → lift or lower the building (use a negative value for roads).\n\n" +
            "Calibration: enable 'Debug Show Footprint', then adjust X/Z until\n" +
            "the mesh sits inside the yellow wireframe box in the Scene view.")]
        public Vector3 placementOffset = Vector3.zero;

        [Tooltip(
            "When enabled, GridManager draws a yellow wireframe box in the Scene\n" +
            "view showing the exact footprint this building type will occupy.\n" +
            "Toggle this ON while calibrating placementOffset, then turn it OFF.")]
        public bool debugShowFootprint = false;

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the total number of grid cells this building occupies
        /// (ignoring rotation; the validator accounts for rotation).
        /// </summary>
        public int TotalCells => footprintWidth * footprintHeight;

        /// <summary>Returns formatted size string e.g. "2 x 2".</summary>
        public string SizeLabel => $"{footprintWidth} x {footprintHeight}";

        /// <summary>Net power impact (production minus consumption).</summary>
        public int NetPower => powerProduction - powerConsumption;

        /// <summary>Net water impact (production minus consumption).</summary>
        public int NetWater => waterProduction - waterConsumption;
    }
}
