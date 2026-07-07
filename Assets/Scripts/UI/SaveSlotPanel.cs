using System;
using CityScape.Managers;
using CityScape.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Displays 3 save slots with metadata (day, money, timestamp).
    /// Works for both Save and Load operations based on the Mode enum.
    /// </summary>
    public class SaveSlotPanel : MonoBehaviour
    {
        public enum Mode { Save, Load }

        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Slot Entries")]
        [SerializeField] private SaveSlotEntry[] slotEntries = new SaveSlotEntry[SaveManager.MaxSlots];

        [Header("Close Button")]
        [SerializeField] private Button closeButton;

        // ─────────────────────────────────────────────
        //  Nested
        // ─────────────────────────────────────────────

        [Serializable]
        public class SaveSlotEntry
        {
            public int               slotIndex;
            public TextMeshProUGUI   slotLabel;
            public TextMeshProUGUI   metaLabel;
            public Button            actionButton;
            public Button            deleteButton;
            public TextMeshProUGUI   actionButtonLabel;
        }

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private Mode _currentMode;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            closeButton?.onClick.AddListener(Hide);
            Hide();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        public void Show(Mode mode)
        {
            _currentMode = mode;
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshSlots();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  Slot Population
        // ─────────────────────────────────────────────

        private void RefreshSlots()
        {
            if (SaveManager.Instance == null) return;

            var metas = SaveManager.Instance.GetAllSlotMetadata();

            for (int i = 0; i < slotEntries.Length && i < SaveManager.MaxSlots; i++)
            {
                var entry = slotEntries[i];
                var meta  = metas[i];
                int slot  = i;

                if (entry.slotLabel != null)
                    entry.slotLabel.text = $"Slot {slot + 1}";

                if (entry.metaLabel != null)
                    entry.metaLabel.text = meta.IsValid
                        ? $"Day {meta.dayCount} | ${meta.money:N0} | {meta.timestamp}"
                        : "— Empty —";

                // Action button
                if (entry.actionButton != null)
                {
                    entry.actionButton.onClick.RemoveAllListeners();
                    bool hasSave = meta.IsValid;

                    if (_currentMode == Mode.Save)
                    {
                        entry.actionButton.interactable = true;
                        if (entry.actionButtonLabel != null)
                            entry.actionButtonLabel.text = hasSave ? "Overwrite" : "Save Here";
                        entry.actionButton.onClick.AddListener(() => OnSave(slot));
                    }
                    else // Load
                    {
                        entry.actionButton.interactable = hasSave;
                        if (entry.actionButtonLabel != null)
                            entry.actionButtonLabel.text = "Load";
                        entry.actionButton.onClick.AddListener(() => OnLoad(slot));
                    }
                }

                // Delete button
                if (entry.deleteButton != null)
                {
                    entry.deleteButton.onClick.RemoveAllListeners();
                    entry.deleteButton.interactable = meta.IsValid;
                    entry.deleteButton.onClick.AddListener(() => OnDelete(slot));
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Slot Actions
        // ─────────────────────────────────────────────

        private void OnSave(int slot)
        {
            SaveManager.Instance?.SaveGame(slot);
            NotificationManager.Instance?.ShowNotification($"Game Saved to Slot {slot + 1}", NotificationType.Success);
            RefreshSlots();
        }

        private void OnLoad(int slot)
        {
            var data = SaveManager.Instance?.LoadGame(slot);
            if (data == null) return;
            GameManager.Instance?.ApplySaveData(data);
            EconomyManager.Instance?.ApplySaveData(data);
            CameraManager.Instance?.ApplySaveData(data);
            GridSystem.Placement.BuildingPlacer.Instance?.ApplySaveData(data);
            GridSystem.Road.RoadPlacer.Instance?.ApplySaveData(data);
            Hide();
            GameManager.Instance?.SetPaused(false);
            NotificationManager.Instance?.ShowNotification($"Slot {slot + 1} Loaded!", NotificationType.Success);
        }

        private void OnDelete(int slot)
        {
            SaveManager.Instance?.DeleteSave(slot);
            NotificationManager.Instance?.ShowNotification($"Slot {slot + 1} Deleted", NotificationType.Info);
            RefreshSlots();
        }
    }
}
