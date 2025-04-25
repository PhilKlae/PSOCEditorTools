#if UNITY_EDITOR
// DataBucketConfigEditor.cs
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(DataBucketConfig))]
public class DataBucketConfigEditor : Editor
{
    private bool isSyncing;
    private string statusMessage;
    private MessageType statusType;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var config = (DataBucketConfig)target;
        var settings = ConnectionSettings.Instance;

        EditorGUILayout.Space(20);
        
        // Folder validation
        if (!config.sourceFolder)
        {
            EditorGUILayout.HelpBox("No source folder selected!", MessageType.Error);
            return;
        }

        if (!Directory.Exists(config.FolderPath))
        {
            EditorGUILayout.HelpBox("Selected folder doesn't exist!", MessageType.Error);
            return;
        }

        // Sync controls
        EditorGUI.BeginDisabledGroup(isSyncing || !settings);
        {
            if (GUILayout.Button("Sync Bucket", GUILayout.Height(30)))
            {
                SyncBucket(config);
            }
        }
        EditorGUI.EndDisabledGroup();

        // Status display
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        if (isSyncing)
        {
            EditorGUILayout.LabelField("Syncing...");
            Repaint();
        }
    }

    private void SyncBucket(DataBucketConfig config)
    {
        if (isSyncing) return;
        
        var settings = ConnectionSettings.Instance;
        if (!settings)
        {
            statusMessage = "No connection settings found!";
            statusType = MessageType.Error;
            return;
        }

        string url = $"http://{settings.serverIP}:{settings.serverPort}/v1/data/sync/" +
                    $"{settings.projectId}/{config.bucketName}";
        
        // Get all files recursively
        var allFiles = Directory.GetFiles(config.FolderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".meta"))
            .ToList();

        if (allFiles.Count == 0)
        {
            statusMessage = "No files found to upload!";
            statusType = MessageType.Error;
            return;
        }

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
    
        foreach (var filePath in allFiles)
        {
            // Skip Unity meta files and hidden files
            if (ShouldSkipFile(filePath)) continue;
        
            try 
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                string relativePath = GetRelativePath(config.FolderPath, filePath);
            
                formData.Add(new MultipartFormFileSection(
                    "files",
                    fileData,
                    relativePath,  // Preserve directory structure
                    "application/octet-stream"
                ));
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read {filePath}: {e}");
            }
        }


        UnityWebRequest request = UnityWebRequest.Post(url, formData);
      
        var operation = request.SendWebRequest(); 
        isSyncing = true;
        statusMessage = "Starting sync...";
        statusType = MessageType.Info;

        operation.completed += (asyncOp) =>
        {
            isSyncing = false;
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                statusMessage = $"Sync failed: {request.error}";
                statusType = MessageType.Error;
                Debug.LogError($"Sync error: {request.downloadHandler.text}");
            }
            else
            {
                statusMessage = "Sync completed successfully!";
                statusType = MessageType.Info;
                Debug.Log($"Sync response: {request.downloadHandler.text}");
            }
            
            request.Dispose();
            Repaint();
        };
    }
    
    private bool ShouldSkipFile(string filePath)
    {
        return Path.GetFileName(filePath).StartsWith(".") || // Unix hidden files
               filePath.EndsWith(".meta") ||                 // Unity meta files
               (File.GetAttributes(filePath) & FileAttributes.Hidden) == FileAttributes.Hidden;
    }

    private string GetRelativePath(string rootPath, string fullPath)
    {
        var rootUri = new Uri(rootPath + Path.DirectorySeparatorChar);
        var fileUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
    }
}
#endif