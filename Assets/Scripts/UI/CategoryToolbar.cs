using System;
using System.Collections.Generic;
using CityScape.GridSystem.Data;
using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// The bottom tab bar with category buttons (Residential, Commercial, etc.)
    /// and the Bulldozer button.
    ///
    /// Reads BuildingCategory enum values to generate buttons dynamically,
    /// or uses manually-assigned buttons in the Inspector.
    /// </summary>
    public class CategoryToolbar : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Serializable]
        public class CategoryButton
        {
            public BuildingCategory category;
            public Button           button;
            public Image            buttonBackground;
        }

        [Header("Category Buttons (assign in Inspector)")]
        [SerializeField] private List<CategoryButton> categoryButtons = new List<CategoryButton>();

        [Header("Bulldozer")]
        [SerializeField] private Button bulldozerButton;
        [SerializeField] private Image  bulldozerBackground;

        [Header("Colors")]
        [SerializeField] private Color activeColor   = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.15f, 0.15f, 0.2f);
        [SerializeField] private Color bulldozerActiveColor = new Color(0.8f, 0.2f, 0.2f);

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Wire category buttons
            foreach (var cb in categoryButtons)
            {
                BuildingCategory cat = cb.category; // capture for closure
                cb.button?.onClick.AddListener(() => BuildManager.Instance?.SetCategory(cat));
            }

            // Wire bulldozer
            bulldozerButton?.onClick.AddListener(() => BuildManager.Instance?.ToggleBulldozer());

            // Subscribe to manager events
            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.OnCategoryChanged   += RefreshCategoryHighlight;
                BuildManager.Instance.OnBulldozerToggled  += RefreshBulldozerHighlight;
            }

            // Default highlight
            RefreshCategoryHighlight(BuildingCategory.Residential);
        }

        private void OnDestroy()
        {
            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.OnCategoryChanged  -= RefreshCategoryHighlight;
                BuildManager.Instance.OnBulldozerToggled -= RefreshBulldozerHighlight;
            }
        }

        // ─────────────────────────────────────────────
        //  Highlight Logic
        // ─────────────────────────────────────────────

        private void RefreshCategoryHighlight(BuildingCategory activeCategory)
        {
            foreach (var cb in categoryButtons)
            {
                if (cb.buttonBackground != null)
                    cb.buttonBackground.color = cb.category == activeCategory
                        ? activeColor
                        : inactiveColor;
            }
            // Deactivate bulldozer highlight
            if (bulldozerBackground != null)
                bulldozerBackground.color = inactiveColor;
        }

        private void RefreshBulldozerHighlight(bool active)
        {
            if (bulldozerBackground != null)
                bulldozerBackground.color = active ? bulldozerActiveColor : inactiveColor;

            // Deactivate all category highlights when bulldozer is on
            if (active)
                foreach (var cb in categoryButtons)
                    if (cb.buttonBackground != null)
                        cb.buttonBackground.color = inactiveColor;
        }
    }
}
