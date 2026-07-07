using CityScape.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Pause menu panel controller. Shown when the game is paused (ESC or pause button).
    /// Buttons: Resume, Save, Load, Settings, Main Menu, Quit.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Save Slot Panel")]
        [SerializeField] private SaveSlotPanel saveSlotPanel;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            resumeButton?.onClick.AddListener(OnResume);
            saveButton?.onClick.AddListener(  OnSave);
            loadButton?.onClick.AddListener(  OnLoad);
            settingsButton?.onClick.AddListener(() => UIManager.Instance?.ShowSettings());
            mainMenuButton?.onClick.AddListener(OnMainMenu);
            quitButton?.onClick.AddListener(   OnQuit);
        }

        // ─────────────────────────────────────────────
        //  Button Handlers
        // ─────────────────────────────────────────────

        private void OnResume()
        {
            GameManager.Instance?.SetPaused(false);
        }

        private void OnSave()
        {
            if (saveSlotPanel != null)
            {
                saveSlotPanel.Show(SaveSlotPanel.Mode.Save);
            }
            else
            {
                // Fallback: Save to Slot 0 automatically if UI is not assigned
                SaveManager.Instance?.SaveGame(0);
                NotificationManager.Instance?.ShowNotification("Game Auto-Saved to Slot 1", NotificationType.Success);
                OnResume(); // Close pause menu
            }
        }

        private void OnLoad()
        {
            if (saveSlotPanel != null)
            {
                saveSlotPanel.Show(SaveSlotPanel.Mode.Load);
            }
            else
            {
                // Fallback: Load from Slot 0 automatically if UI is not assigned
                var data = SaveManager.Instance?.LoadGame(0);
                if (data != null)
                {
                    GameManager.Instance?.ApplySaveData(data);
                    EconomyManager.Instance?.ApplySaveData(data);
                    CameraManager.Instance?.ApplySaveData(data);
                    GridSystem.Placement.BuildingPlacer.Instance?.ApplySaveData(data);
                    GridSystem.Road.RoadPlacer.Instance?.ApplySaveData(data);
                    
                    NotificationManager.Instance?.ShowNotification("Game Loaded from Slot 1", NotificationType.Success);
                    OnResume(); // Unpause and close pause menu
                }
                else
                {
                    NotificationManager.Instance?.ShowNotification("No save found in Slot 1", NotificationType.Warning);
                }
            }
        }

        private void OnMainMenu()
        {
            // Auto-save before leaving
            try { SaveManager.Instance?.SaveGame(0); } catch (System.Exception e) { Debug.LogError($"Auto-save failed: {e}"); }
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnQuit()
        {
            try { SaveManager.Instance?.SaveGame(0); } catch (System.Exception e) { Debug.LogError($"Auto-save failed: {e}"); }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
