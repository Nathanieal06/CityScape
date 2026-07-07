using System.Collections.Generic;
using CityScape.GridSystem.Data;
using CityScape.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Horizontal scrollable panel of BuildingCard items.
    /// Refreshes whenever the active BuildingCategory changes.
    ///
    /// Uses a fixed card pool (up to maxCards) — cards beyond that are hidden.
    /// </summary>
    public class BuildingSelectionPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Layout")]
        [Tooltip("Parent transform (Horizontal Layout Group) for card children.")]
        [SerializeField] private Transform cardContainer;

        [Tooltip("BuildingCard prefab to instantiate.")]
        [SerializeField] private BuildingCard cardPrefab;

        [Header("Scroll")]
        [SerializeField] private Button scrollLeftButton;
        [SerializeField] private Button scrollRightButton;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Pool Size")]
        [SerializeField] private int maxCards = 20;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private readonly List<BuildingCard> _cards = new List<BuildingCard>();
        private BuildingData                _selectedData;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Pre-instantiate card pool
            for (int i = 0; i < maxCards; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.gameObject.SetActive(false);
                card.OnCardClicked += OnCardClicked;
                _cards.Add(card);
            }

            // Subscribe to category changes
            if (BuildManager.Instance != null)
                BuildManager.Instance.OnCategoryChanged += RefreshForCategory;

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnBuildingSelected += OnBuildingSelected;

            // Scroll buttons
            scrollLeftButton?.onClick.AddListener(ScrollLeft);
            scrollRightButton?.onClick.AddListener(ScrollRight);

            // Initial populate
            RefreshForCategory(BuildingCategory.Residential);
        }

        private void OnDestroy()
        {
            if (BuildManager.Instance != null)
                BuildManager.Instance.OnCategoryChanged -= RefreshForCategory;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnBuildingSelected -= OnBuildingSelected;
        }

        // ─────────────────────────────────────────────
        //  Refresh
        // ─────────────────────────────────────────────

        private void RefreshForCategory(BuildingCategory category)
        {
            _selectedData = null;

            // Deactivate all cards
            foreach (var c in _cards) c.gameObject.SetActive(false);

            if (BuildingDatabase.Instance == null) return;

            var buildings = BuildingDatabase.Instance.GetByCategory(category);
            int count     = Mathf.Min(buildings.Count, maxCards);

            for (int i = 0; i < count; i++)
            {
                _cards[i].gameObject.SetActive(true);
                _cards[i].Populate(buildings[i], isSelected: false);
            }

            // Scroll back to start
            if (scrollRect != null) scrollRect.normalizedPosition = new Vector2(0f, 0f);
        }

        // ─────────────────────────────────────────────
        //  Card Interaction
        // ─────────────────────────────────────────────

        private void OnCardClicked(BuildingData data)
        {
            _selectedData = data;
            // Highlight selected card
            foreach (var c in _cards)
                c.SetSelected(c.CurrentData == data);

            // Show info panel
            UIManager.Instance?.GetComponent<UI.UIManager>();
            BuildingInfoPanel infoPnl = FindFirstObjectByType<BuildingInfoPanel>();
            infoPnl?.ShowBuilding(data);
        }

        private void OnBuildingSelected(BuildingData data)
        {
            // Sync highlights when selection changes externally
            foreach (var c in _cards)
                c.SetSelected(data != null && c.CurrentData == data);
        }

        // ─────────────────────────────────────────────
        //  Scroll Helpers
        // ─────────────────────────────────────────────

        private void ScrollLeft()
        {
            if (scrollRect == null) return;
            scrollRect.normalizedPosition = new Vector2(
                Mathf.Clamp01(scrollRect.normalizedPosition.x - 0.2f), 0f);
        }

        private void ScrollRight()
        {
            if (scrollRect == null) return;
            scrollRect.normalizedPosition = new Vector2(
                Mathf.Clamp01(scrollRect.normalizedPosition.x + 0.2f), 0f);
        }
    }
}
