using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System.IO;

public class ScriptableObjectJsonViewer : EditorWindow
{
    private ScriptableObject targetScriptableObject;
    private string jsonOutput = "";
    private Vector2 scrollPosition;

    [MenuItem("Tools/ScriptableObject JSON Viewer")]
    public static void ShowWindow()
    {
        GetWindow<ScriptableObjectJsonViewer>("SO JSON Viewer");
    }

    private void OnGUI()
    {
        GUILayout.Label("ScriptableObject JSON Viewer", EditorStyles.boldLabel);
        
        // Object field to select a ScriptableObject
        targetScriptableObject = (ScriptableObject)EditorGUILayout.ObjectField("Target ScriptableObject", targetScriptableObject, typeof(ScriptableObject), false);

        // Generate JSON Button
        if (GUILayout.Button("Generate JSON") && targetScriptableObject != null)
        {
            HashSet<string> ignoredKeys = new HashSet<string>();
            ignoredKeys.Add("hideFlags");
            jsonOutput = JsonObjectDescriptor.DescribeObject(targetScriptableObject, maxDepth: 10, ignoredKeys);
        }

        // Copy to Clipboard Button
        if (!string.IsNullOrEmpty(jsonOutput) && GUILayout.Button("Copy JSON to Clipboard"))
        {
            EditorGUIUtility.systemCopyBuffer = jsonOutput;
        }

        // Scrollable Text Area
        if (!string.IsNullOrEmpty(jsonOutput))
        {
            GUILayout.Label("Generated JSON:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            EditorGUILayout.TextArea(jsonOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}