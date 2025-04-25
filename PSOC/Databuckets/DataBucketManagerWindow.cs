#if UNITY_EDITOR
// DataBucketManagerWindow.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System;

public class DataBucketManagerWindow : EditorWindow
{
    private Vector2 scrollPos;
    private bool isSyncing;
    private string statusMessage;
    private MessageType statusType;
    private List<DataBucketConfig> allBuckets = new List<DataBucketConfig>();

    [MenuItem("PSOC/Data Bucket Manager")]
    public static void ShowWindow() => GetWindow<DataBucketManagerWindow>("Data Bucket Manager");

    void OnGUI()
    {
        var settings = ConnectionSettings.Instance;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Connection settings not found!", MessageType.Error);
            return;
        }

        RefreshBucketList();
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Data Bucket Management", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        foreach (var bucket in allBuckets)
        {
            DrawBucketEntry(bucket);
        }
        
        EditorGUILayout.EndScrollView();

        DrawSyncControls();
        DrawStatusArea();
    }

    private void RefreshBucketList()
    {
        allBuckets = AssetDatabase.FindAssets($"t:{nameof(DataBucketConfig)}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<DataBucketConfig>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
    }

    private void DrawBucketEntry(DataBucketConfig bucket)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.LabelField(bucket.bucketName, EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Source Folder", bucket.sourceFolder, typeof(DefaultAsset), false);
        EditorGUILayout.LabelField($"Files: {CountValidFiles(bucket)}");
        
        EditorGUILayout.EndVertical();
    }

    private int CountValidFiles(DataBucketConfig bucket)
    {
        if (bucket.sourceFolder == null) return 0;
        var path = AssetDatabase.GetAssetPath(bucket.sourceFolder);
        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Count(f => !f.EndsWith(".meta"));
    }

    private void DrawSyncControls()
    {
        EditorGUI.BeginDisabledGroup(isSyncing);
        if (GUILayout.Button("Sync All Buckets", GUILayout.Height(30)))
        {
            SyncAllBuckets();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawStatusArea()
    {
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    private async void SyncAllBuckets()
    {
        if (isSyncing) return;

        var settings = ConnectionSettings.Instance;
        if (!settings)
        {
            statusMessage = "No connection settings found!";
            statusType = MessageType.Error;
            return;
        }
        
        isSyncing = true;
        statusMessage = "Starting bucket sync...";
        Repaint();

        foreach (var bucket in allBuckets)
        {
            if (bucket.sourceFolder == null)
            {
                Debug.LogWarning($"Skipping {bucket.bucketName} - no folder selected");
                continue;
            }

            try
            {
                statusMessage = $"Syncing {bucket.bucketName}...";
                await SyncBucket(bucket);
                EditorUtility.SetDirty(bucket);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to sync {bucket.bucketName}: {e.Message}");
            }
            
            Repaint();
            await System.Threading.Tasks.Task.Delay(100);
        }

        isSyncing = false;
        statusMessage = "Bucket sync complete!";
        Repaint();
    }

    private System.Threading.Tasks.Task SyncBucket(DataBucketConfig config)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/data/sync/" +
                $"{settings.projectId}/{config.bucketName}";
        
        var allFiles = Directory.GetFiles(config.FolderPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".meta") && !ShouldSkipFile(f))
            .ToList();
        
        if (allFiles.Count == 0)
        {
            statusMessage = "No files found to upload!";
            statusType = MessageType.Error;
            return tcs.Task;
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

        var request = UnityWebRequest.Post(url, formData);
  
        var operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Sync failed: {request.error}\n{request.downloadHandler.text}");
                tcs.SetException(new Exception(request.error));
            }
            else
            {
                Debug.Log($"Successfully synced {config.bucketName}");
                tcs.SetResult(true);
            }
            request.Dispose();
        };

        return tcs.Task;
    }

    private bool ShouldSkipFile(string path)
    {
        return Path.GetFileName(path).StartsWith(".") ||
               (File.GetAttributes(path) & FileAttributes.Hidden) == FileAttributes.Hidden;
    }

    private string GetRelativePath(string rootPath, string fullPath)
    {
        var rootUri = new Uri(rootPath + Path.DirectorySeparatorChar);
        var fileUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
    }
}
#endif