using CityScape.GridSystem.Data;
using UnityEngine;

namespace CityScape.GridSystem.Data
{
    // ─────────────────────────────────────────────
    //  Extended BuildingCategory enum
    // ─────────────────────────────────────────────

    /// <summary>
    /// High-level category a building belongs to.
    /// Extended from the original to match the UI toolbar.
    /// </summary>
    public enum BuildingCategory
    {
        Residential,    // Houses, apartments
        Commercial,     // Shops, markets
        Industrial,     // Factories, warehouses
        Utilities,      // Water tower, power plant
        Services,       // City hall, school, hospital
        Decoration,     // Parks, statues, trees
        Road,           // Road tiles
        Park            // Kept for backwards-compatibility — maps to Decoration
    }
}
