// ConnectionSettings.cs

using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ConnectionSettings", menuName = "PSOC/Connection Settings")]
public class ConnectionSettings : ScriptableObject
{
    [Header("Server Configuration")]
    public string serverIP = "localhost";
    public int serverPort = 80;
    public string apiKey = "";
    
    [Header("Project Identity")]
    public string projectId = "defaultproject";
    
    [Header("Authentication")]
    public string username = "";
    public string password = "";
    
    [SerializeField, TextArea] 
    private string _notes = "";

    // Singleton pattern
    private static ConnectionSettings _instance;
    public static ConnectionSettings Instance
    {
        get
        {
            if (!_instance)
            {
                var guids = AssetDatabase.FindAssets("t:ConnectionSettings");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<ConnectionSettings>(path);
                }
            }
            return _instance;
        }
    }
}