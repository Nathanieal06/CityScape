using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CityScape.GridSystem.Core;
using CityScape.GridSystem.Placement;
using CityScape.GridSystem.Road;
using CityScape.GridSystem.Data;
using CityScape.ExploreMode;

namespace CityScape.Managers
{
    public class NPCManager : MonoBehaviour
    {
        public static NPCManager Instance { get; private set; }

        [Header("NPC Settings")]
        [SerializeField] private GameObject npcPrefab;
        [SerializeField] private int maxNPCCount = 30;
        [SerializeField] private float spawnDelay = 2f;
        
        [Header("Population Settings")]
        // [Tooltip("How many residential buildings are required per NPC on average. E.g., if 2, then 10 houses = 5 NPCs. If 0.5, 10 houses = 20 NPCs.")]
        // [SerializeField] private float housesPerNPC = 1.0f; // Alternatively, use a stepped approach as requested

        private List<NPCController> _spawnedNPCs = new List<NPCController>();
        private int _residentialCount = 0;
        private List<GridCoordinates> _placedRoads = new List<GridCoordinates>();

        private Coroutine _spawnCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Subscribe to events once systems are ready. They are singletons.
            if (BuildingPlacer.Instance != null)
            {
                BuildingPlacer.Instance.OnBuildingPlaced += HandleBuildingPlaced;
                BuildingPlacer.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            }

            if (GridManager.Instance != null)
            {
                GridManager.Instance.OnRoadPlaced += HandleRoadPlaced;
                GridManager.Instance.OnRoadRemoved += HandleRoadRemoved;
            }

            // Also check RoadPlacer in case we loaded a save and roads are already there?
            // Actually, OnRoadPlaced might be called during load for roads. 
            // If not, we can fetch them here.
            if (RoadPlacer.Instance != null)
            {
                _placedRoads.AddRange(RoadPlacer.Instance.GetPlacedRoads().Keys);
            }

            // Similar for buildings if we need to load them, but PlacedBuildings might not be easily queryable yet.
            // Let's assume OnBuildingPlaced fires during load. GameManager's ApplySaveData calls BuildingPlacer.ApplySaveData which places buildings and fires events.

            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }

        private void HandleBuildingPlaced(BuildingData data, GridCoordinates coords)
        {
            if (data.category == BuildingCategory.Residential)
            {
                _residentialCount++;
            }
        }

        private void HandleBuildingRemoved(BuildingData data, GridCoordinates coords)
        {
            if (data.category == BuildingCategory.Residential)
            {
                _residentialCount--;
                if (_residentialCount < 0) _residentialCount = 0;
            }
        }

        private void HandleRoadPlaced(GridCoordinates coords)
        {
            if (!_placedRoads.Contains(coords))
            {
                _placedRoads.Add(coords);
            }
        }

        private void HandleRoadRemoved(GridCoordinates coords)
        {
            _placedRoads.Remove(coords);
        }

        private int GetTargetPopulation()
        {
            // Implement stepped approach as per request:
            // 1–5 houses → 2 NPCs
            // 6–10 houses → 5 NPCs
            // 20+ houses → 15+ NPCs
            
            if (_residentialCount == 0 || _placedRoads.Count == 0) return 0;

            if (_residentialCount <= 5) return 2;
            if (_residentialCount <= 10) return 5;
            if (_residentialCount <= 19) return 10;
            
            // 20+ houses
            return Mathf.Min(15 + (_residentialCount - 20) / 2, maxNPCCount);
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                int targetPop = GetTargetPopulation();

                // Clean up any destroyed NPCs from list (just in case)
                _spawnedNPCs.RemoveAll(npc => npc == null);

                if (_spawnedNPCs.Count < targetPop && _placedRoads.Count > 0)
                {
                    SpawnNPC();
                    yield return new WaitForSeconds(spawnDelay);
                }
                else
                {
                    yield return new WaitForSeconds(1f); // Check every second
                }
            }
        }

        private void SpawnNPC()
        {
            if (npcPrefab == null) return;

            // Pick a random road
            GridCoordinates spawnCoord = _placedRoads[Random.Range(0, _placedRoads.Count)];
            
            GameObject npcObj = Instantiate(npcPrefab);
            NPCController controller = npcObj.GetComponent<NPCController>();
            
            if (controller != null)
            {
                controller.Initialize(spawnCoord);
                _spawnedNPCs.Add(controller);
            }
            else
            {
                Debug.LogWarning("[NPCManager] NPC Prefab is missing NPCController component!");
                Destroy(npcObj);
            }
        }

        private void OnDestroy()
        {
            if (BuildingPlacer.Instance != null)
            {
                BuildingPlacer.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
                BuildingPlacer.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            }

            if (GridManager.Instance != null)
            {
                GridManager.Instance.OnRoadPlaced -= HandleRoadPlaced;
                GridManager.Instance.OnRoadRemoved -= HandleRoadRemoved;
            }

            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
            }
        }
    }
}
