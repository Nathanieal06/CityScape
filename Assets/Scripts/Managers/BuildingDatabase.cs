using System;
using System.Collections.Generic;
using System.Linq;
using CityScape.GridSystem.Data;
using UnityEngine;

namespace CityScape.Managers
{
    /// <summary>
    /// Central database for all BuildingData ScriptableObjects.
    ///
    /// Buildings are loaded from Resources/Buildings/ at startup.
    /// Use GetByID(), GetByCategory(), or GetAll() to query.
    ///
    /// No FindObjectOfType or direct asset loading elsewhere — everything
    /// routes through this class (Open/Closed Principle).
    /// </summary>
    public class BuildingDatabase : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────

        public static BuildingDatabase Instance { get; private set; }

        // ─────────────────────────────────────────────
        //  Inspector — optional manual override list
        // ─────────────────────────────────────────────

        [Header("Manual Database (overrides Resources/ loading if populated)")]
        [Tooltip("Drag BuildingData assets here to populate the database manually " +
                 "instead of relying on Resources/Buildings/.")]
        [SerializeField] private List<BuildingData> manualDatabase = new List<BuildingData>();

        // ─────────────────────────────────────────────
        //  Private State
        // ─────────────────────────────────────────────

        private readonly Dictionary<string, BuildingData>              _byID       = new();
        private readonly Dictionary<BuildingCategory, List<BuildingData>> _byCategory = new();

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadDatabase();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>Returns the BuildingData with the given ID, or null if not found.</summary>
        public BuildingData GetByID(string id)
            => _byID.TryGetValue(id, out var data) ? data : null;

        /// <summary>Returns all buildings in the given category.</summary>
        public IReadOnlyList<BuildingData> GetByCategory(BuildingCategory category)
            => _byCategory.TryGetValue(category, out var list) ? list : Array.Empty<BuildingData>();

        /// <summary>Returns every registered BuildingData.</summary>
        public IReadOnlyCollection<BuildingData> GetAll() => _byID.Values;

        // ─────────────────────────────────────────────
        //  Loading
        // ─────────────────────────────────────────────

        private void LoadDatabase()
        {
            _byID.Clear();
            _byCategory.Clear();

            IEnumerable<BuildingData> source;

#if UNITY_EDITOR
            // Automatically find all BuildingData in the project when playing in the Editor
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BuildingData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                BuildingData data = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data != null)
                {
                    // Auto-fix default IDs so they don't get rejected as duplicates
                    if (string.IsNullOrEmpty(data.buildingID) || data.buildingID == "building_001")
                    {
                        data.buildingID = data.name;
                        UnityEditor.EditorUtility.SetDirty(data);
                    }

                    if (!manualDatabase.Contains(data))
                    {
                        manualDatabase.Add(data);
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
            }
#endif

            if (manualDatabase != null && manualDatabase.Count > 0)
            {
                source = manualDatabase.Where(b => b != null);
            }
            else
            {
                // Fallback: load from Resources/Buildings/
                var loaded = Resources.LoadAll<BuildingData>("Buildings");
                source = loaded;
            }

            foreach (BuildingData data in source)
                Register(data);

            Debug.Log($"[BuildingDatabase] Loaded {_byID.Count} buildings.");
        }

        private void Register(BuildingData data)
        {
            if (data == null) return;

            if (string.IsNullOrEmpty(data.buildingID))
            {
                Debug.LogWarning($"[BuildingDatabase] '{data.name}' has no buildingID — skipping.");
                return;
            }

            if (_byID.ContainsKey(data.buildingID))
            {
                Debug.LogWarning($"[BuildingDatabase] Duplicate buildingID '{data.buildingID}' on '{data.name}'.");
                return;
            }

            _byID[data.buildingID] = data;

            if (!_byCategory.ContainsKey(data.category))
                _byCategory[data.category] = new List<BuildingData>();

            _byCategory[data.category].Add(data);
        }
    }
}
