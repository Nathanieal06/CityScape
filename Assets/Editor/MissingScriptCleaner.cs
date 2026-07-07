using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to find and remove all "Missing Script" components in the scene.
/// Access via: Tools → CityScape → Remove Missing Scripts
///
/// Missing scripts are the most common cause of SerializedObjectNotCreatableException
/// and GameObjectInspector MissingReferenceException errors in the Unity Editor.
/// </summary>
public static class MissingScriptCleaner
{
    [MenuItem("Tools/CityScape/Remove Missing Scripts From Scene")]
    public static void RemoveMissingScripts()
    {
        int totalRemoved = 0;
        int objectsAffected = 0;

        // Iterate over every root GameObject in the active scene (includes children)
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                Debug.Log($"[MissingScriptCleaner] Removed {removed} missing script(s) " +
                          $"from '{go.name}'.", go);
                totalRemoved   += removed;
                objectsAffected++;
            }
        }

        if (totalRemoved == 0)
        {
            Debug.Log("[MissingScriptCleaner] ✓ No missing scripts found in the scene.");
        }
        else
        {
            Debug.Log($"[MissingScriptCleaner] ✓ Removed {totalRemoved} missing script(s) " +
                      $"from {objectsAffected} GameObject(s). Save the scene to persist changes.");

            // Mark scene dirty so Unity prompts to save
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
    }

    [MenuItem("Tools/CityScape/Clear Console")]
    public static void ClearConsole()
    {
        // Uses reflection to access Unity's internal console clear method
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
        var logEntries = assembly.GetType("UnityEditor.LogEntries");
        var clearMethod = logEntries?.GetMethod("Clear");
        clearMethod?.Invoke(null, null);
        Debug.Log("[MissingScriptCleaner] Console cleared.");
    }
}
