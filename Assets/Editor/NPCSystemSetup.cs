using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CityScape.ExploreMode;
using CityScape.Managers;
using UnityEngine.UI;
using TMPro;

public class NPCSystemSetup : EditorWindow
{
    [MenuItem("CityScape/Setup NPC System")]
    public static void SetupSystem()
    {
        // 1. Create NPC Prefab
        string prefabPath = "Assets/Prefab/NPC_Capsule.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            GameObject npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObj.name = "NPC_Capsule";
            npcObj.transform.position = Vector3.zero;
            
            // Adjust primitive components
            Collider col = npcObj.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            
            // Add scripts
            var controller = npcObj.AddComponent<NPCController>();
            var interaction = npcObj.AddComponent<NPCInteraction>();
            
            // Set up World Space UI
            GameObject uiObj = new GameObject("NPC_UI");
            uiObj.transform.SetParent(npcObj.transform, false);
            uiObj.transform.localPosition = new Vector3(0, 1.5f, 0); // Above head
            
            var canvas = uiObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
            canvas.GetComponent<RectTransform>().localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            uiObj.AddComponent<CanvasScaler>();
            uiObj.AddComponent<GraphicRaycaster>();
            
            var dialogueUI = uiObj.AddComponent<NPCDialogueUI>();
            
            // Prompt Group
            GameObject promptGroup = new GameObject("PromptGroup");
            promptGroup.transform.SetParent(uiObj.transform, false);
            var promptBg = promptGroup.AddComponent<Image>();
            promptBg.color = new Color(0, 0, 0, 0.7f);
            
            GameObject promptTextObj = new GameObject("Text");
            promptTextObj.transform.SetParent(promptGroup.transform, false);
            var promptText = promptTextObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "Press E to Talk";
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 24;
            
            // Dialogue Group
            GameObject dialogueGroup = new GameObject("DialogueGroup");
            dialogueGroup.transform.SetParent(uiObj.transform, false);
            var diagBg = dialogueGroup.AddComponent<Image>();
            diagBg.color = new Color(1, 1, 1, 0.9f);
            
            GameObject diagTextObj = new GameObject("Text");
            diagTextObj.transform.SetParent(dialogueGroup.transform, false);
            var diagText = diagTextObj.AddComponent<TextMeshProUGUI>();
            diagText.text = "Hi!";
            diagText.color = Color.black;
            diagText.alignment = TextAlignmentOptions.Center;
            diagText.fontSize = 24;
            
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(dialogueGroup.transform, false);
            closeBtnObj.transform.localPosition = new Vector3(0, -30, 0);
            var closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = Color.red;
            var closeBtn = closeBtnObj.AddComponent<Button>();
            
            GameObject closeTxtObj = new GameObject("Text");
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
            closeTxt.text = "Close";
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.fontSize = 16;
            
            // Hook up references in NPCDialogueUI (reflection/serialized object not strictly needed since we are making a prefab from this live object, but let's just make it public or use SerializedObject)
            SerializedObject so = new SerializedObject(dialogueUI);
            so.FindProperty("interactionPromptGroup").objectReferenceValue = promptGroup;
            so.FindProperty("dialogueGroup").objectReferenceValue = dialogueGroup;
            so.FindProperty("dialogueText").objectReferenceValue = diagText;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();
            
            SerializedObject soInteraction = new SerializedObject(interaction);
            soInteraction.FindProperty("dialogueUI").objectReferenceValue = dialogueUI;
            soInteraction.ApplyModifiedProperties();
            
            // Save as Prefab
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefab");
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(npcObj, prefabPath);
            DestroyImmediate(npcObj);
            
            Debug.Log("Created NPC Prefab at " + prefabPath);
        }

        // 2. Setup NPCManager in Scene
        NPCManager manager = Object.FindFirstObjectByType<NPCManager>();
        if (manager == null)
        {
            // Find Managers object
            GameObject managersObj = GameObject.Find("Managers");
            if (managersObj == null)
            {
                managersObj = new GameObject("Managers");
            }
            manager = managersObj.AddComponent<NPCManager>();
            
            SerializedObject soManager = new SerializedObject(manager);
            soManager.FindProperty("npcPrefab").objectReferenceValue = prefab;
            soManager.ApplyModifiedProperties();
            
            Debug.Log("Added NPCManager to Scene under " + managersObj.name);
        }
        else
        {
            SerializedObject soManager = new SerializedObject(manager);
            if (soManager.FindProperty("npcPrefab").objectReferenceValue == null)
            {
                soManager.FindProperty("npcPrefab").objectReferenceValue = prefab;
                soManager.ApplyModifiedProperties();
                Debug.Log("Updated NPCManager prefab reference.");
            }
        }
        
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("NPC System setup complete!");
    }
}
