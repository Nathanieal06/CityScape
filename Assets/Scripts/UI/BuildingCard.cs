using System;
using CityScape.GridSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// A single building card in the horizontal selection panel.
    /// Handles hover tooltip, click selection, and selection highlight ring.
    /// </summary>
    public class BuildingCard : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Card Elements")]
        [SerializeField] private Image           buildingIcon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private GameObject      selectionRing;

        [Header("Tooltip")]
        [SerializeField] private GameObject      tooltipRoot;
        [SerializeField] private TextMeshProUGUI tooltipText;

        [Header("Colors")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color normalColor   = new Color(0.12f, 0.15f, 0.2f);
        [SerializeField] private Color hoverColor    = new Color(0.2f,  0.25f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(0.15f, 0.4f,  0.7f);

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired when the card is clicked. Param = building data.</summary>
        public event Action<BuildingData> OnCardClicked;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        public BuildingData CurrentData { get; private set; }

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
            if (selectionRing != null) selectionRing.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Fills the card with building data.</summary>
        public void Populate(BuildingData data, bool isSelected)
        {
            CurrentData = data;
            if (nameLabel != null) nameLabel.text = data.buildingName;
            if (costLabel != null) costLabel.text = $"{data.placementCost}";

            if (buildingIcon != null)
            {
                buildingIcon.sprite  = data.icon;
                buildingIcon.enabled = data.icon != null;
            }

            if (tooltipText != null)
                tooltipText.text = $"{data.buildingName}\n{data.description}";

            SetSelected(isSelected);
        }

        /// <summary>Toggles the selection highlight ring.</summary>
        public void SetSelected(bool selected)
        {
            if (selectionRing != null) selectionRing.SetActive(selected);
            if (cardBackground != null)
                cardBackground.color = selected ? selectedColor : normalColor;
        }

        // ─────────────────────────────────────────────
        //  Pointer Callbacks
        // ─────────────────────────────────────────────

        public void OnPointerClick(PointerEventData _)
        {
            if (CurrentData == null) return;
            OnCardClicked?.Invoke(CurrentData);
            Managers.SelectionManager.Instance?.SelectBuilding(CurrentData);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (cardBackground != null && selectionRing != null && !selectionRing.activeSelf)
                cardBackground.color = hoverColor;

            if (tooltipRoot != null) tooltipRoot.SetActive(true);
        }

        public void OnPointerExit(PointerEventData _)
        {
            if (cardBackground != null && selectionRing != null && !selectionRing.activeSelf)
                cardBackground.color = normalColor;

            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }
    }
}
