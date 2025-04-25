// ConnectionSettingsWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ConnectionSettingsWindow : EditorWindow
{
    [MenuItem("PSOC/Connection Settings")]
    public static void ShowWindow()
    {
        var settings = FindOrCreateSettings();
        Selection.activeObject = settings;
        EditorUtility.FocusProjectWindow();
    }

    private static ConnectionSettings FindOrCreateSettings()
    {
        var guids = AssetDatabase.FindAssets("t:ConnectionSettings");
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<ConnectionSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        var settings = ScriptableObject.CreateInstance<ConnectionSettings>();
        AssetDatabase.CreateAsset(settings, "Assets/ConnectionSettings.asset");
        AssetDatabase.SaveAssets();
        return settings;
    }
}
#endif