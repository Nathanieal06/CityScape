#if UNITY_EDITOR
using CityScape.GridSystem.Environment;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for GroundGridGenerator.
/// Adds two buttons:
///   - "Generate Ground"  — spawns / refreshes the tile grid
///   - "Clear Ground"     — removes all tiles
/// Both work in Edit Mode and Play Mode.
/// </summary>
[CustomEditor(typeof(GroundGridGenerator))]
public class GroundGridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default serialized fields
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        GroundGridGenerator generator = (GroundGridGenerator)target;

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("Generate Ground", GUILayout.Height(36)))
        {
            Undo.RecordObject(generator.gameObject, "Generate Ground Grid");
            generator.GenerateGround();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = new Color(0.85f, 0.35f, 0.35f);
        if (GUILayout.Button("Clear Ground", GUILayout.Height(28)))
        {
            Undo.RecordObject(generator.gameObject, "Clear Ground Grid");
            generator.ClearGround();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif
