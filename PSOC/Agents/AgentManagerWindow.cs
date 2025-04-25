#if UNITY_EDITOR
// AgentManagerWindow.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;

public class AgentManagerWindow : EditorWindow
{
    private Vector2 scrollPos;
    private bool isSyncing;
    private string statusMessage;
    private MessageType statusType;

    [MenuItem("PSOC/Agent Manager")]
    public static void ShowWindow() => GetWindow<AgentManagerWindow>("Agent Manager");

    void OnGUI()
    {
        var settings = ConnectionSettings.Instance;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Connection settings not found!", MessageType.Error);
            return;
        }

        DrawHeader();
        DrawAgentList();
        DrawSyncControls();
        DrawStatusArea();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Agent Management", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawAgentList()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        foreach (var agent in FindAllAgents())
        {
            DrawAgentEntry(agent);
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawAgentEntry(AgentBase agent)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.LabelField(agent.agentName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ID: {agent.agentId}");
        EditorGUILayout.LabelField($"Last Updated: {agent.lastUpdated:yyyy-MM-dd HH:mm:ss}");
        
        EditorGUILayout.EndVertical();
    }

    private void DrawSyncControls()
    {
        EditorGUI.BeginDisabledGroup(isSyncing);
        if (GUILayout.Button("Sync All Agents", GUILayout.Height(30)))
        {
            SyncAllAgents();
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

    private List<AgentBase> FindAllAgents()
    {
        return AssetDatabase.FindAssets($"t:{nameof(AgentBase)}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<AgentBase>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
    }

    // Add this method to fetch remote IDs
private async Task<List<string>> GetRemoteAgentIds()
{
    var settings = ConnectionSettings.Instance;
    var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/agents";
    
    using var request = UnityWebRequest.Get(url);
    request.SetRequestHeader("Authorization", $"Bearer {settings.apiKey}");
    
    var operation = request.SendWebRequest();
    while (!operation.isDone) await System.Threading.Tasks.Task.Yield();

    if (request.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError($"Failed to fetch agents: {request.error}");
        return new List<string>();
    }

    return JsonConvert.DeserializeObject<List<string>>(request.downloadHandler.text);
}

// Updated SyncAllAgents method
private async void SyncAllAgents()
{
    isSyncing = true;
    statusMessage = "Starting agent sync...";
    Repaint();

    try
    {
        // Get remote IDs first
        var remoteIds = await GetRemoteAgentIds();
        var localAgents = FindAllAgents();

        // Clear orphaned IDs
        foreach (var agent in localAgents)
        {
            if (!string.IsNullOrEmpty(agent.agentId) && !remoteIds.Contains(agent.agentId))
            {
                Debug.Log($"Clearing orphaned ID for {agent.agentName}");
                agent.agentId = string.Empty;
                EditorUtility.SetDirty(agent);
            }
        }

        // Process sync
        foreach (var agent in localAgents)
        {
            if (string.IsNullOrEmpty(agent.agentId))
            {
                statusMessage = $"Creating {agent.agentName}...";
                await CreateAgent(agent);
            }
            else
            {
                statusMessage = $"Updating {agent.agentName}...";
                await UpdateAgent(agent);
            }
            
            agent.lastUpdated = DateTime.Now;
            EditorUtility.SetDirty(agent);
            Repaint();
            await System.Threading.Tasks.Task.Delay(100);
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"Sync failed: {e}");
    }
    finally
    {
        isSyncing = false;
        statusMessage = "Agent sync completed!";
        Repaint();
    }
}

    private System.Threading.Tasks.Task CreateAgent(AgentBase agent)
    {
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/agents";
        var payload = CreateAgentPayload(agent);
        
        return SendAgentRequest(url, "POST", payload, agent);
    }

    private System.Threading.Tasks.Task UpdateAgent(AgentBase agent)
    {
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/agents/{agent.agentId}";
        var payload = CreateAgentPayload(agent);
        
        return SendAgentRequest(url, "PUT", payload, agent);
    }

    private System.Threading.Tasks.Task SendAgentRequest(string url, string method, string json, AgentBase agent)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        
        var request = new UnityWebRequest(url, method);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
     
        var operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                tcs.SetException(new Exception(request.error));
                return;
            }

            var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
            if (response.ContainsKey("id"))
            {
                agent.agentId = response["id"].ToString();
                agent.status = response["status"].ToString();
            }
            
            tcs.SetResult(true);
            request.Dispose();
        };

        return tcs.Task;
    }

    private string CreateAgentPayload(AgentBase agent)
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            name = agent.agentName,
            description = agent.description,
            tools = agent.tools.ConvertAll((inputTool => inputTool.toolId)),
            llm_config = new
            {
                model_name = agent.llmConfig.modelName,
                temperature = agent.llmConfig.temperature,
                max_tokens = agent.llmConfig.maxTokens
            },
            max_iterations = agent.maxIterations,
            verbose = agent.verbose
        });
    }
}
#endif