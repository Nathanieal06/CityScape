using System;
using System.Collections.Generic;
using System.IO;
using CityScape.SaveSystem;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>
    /// Manages reading and writing save files to disk.
    /// Supports 3 independent save slots stored as JSON under
    /// Application.persistentDataPath/Saves/.
    ///
    /// Usage:
    ///   SaveManager.Instance.SaveGame(0);      // save to slot 0
    ///   var data = SaveManager.Instance.LoadGame(1);
    ///   SaveManager.Instance.DeleteSave(2);
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static SaveManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────

        public const int MaxSlots = 3;
        private const string SaveFolder   = "Saves";
        private const string SaveFileName = "slot_{0}.json";
        private const string MetaFileName = "slot_{0}_meta.json";

        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("Auto-Save")]
        [SerializeField] private bool  autoSaveEnabled         = true;
        [SerializeField] private float autoSaveIntervalMinutes = 5f;
        [SerializeField] private int   autoSaveSlot            = 0;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired after a successful save. Param = slot index.</summary>
        public event Action<int> OnSaveCompleted;

        /// <summary>Fired after a successful load. Param = slot index.</summary>
        public event Action<int, GameSaveData> OnLoadCompleted;

        /// <summary>Fired after a slot is deleted.</summary>
        public event Action<int> OnSaveDeleted;

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private string _savePath;
        private float  _autoSaveTimer;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _savePath = Path.Combine(Application.persistentDataPath, SaveFolder);
            Directory.CreateDirectory(_savePath);
            Debug.Log($"[SaveManager] Save path: {_savePath}");
        }

        private void Update()
        {
            if (!autoSaveEnabled) return;
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= autoSaveIntervalMinutes * 60f)
            {
                _autoSaveTimer = 0f;
                SaveGame(autoSaveSlot);
                NotificationManager.Instance?.ShowNotification("Game Auto-Saved", NotificationType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Serialises the current game state to the given slot (0–2).</summary>
        public void SaveGame(int slot)
        {
            if (!IsValidSlot(slot)) return;

            GameSaveData data = GatherSaveData();
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(GetSavePath(slot), json);

            SaveSlotMetadata meta = BuildMetadata(slot, data);
            File.WriteAllText(GetMetaPath(slot), JsonUtility.ToJson(meta, prettyPrint: true));

            Debug.Log($"[SaveManager] Saved slot {slot}.");
            OnSaveCompleted?.Invoke(slot);
        }

        /// <summary>Deserialises and returns save data from the given slot, or null if none exists.</summary>
        public GameSaveData LoadGame(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            string path = GetSavePath(slot);
            if (!File.Exists(path)) { Debug.LogWarning($"[SaveManager] No save in slot {slot}."); return null; }

            string json = File.ReadAllText(path);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            Debug.Log($"[SaveManager] Loaded slot {slot}.");
            OnLoadCompleted?.Invoke(slot, data);
            return data;
        }

        /// <summary>Deletes the save file and metadata for the given slot.</summary>
        public void DeleteSave(int slot)
        {
            if (!IsValidSlot(slot)) return;
            DeleteIfExists(GetSavePath(slot));
            DeleteIfExists(GetMetaPath(slot));
            Debug.Log($"[SaveManager] Deleted slot {slot}.");
            OnSaveDeleted?.Invoke(slot);
        }

        /// <summary>Returns true if a save file exists in the given slot.</summary>
        public bool HasSave(int slot)
            => IsValidSlot(slot) && File.Exists(GetSavePath(slot));

        /// <summary>Returns metadata for a slot, or an empty object if no save exists.</summary>
        public SaveSlotMetadata GetSlotMetadata(int slot)
        {
            if (!IsValidSlot(slot)) return new SaveSlotMetadata();
            string path = GetMetaPath(slot);
            if (!File.Exists(path)) return new SaveSlotMetadata { slotIndex = slot };
            return JsonUtility.FromJson<SaveSlotMetadata>(File.ReadAllText(path));
        }

        /// <summary>Returns metadata for all 3 slots.</summary>
        public SaveSlotMetadata[] GetAllSlotMetadata()
        {
            var result = new SaveSlotMetadata[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
                result[i] = GetSlotMetadata(i);
            return result;
        }

        /// <summary>Returns the index of the most-recently-written slot, or -1 if none.</summary>
        public int GetMostRecentSlot()
        {
            int   bestSlot = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < MaxSlots; i++)
            {
                string p = GetSavePath(i);
                if (!File.Exists(p)) continue;
                DateTime t = File.GetLastWriteTime(p);
                if (t <= bestTime) continue;
                bestTime = t;
                bestSlot = i;
            }
            return bestSlot;
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        private GameSaveData GatherSaveData()
        {
            var data = new GameSaveData();

            // Economy
            if (EconomyManager.Instance != null)
            {
                data.money               = EconomyManager.Instance.Money;
                data.population          = EconomyManager.Instance.Population;
                data.happiness           = EconomyManager.Instance.Happiness;
                data.electricityUsed     = EconomyManager.Instance.ElectricityUsed;
                data.electricityCapacity = EconomyManager.Instance.ElectricityCapacity;
                data.waterUsed           = EconomyManager.Instance.WaterUsed;
                data.waterCapacity       = EconomyManager.Instance.WaterCapacity;
                data.wastePercentage     = EconomyManager.Instance.WastePercentage;
            }

            // Time
            if (GameManager.Instance != null)
            {
                data.currentGameTime = GameManager.Instance.GameTimeSeconds;
                data.currentDay      = GameManager.Instance.CurrentDay;
            }

            // Camera
            if (CameraManager.Instance != null)
            {
                var camPos = CameraManager.Instance.GetCameraPosition();
                var camRot = CameraManager.Instance.GetCameraRotation();
                data.cameraPosX = camPos.x; data.cameraPosY = camPos.y; data.cameraPosZ = camPos.z;
                data.cameraRotX = camRot.x; data.cameraRotY = camRot.y;
                data.cameraRotZ = camRot.z; data.cameraRotW = camRot.w;
                data.cameraModeIndex = (int)CameraManager.Instance.CurrentMode;
            }

            // Buildings
            if (GridSystem.Placement.BuildingPlacer.Instance != null)
            {
                foreach (var kvp in GridSystem.Placement.BuildingPlacer.Instance.GetPlacedBuildings())
                {
                    var pb  = kvp.Value;
                    var pos = pb.GameObject != null ? pb.GameObject.transform.position : Vector3.zero;
                    var rot = pb.GameObject != null ? pb.GameObject.transform.rotation : Quaternion.identity;
                    data.buildings.Add(new BuildingSaveData
                    {
                        buildingID   = pb.Data.buildingID,
                        posX = pos.x, posY = pos.y, posZ = pos.z,
                        rotX = rot.x, rotY = rot.y, rotZ = rot.z, rotW = rot.w,
                        originX      = pb.Origin.X,
                        originY      = pb.Origin.Y,
                        rotationStep = pb.RotationStep,
                        level        = 0,
                        enabled      = true
                    });
                }
            }

            // Settings
            data.autoSaveEnabled          = autoSaveEnabled;
            data.autoSaveIntervalMinutes  = autoSaveIntervalMinutes;

            // Roads
            if (GridSystem.Road.RoadPlacer.Instance != null)
            {
                foreach (var kvp in GridSystem.Road.RoadPlacer.Instance.GetPlacedRoads())
                {
                    var pr = kvp.Value;
                    data.roads.Add(new SaveSystem.RoadSaveData
                    {
                        gridX = pr.Coordinates.X,
                        gridY = pr.Coordinates.Y,
                        rotationStep = 0
                    });
                }
            }

            return data;
        }

        private SaveSlotMetadata BuildMetadata(int slot, GameSaveData data)
        {
            return new SaveSlotMetadata
            {
                slotIndex    = slot,
                timestamp    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                dayCount     = data.currentDay,
                money        = data.money,
                population   = data.population,
                displayLabel = $"Day {data.currentDay} — ${data.money:N0}"
            };
        }

        private string GetSavePath(int slot) => Path.Combine(_savePath, string.Format(SaveFileName, slot));
        private string GetMetaPath(int slot) => Path.Combine(_savePath, string.Format(MetaFileName, slot));
        private static bool IsValidSlot(int slot) => slot >= 0 && slot < MaxSlots;
        private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }
    }
}
