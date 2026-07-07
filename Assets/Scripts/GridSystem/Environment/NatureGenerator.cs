using CityScape.GridSystem.Core;
using UnityEngine;

namespace CityScape.GridSystem.Environment
{
    /// <summary>
    /// Procedurally generates trees and grass across the grid at startup.
    /// Nature objects are attached to GridCells and can be cleared dynamically
    /// when buildings or roads are placed.
    /// </summary>
    public class NatureGenerator : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Array of tree prefabs to spawn randomly.")]
        [SerializeField] private GameObject[] treePrefabs;
        
        [Tooltip("Array of grass/bush prefabs to spawn randomly.")]
        [SerializeField] private GameObject[] grassPrefabs;

        [Header("Settings")]
        [Tooltip("Probability (0 to 1) of a cell containing a tree.")]
        [Range(0f, 1f)]
        [SerializeField] private float treeDensity = 0.05f;

        [Tooltip("Probability (0 to 1) of a cell containing grass (if no tree is present).")]
        [Range(0f, 1f)]
        [SerializeField] private float grassDensity = 0.15f;

        [Header("Hierarchy")]
        [Tooltip("Parent transform to hold all spawned nature objects. If null, this transform is used.")]
        [SerializeField] private Transform natureContainer;

        private void Start()
        {
            if (natureContainer == null)
                natureContainer = transform;

            GenerateNature();
        }

        private void GenerateNature()
        {
            var gm = GridManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[NatureGenerator] GridManager not found. Cannot generate nature.");
                return;
            }

            int width = gm.GridWidth;
            int height = gm.GridHeight;
            float cellSize = gm.CellSize;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var coords = new GridCoordinates(x, y);
                    var cell = gm.GetCell(coords);

                    // Skip if something is already there
                    if (cell.IsBlocked) continue;

                    // 1. Try to spawn a tree
                    if (treePrefabs != null && treePrefabs.Length > 0 && Random.value < treeDensity)
                    {
                        SpawnNature(treePrefabs, cell, gm, cellSize);
                        continue; // Cell is taken by tree, don't spawn grass
                    }

                    // 2. Try to spawn grass
                    if (grassPrefabs != null && grassPrefabs.Length > 0 && Random.value < grassDensity)
                    {
                        SpawnNature(grassPrefabs, cell, gm, cellSize);
                    }
                }
            }
        }

        private void SpawnNature(GameObject[] prefabs, GridCell cell, GridManager gm, float cellSize)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) return;

            // Base position is the centre of the cell
            Vector3 position = gm.GridToWorld(cell.Coordinates);

            // Add some organic jitter so they don't look perfectly grid-aligned
            float jitterLimit = cellSize * 0.35f;
            position.x += Random.Range(-jitterLimit, jitterLimit);
            position.z += Random.Range(-jitterLimit, jitterLimit);

            // Random rotation on Y axis
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var go = Instantiate(prefab, position, rotation, natureContainer);
            go.name = $"{prefab.name}_{cell.Coordinates.X}_{cell.Coordinates.Y}";

            // Register it with the cell so it can be cleared later
            cell.NatureObject = go;
        }
    }
}
