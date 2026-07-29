using UnityEngine;
using UnityEditor;
using System.IO;

public class CityScapeOptimizer : EditorWindow
{
    [MenuItem("Tools/CityScape Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<CityScapeOptimizer>("CityScape Optimizer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Project Optimization Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Optimize All Textures"))
        {
            OptimizeTextures();
        }

        if (GUILayout.Button("2. Optimize All Models"))
        {
            OptimizeModels();
        }

        if (GUILayout.Button("3. Enable GPU Instancing (Materials)"))
        {
            OptimizeMaterials();
        }

        if (GUILayout.Button("4. Auto-Set Static Flags (Scene Objects)"))
        {
            OptimizeSceneObjects();
        }
        
        if (GUILayout.Button("5. Adjust Quality Settings (Shadows)"))
        {
            OptimizeQualitySettings();
        }
        
        GUILayout.Space(20);
        if (GUILayout.Button("Run ALL Optimizations", GUILayout.Height(40)))
        {
            OptimizeTextures();
            OptimizeModels();
            OptimizeMaterials();
            OptimizeSceneObjects();
            OptimizeQualitySettings();
            Debug.Log("All optimizations applied!");
        }
    }

    private static void OptimizeTextures()
    {
        string[] textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int count = 0;
        
        try
        {
            for (int i = 0; i < textureGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGUIDs[i]);
                
                // Skip UI or Editor specific folders
                if (path.Contains("Packages") || path.Contains("Editor") || path.Contains("UI"))
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;

                    if (importer.textureType == TextureImporterType.Default)
                    {
                        if (importer.maxTextureSize > 1024)
                        {
                            importer.maxTextureSize = 1024;
                            changed = true;
                        }

                        if (importer.isReadable)
                        {
                            importer.isReadable = false;
                            changed = true;
                        }

                        if (importer.textureCompression != TextureImporterCompression.Compressed)
                        {
                            importer.textureCompression = TextureImporterCompression.Compressed;
                            changed = true;
                        }
                        
                        if (importer.crunchedCompression == false)
                        {
                            importer.crunchedCompression = true;
                            importer.compressionQuality = 50;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        count++;
                    }
                }
                EditorUtility.DisplayProgressBar("Optimizing Textures", path, (float)i / textureGUIDs.Length);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Debug.Log($"Optimized {count} Textures.");
        }
    }

    private static void OptimizeModels()
    {
        string[] modelGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
        int count = 0;

        try
        {
            for (int i = 0; i < modelGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGUIDs[i]);
                if (path.Contains("Packages")) continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;

                    if (importer.isReadable)
                    {
                        importer.isReadable = false;
                        changed = true;
                    }

                    if (importer.meshCompression == ModelImporterMeshCompression.Off)
                    {
                        importer.meshCompression = ModelImporterMeshCompression.Medium;
                        changed = true;
                    }
                    
                    if (importer.optimizeMeshVertices == false)
                    {
                        importer.optimizeMeshVertices = true;
                        changed = true;
                    }
                    if (importer.optimizeMeshPolygons == false)
                    {
                        importer.optimizeMeshPolygons = true;
                        changed = true;
                    }

                    if (changed)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        count++;
                    }
                }
                EditorUtility.DisplayProgressBar("Optimizing Models", path, (float)i / modelGUIDs.Length);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Debug.Log($"Optimized {count} Models.");
        }
    }

    private static void OptimizeMaterials()
    {
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int count = 0;
        
        try
        {
            for (int i = 0; i < materialGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGUIDs[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
                EditorUtility.DisplayProgressBar("Optimizing Materials", path, (float)i / materialGUIDs.Length);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Debug.Log($"Enabled GPU Instancing on {count} Materials.");
        }
    }

    private static void OptimizeSceneObjects()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.GetComponent<MeshRenderer>() != null)
            {
                // Exclude characters, NPCs, UI, and animated objects
                if (!obj.CompareTag("Player") && 
                    !obj.CompareTag("MainCamera") &&
                    !obj.name.ToLower().Contains("npc") &&
                    !obj.name.ToLower().Contains("player") &&
                    obj.GetComponent<Animator>() == null &&
                    obj.GetComponent<Animation>() == null &&
                    obj.GetComponent<Rigidbody>() == null)
                {
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);
                    
                    StaticEditorFlags newFlags = flags | 
                        StaticEditorFlags.BatchingStatic | 
                        StaticEditorFlags.ContributeGI | 
                        StaticEditorFlags.ReflectionProbeStatic | 
                        StaticEditorFlags.OccludeeStatic | 
                        StaticEditorFlags.OccluderStatic;

                    if (flags != newFlags)
                    {
                        GameObjectUtility.SetStaticEditorFlags(obj, newFlags);
                        count++;
                    }
                }
            }
        }
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"Applied Static Flags to {count} Scene Objects.");
    }
    
    private static void OptimizeQualitySettings()
    {
        QualitySettings.shadowDistance = 75f;
        QualitySettings.shadowCascades = 2;
        
        Debug.Log("Adjusted Quality Settings: Shadow Distance = 75, Cascades = 2.");
    }
}
