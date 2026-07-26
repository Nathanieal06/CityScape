using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CityScape.GridSystem.Core;
using CityScape.GridSystem.Road;

namespace CityScape.ExploreMode
{
    public class NPCController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float idleTime = 2f;
        [SerializeField] private float yOffset = 1f;
        [Tooltip("Multiplier for cell size to determine how far from the road center the NPC walks. Increase to push them further onto the sidewalk.")]
        [SerializeField] private float sidewalkOffsetMultiplier = 0.95f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("Float parameter used by the Player Controller for movement.")]
        [SerializeField] private string speedParam = "Speed";
        [Tooltip("Float parameter used by the Player Controller for 2D blend trees.")]
        [SerializeField] private string velocityZParam = "VelocityZ";
        [Tooltip("Bool parameter used by the Player Controller to check if on ground.")]
        [SerializeField] private string isGroundedParam = "IsGrounded";

        private GridManager _gridManager;
        private GridCoordinates _currentCell;
        private GridCoordinates _targetCell;
        private Vector3 _targetPosition;

        private bool _isWalking = false;
        private bool _isPaused = false;
        private Coroutine _behaviorCoroutine;
        
        private Vector3 _personalOffset;

        private void Awake()
        {
            if (sidewalkOffsetMultiplier == 0f)
            {
                sidewalkOffsetMultiplier = 0.95f;
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        public void Initialize(GridCoordinates spawnCell)
        {
            if (sidewalkOffsetMultiplier <= 0.01f) sidewalkOffsetMultiplier = 0.95f; // Bulletproof safeguard for hot-reloads

            _gridManager = GridManager.Instance;
            
            // Snap the spawn cell to the origin of the 2x2 road block!
            _currentCell = new GridCoordinates((spawnCell.X / 2) * 2, (spawnCell.Y / 2) * 2);
            
            // Assign a persistent world-space corner offset to keep the NPC on the exact same sidewalk globally
            float offsetVal = _gridManager.CellSize * sidewalkOffsetMultiplier;
            float offsetX = Random.value > 0.5f ? offsetVal : -offsetVal;
            float offsetZ = Random.value > 0.5f ? offsetVal : -offsetVal;
            _personalOffset = new Vector3(offsetX, 0, offsetZ);

            if (TryGetWaypointPosition(_currentCell, _personalOffset, out Vector3 spawnPos))
            {
                _targetPosition = spawnPos;
                _targetPosition.y += yOffset;
            }
            else
            {
                _targetPosition = _gridManager.GetFootprintCenter(_currentCell, 2, 2);
                _targetPosition.y += yOffset;
                _targetPosition += _personalOffset;
            }

            transform.position = _targetPosition;

            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (animator != null)
            {
                float speed = (_isWalking && !_isPaused) ? 0.5f : 0f;
                animator.SetFloat(speedParam, speed);
                animator.SetFloat(velocityZParam, speed);
                animator.SetBool(isGroundedParam, true);
            }
        }

        private IEnumerator BehaviorRoutine()
        {
            while (true)
            {
                yield return new WaitUntil(() => !_isPaused);

                // Wait for idle time
                _isWalking = false;
                if (animator != null) 
                {
                    animator.SetFloat(speedParam, 0f);
                    animator.SetFloat(velocityZParam, 0f);
                    animator.SetBool(isGroundedParam, true);
                }
                yield return new WaitForSeconds(idleTime);

                yield return new WaitUntil(() => !_isPaused);

                // Find next road cell
                if (TryFindNextDestination(out _targetCell))
                {
                    Vector3 currentCenter = _gridManager.GetFootprintCenter(_currentCell, 2, 2);
                    Vector3 nextCenter = _gridManager.GetFootprintCenter(_targetCell, 2, 2);
                    
                    if (TryGetWaypointPosition(_targetCell, _personalOffset, out Vector3 waypointPos))
                    {
                        _targetPosition = waypointPos;
                        _targetPosition.y += yOffset;
                        
                        _isWalking = true;
                        if (animator != null) 
                        {
                            animator.SetFloat(speedParam, 0.5f);
                            animator.SetFloat(velocityZParam, 0.5f);
                            animator.SetBool(isGroundedParam, true);
                        }

                        // Walk to target
                        while (Vector3.Distance(transform.position, _targetPosition) > 0.1f)
                        {
                            if (!_isPaused)
                            {
                                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, walkSpeed * Time.deltaTime);
                                Vector3 dir = (_targetPosition - transform.position).normalized;
                                if (dir != Vector3.zero)
                                {
                                    Quaternion lookRot = Quaternion.LookRotation(dir);
                                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
                                }
                            }
                            yield return null;
                        }
                    }
                    else
                    {
                        // Fallback: simply walk to the same personal offset corner of the next road tile
                        _targetPosition = nextCenter + _personalOffset;
                        _targetPosition.y += yOffset;
                        
                        _isWalking = true;
                        if (animator != null) 
                        {
                            animator.SetFloat(speedParam, 0.5f);
                            animator.SetFloat(velocityZParam, 0.5f);
                            animator.SetBool(isGroundedParam, true);
                        }

                        // Walk to target
                        while (Vector3.Distance(transform.position, _targetPosition) > 0.1f)
                        {
                            if (!_isPaused)
                            {
                                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, walkSpeed * Time.deltaTime);
                                Vector3 dir = (_targetPosition - transform.position).normalized;
                                if (dir != Vector3.zero)
                                {
                                    Quaternion lookRot = Quaternion.LookRotation(dir);
                                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
                                }
                            }
                            yield return null;
                        }
                    }

                    _currentCell = _targetCell;
                }
                else
                {
                    // No valid path found (stuck). Just wait a bit and try again.
                    yield return new WaitForSeconds(1f);
                }
            }
        }

        private bool TryFindNextDestination(out GridCoordinates nextCell)
        {
            nextCell = _currentCell;
            if (_gridManager == null) return false;

            // Get neighbours (size 2 since road footprint is 2)
            IEnumerable<GridCoordinates> neighbours = RoadTileSelector.GetBlockNeighbours(_currentCell, 2);
            List<GridCoordinates> validRoads = new List<GridCoordinates>();

            foreach (var n in neighbours)
            {
                GridCell cell = _gridManager.GetCell(n);
                if (cell != null && cell.HasRoad)
                {
                    validRoads.Add(n);
                }
            }

            if (validRoads.Count > 0)
            {
                // Pick random road
                nextCell = validRoads[Random.Range(0, validRoads.Count)];
                return true;
            }

            return false;
        }

        private bool TryGetWaypointPosition(GridCoordinates cellCoords, Vector3 idealLocalOffset, out Vector3 position)
        {
            position = Vector3.zero;
            if (RoadPlacer.Instance == null || _gridManager == null) return false;

            if (RoadPlacer.Instance.GetPlacedRoads().TryGetValue(cellCoords, out PlacedRoad road))
            {
                if (road.GameObject == null) return false;

                NPCWaypoint[] waypoints = road.GameObject.GetComponentsInChildren<NPCWaypoint>();
                if (waypoints.Length == 0) return false;

                Vector3 cellCenter = _gridManager.GetFootprintCenter(cellCoords, 2, 2);

                NPCWaypoint bestWaypoint = null;
                float bestDist = float.MaxValue;

                foreach (var wp in waypoints)
                {
                    Vector3 offsetFromCenter = wp.transform.position - cellCenter;
                    offsetFromCenter.y = 0;

                    // If the waypoint is dead center, the user probably forgot to move it! Ignore it.
                    if (offsetFromCenter.magnitude < 0.1f) continue;

                    // Pick the waypoint closest to their ideal persistent corner offset
                    float dist = Vector3.Distance(offsetFromCenter, idealLocalOffset);
                    
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestWaypoint = wp;
                    }
                }

                if (bestWaypoint != null)
                {
                    position = bestWaypoint.transform.position;
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_behaviorCoroutine != null)
            {
                StopCoroutine(_behaviorCoroutine);
            }
        }
    }
}
