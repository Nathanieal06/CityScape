using UnityEngine;

namespace CityScape.ExploreMode
{
    /// <summary>
    /// Attach this component to empty GameObjects inside your road prefabs.
    /// Place them on the sidewalks or crosswalks where you want NPCs to walk.
    /// The NPCController will automatically find these and navigate between them.
    /// </summary>
    public class NPCWaypoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            // Draw a small sphere in the editor so it's easy to see where waypoints are
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan
            Gizmos.DrawSphere(transform.position, 0.3f);
            
            // Draw a line pointing forward to show orientation (optional, but helpful)
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
        }
    }
}
