using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Main menu screen controller.
    /// Buttons: Continue (disabled if no save), New Game, Load Game, Settings, Quit.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Sub-Panels")]
        [SerializeField] private SaveSlotPanel saveSlotPanel;
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("Scene Names")]
        [SerializeField] private string gameSceneName = "SampleScene";

        [Header("Continue Label")]
        [SerializeField] private TextMeshProUGUI continueSubLabel;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Determine most recent save
            int recentSlot = SaveManager.Instance?.GetMostRecentSlot() ?? -1;
            bool hasSave   = recentSlot >= 0;

            continueButton.interactable = hasSave;
            if (continueSubLabel != null)
            {
                if (hasSave)
                {
                    var meta = SaveManager.Instance.GetSlotMetadata(recentSlot);
                    continueSubLabel.text = meta.displayLabel;
                }
                else
                {
                    continueSubLabel.text = "No save found";
                }
            }

            // Wire buttons
            continueButton?.onClick.AddListener(() => ContinueGame(recentSlot));
            newGameButton?.onClick.AddListener(    StartNewGame);
            loadGameButton?.onClick.AddListener(() => saveSlotPanel?.Show(SaveSlotPanel.Mode.Load));
            settingsButton?.onClick.AddListener(() => settingsPanel?.Show());
            quitButton?.onClick.AddListener(       QuitGame);
        }

        // ─────────────────────────────────────────────
        //  Actions
        // ─────────────────────────────────────────────

        private void ContinueGame(int slot)
        {
            // Store which slot to load, then load the game scene
            PlayerPrefs.SetInt("AutoLoadSlot", slot);
            SceneManager.LoadScene(gameSceneName);
        }

        private void StartNewGame()
        {
            PlayerPrefs.SetInt("AutoLoadSlot", -1);
            SceneManager.LoadScene(gameSceneName);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
