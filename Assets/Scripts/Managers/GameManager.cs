using System;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>Game speed state.</summary>
    public enum GameSpeed { Paused = 0, Normal = 1, Fast = 2, VeryFast = 3 }

    /// <summary>
    /// Top-level game orchestrator. Manages game time, day/night cycle,
    /// game speed, and coordinates save/load flow at a scene level.
    ///
    /// DontDestroyOnLoad so it persists across scene changes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Time Settings")]
        [Tooltip("Length of one in-game day in real seconds.")]
        [SerializeField] private float dayLengthSeconds = 300f;   // 5 real minutes = 1 game day

        [Header("Speed Multipliers")]
        [SerializeField] private float speedNormal   = 1f;
        [SerializeField] private float speedFast     = 2f;
        [SerializeField] private float speedVeryFast = 4f;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>Elapsed in-game time in seconds since the start of Day 1.</summary>
        public float GameTimeSeconds { get; private set; }

        /// <summary>Current in-game day number (1-based).</summary>
        public int CurrentDay { get; private set; } = 1;

        /// <summary>Fraction of the current day elapsed (0–1).</summary>
        public float DayFraction => (GameTimeSeconds % dayLengthSeconds) / dayLengthSeconds;

        /// <summary>Current time string e.g. "10:30 AM".</summary>
        public string TimeString => FractionToTimeString(DayFraction);

        public bool      IsPaused     { get; private set; }
        public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Normal;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        public event Action<int>       OnDayChanged;
        public event Action<bool>      OnPauseToggled;
        public event Action<GameSpeed> OnSpeedChanged;
        public event Action            OnNewGameStarted;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // When we enter the main game scene, process the auto-load request
            if (scene.name == "SampleScene")
            {
                int slotToLoad = PlayerPrefs.GetInt("AutoLoadSlot", -1);
                if (slotToLoad >= 0 && SaveManager.Instance != null && SaveManager.Instance.HasSave(slotToLoad))
                {
                    var data = SaveManager.Instance.LoadGame(slotToLoad);
                    if (data != null)
                    {
                        ApplySaveData(data);
                        EconomyManager.Instance?.ApplySaveData(data);
                        CameraManager.Instance?.ApplySaveData(data);
                        GridSystem.Placement.BuildingPlacer.Instance?.ApplySaveData(data);
                        GridSystem.Road.RoadPlacer.Instance?.ApplySaveData(data);
                    }
                }
                else
                {
                    StartNewGame();
                }
                
                // Clear so we don't accidentally reload if we reload the scene later
                PlayerPrefs.SetInt("AutoLoadSlot", -1);
            }
        }

        private void Update()
        {
            if (IsPaused) return;

            float multiplier = CurrentSpeed switch
            {
                GameSpeed.Normal   => speedNormal,
                GameSpeed.Fast     => speedFast,
                GameSpeed.VeryFast => speedVeryFast,
                _                  => 0f
            };

            GameTimeSeconds += Time.deltaTime * multiplier;

            int newDay = Mathf.FloorToInt(GameTimeSeconds / dayLengthSeconds) + 1;
            if (newDay != CurrentDay)
            {
                CurrentDay = newDay;
                OnDayChanged?.Invoke(CurrentDay);
                NotificationManager.Instance?.ShowNotification(
                    $"Day {CurrentDay} has begun!", NotificationType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        public void SetPaused(bool paused)
        {
            IsPaused        = paused;
            Time.timeScale  = paused ? 0f : 1f;
            OnPauseToggled?.Invoke(paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetSpeed(GameSpeed speed)
        {
            if (speed == GameSpeed.Paused) { SetPaused(true); return; }
            IsPaused       = false;
            CurrentSpeed   = speed;
            Time.timeScale  = 1f;             // actual multiplier applied in Update
            OnSpeedChanged?.Invoke(speed);
        }

        public void StartNewGame()
        {
            GameTimeSeconds = 0f;
            CurrentDay      = 1;
            IsPaused        = false;
            CurrentSpeed    = GameSpeed.Normal;
            EconomyManager.Instance?.ResetToDefaults();
            OnNewGameStarted?.Invoke();
        }

        public void ApplySaveData(SaveSystem.GameSaveData data)
        {
            GameTimeSeconds = data.currentGameTime;
            CurrentDay      = data.currentDay;
        }

        // ─────────────────────────────────────────────
        //  Time Helpers
        // ─────────────────────────────────────────────

        private static string FractionToTimeString(float fraction)
        {
            // 0 = midnight, 0.5 = noon
            int totalMinutes = Mathf.FloorToInt(fraction * 24 * 60);
            int hour         = totalMinutes / 60;
            int minute       = totalMinutes % 60;
            string ampm      = hour < 12 ? "AM" : "PM";
            int    h12       = hour % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12}:{minute:D2} {ampm}";
        }
    }
}
