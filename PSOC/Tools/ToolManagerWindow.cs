#if UNITY_EDITOR
// ToolManagerWindow.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class ToolManagerWindow : EditorWindow
{
    private Vector2 scrollPos;
    private bool isSyncing;
    private string statusMessage;
    private MessageType statusType;

    [MenuItem("PSOC/Tool Manager")]
    public static void ShowWindow() => GetWindow<ToolManagerWindow>("Tool Manager");

    void OnGUI()
    {
        var settings = ConnectionSettings.Instance;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Connection settings not found!", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Tool Management", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var allTools = FindAllTools();
        foreach (var tool in allTools)
        {
            DrawToolEntry(tool);
        }

        EditorGUILayout.EndScrollView();

        DrawSyncControls(allTools);
        DrawStatusArea();
    }

    private void DrawToolEntry(ToolBase tool)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.LabelField($"{tool.toolType}: {tool.toolName}", EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Data Bucket", tool.dataBucket, typeof(DataBucketConfig), false);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"ID: {(string.IsNullOrEmpty(tool.toolId) ? "Not Synced" : tool.toolId)}");
        EditorGUILayout.LabelField($"Last Sync: {DateTime.Now.ToString("HH:mm:ss")}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawSyncControls(List<ToolBase> tools)
    {
        EditorGUI.BeginDisabledGroup(isSyncing);
        {
            if (GUILayout.Button("Sync All Tools", GUILayout.Height(30)))
            {
                SyncAllTools(tools);
            }
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

    private List<ToolBase> FindAllTools()
    {
        return AssetDatabase.FindAssets($"t:{nameof(ToolBase)}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ToolBase>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
    }

    private async Task<List<string>> GetRemoteToolIds()
    {
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/tools";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Bearer {settings.apiKey}");

        var operation = request.SendWebRequest();
        while (!operation.isDone) await System.Threading.Tasks.Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Failed to fetch tools: {request.error}");
            return new List<string>();
        }

        var response = JsonConvert.DeserializeObject<List<ToolResponse>>(request.downloadHandler.text);
        return response.Select(t => t.id).ToList();
    }

    private async void SyncAllTools(List<ToolBase> tools)
    {
        isSyncing = true;
        statusMessage = "Starting sync...";
        statusType = MessageType.Info;
        Repaint();

        try
        {
            // Get remote IDs first
            var remoteIds = await GetRemoteToolIds();

            // Clear orphaned IDs
            foreach (var tool in tools)
            {
                if (!string.IsNullOrEmpty(tool.toolId) && !remoteIds.Contains(tool.toolId))
                {
                    Debug.Log($"Clearing orphaned ID for {tool.toolName}");
                    tool.toolId = string.Empty;
                    EditorUtility.SetDirty(tool);
                }
            }

            // Process sync
            foreach (var tool in tools)
            {
                try
                {
                    if (string.IsNullOrEmpty(tool.toolId))
                    {
                        statusMessage = $"Creating {tool.toolName}...";
                        await CreateTool(tool);
                    }
                    else
                    {
                        statusMessage = $"Updating {tool.toolName}...";
                        await UpdateTool(tool);
                    }

                    EditorUtility.SetDirty(tool);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Sync failed for {tool.toolName}: {e.Message}");
                }

                Repaint();
                await System.Threading.Tasks.Task.Delay(100);
            }
        }
        finally
        {
            isSyncing = false;
            statusMessage = "Sync complete!";
            statusType = MessageType.Info;
            Repaint();
        }
    }
    
      private string CreatePayloadForTool(ToolBase tool)
    {
        var settings = ConnectionSettings.Instance;
        var basePayload = new Dictionary<string, object>
        {
            ["name"] = tool.toolName,
            ["description"] = tool.description,
            ["data_directory"] = $"/storage/project_data/{settings.projectId}/{tool.dataBucket.bucketName}"
        };

        switch (tool)
        {
            case CodeLookupTool ct:
                basePayload["type"] = "code_lookup";
                basePayload["similarity_threshold"] = ct.similarityThreshold;
                basePayload["max_matches"] = 3;
                basePayload["file_extensions"] = ct.fileExtensions;
                basePayload["index_strategy"] = "semantic";
                break;

            case ExampleRetrieverTool et:
                basePayload["type"] = "example_retriever";
                basePayload["focused_fields"] = et.focusedFields;
                basePayload["similarity_top_k"] = 3;
                basePayload["include_full_json"] = true;
                break;

            case NERSmallRetrieverTool nst:
                basePayload["type"] = "ner_small_retriever";
                basePayload["choice_description"] = nst.choiceDescription;
                basePayload["focused_fields"] = nst.included_fields;
                basePayload["id_field"] = nst.idField;
                break;
            case NERLargeRetrieverTool nlt:
                basePayload["type"] = "ner_small_retriever";
                basePayload["focused_fields"] = nlt.fields_for_embedding_search;
                basePayload["focused_fields_fuzzy_search"] = nlt.fields_for_fuzzy_search;
                basePayload["id_field"] = nlt.idField;
                break;
            case SubAgent sa:
                basePayload["type"] = "agent_tool";
                basePayload["agent_id"] = sa.agent.agentId;
                break;
        }

        return Newtonsoft.Json.JsonConvert.SerializeObject(basePayload);
    }

    private string CreateUpdatePayloadForTool(ToolBase tool)
    {
        var payload = new Dictionary<string, object>
        {
            ["id"] = tool.toolId,
            ["name"] = tool.toolName,
            ["description"] = tool.description
        };

        switch (tool)
        {
            case CodeLookupTool ct:
                payload.Add("similarity_threshold", ct.similarityThreshold);
                payload.Add("max_matches", ct.maxMatches);
                payload.Add("file_extensions", ct.fileExtensions);
                payload.Add("index_strategy", ct.indexStrategy);
                break;

            case ExampleRetrieverTool et:
                payload.Add("focused_fields", et.focusedFields);
                payload.Add("similarity_top_k", et.similarityTopK);
                payload.Add("include_full_json", et.includeFullJson);
                break;

            case NERSmallRetrieverTool nst:
                payload["focused_fields"] = nst.included_fields;
                payload["choice_description"] = nst.choiceDescription;
                payload["id_field"] = nst.idField;
                break;
            case NERLargeRetrieverTool nlt:
                payload["focused_fields"] = nlt.fields_for_embedding_search;
                payload["focused_fields_fuzzy_search"] = nlt.fields_for_fuzzy_search;
                payload["id_field"] = nlt.idField;
                break;
            case SubAgent sa:
                payload["type"] = "agent_tool";
                payload["agent_id"] = sa.agent.agentId;
                break;
        }

        return Newtonsoft.Json.JsonConvert.SerializeObject(payload);
    }

    private System.Threading.Tasks.Task CreateTool(ToolBase tool)
    {
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/tools";

        var payload = CreatePayloadForTool(tool);
        return SendToolRequest(url, "POST", payload, tool);
    }

    private System.Threading.Tasks.Task UpdateTool(ToolBase tool)
    {
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/tools/{tool.toolId}";

        var payload = CreateUpdatePayloadForTool(tool);
        return SendToolRequest(url, "PATCH", payload, tool);
    }

    private System.Threading.Tasks.Task SendToolRequest(string url, string method, string json, ToolBase tool)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

        var request = new UnityWebRequest(url, method);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        //request.SetRequestHeader("Authorization", $"Bearer {ConnectionSettings.Instance.apiKey}");

        var operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Tool sync error: {request.downloadHandler.text}");
                tcs.SetException(new Exception(request.error));
                return;
            }

            var response = JsonUtility.FromJson<ToolResponse>(request.downloadHandler.text);
            tool.toolId = response.id;
            EditorUtility.SetDirty(tool);
            tcs.SetResult(true);
            request.Dispose();
        };

        return tcs.Task;
    }

  

    [System.Serializable]
    private class ToolResponse
    {
        public string id;
    }
}
#endif