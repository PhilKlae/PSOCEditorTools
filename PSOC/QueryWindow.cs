#if UNITY_EDITOR
// QueryEditorWindow.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;
using UnityEditor.Scripting.Python;

public class QueryEditorWindow : EditorWindow
{
    private AgentBase selectedAgent;
    private string queryText = "";
    private string generatedCode = "";
    private string additionalNotes = "";
    private Vector2 codeScroll;
    private Vector2 notesScroll;
    private bool isProcessing;

    [MenuItem("PSOC/Query Editor")]
    public static void ShowWindow() => GetWindow<QueryEditorWindow>("Query Editor");

    void OnGUI()
    {
        DrawAgentSelection();
        DrawQueryInput();
        DrawCodeOutput();
        DrawAdditionalNotes();
    }

    private void DrawAgentSelection()
    {
        EditorGUILayout.LabelField("Agent Configuration", EditorStyles.boldLabel);
        selectedAgent = (AgentBase)EditorGUILayout.ObjectField(
            "Selected Agent", 
            selectedAgent, 
            typeof(AgentBase), 
            false
        );
    }

    private void DrawQueryInput()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Query Input", EditorStyles.boldLabel);
        queryText = EditorGUILayout.TextArea(queryText, GUILayout.Height(100));
        
        EditorGUI.BeginDisabledGroup(selectedAgent == null || string.IsNullOrEmpty(queryText) || isProcessing);
        if (GUILayout.Button("Send Query", GUILayout.Height(30)))
        {
            ProcessQuery();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawCodeOutput()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Generated Code", EditorStyles.boldLabel);
        
        codeScroll = EditorGUILayout.BeginScrollView(codeScroll, GUILayout.Height(200));
        generatedCode = EditorGUILayout.TextArea(generatedCode, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(generatedCode));
        if (GUILayout.Button("Execute Code", GUILayout.Height(30)))
        {
            ExecuteGeneratedCode();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawAdditionalNotes()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Additional Notes", EditorStyles.boldLabel);
        notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.Height(100));
        additionalNotes = EditorGUILayout.TextArea(additionalNotes, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private async void ProcessQuery()
    {
        if (selectedAgent == null || string.IsNullOrEmpty(selectedAgent.agentId))
        {
            Debug.LogError("No valid agent selected!");
            return;
        }

        isProcessing = true;
        var settings = ConnectionSettings.Instance;
        var url = $"http://{settings.serverIP}:{settings.serverPort}/v1/query";

        try
        {
            var requestData = new QueryRequest
            {
                agent_id = selectedAgent.agentId,
                query = selectedAgent.GetPromptText(queryText),
                max_tokens = 300
            };

            var json = JsonConvert.SerializeObject(requestData);
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await System.Threading.Tasks.Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Query failed: {request.error}");
                return;
            }

            var response = JsonConvert.DeserializeObject<CodeGenerationResponse>(request.downloadHandler.text);
            generatedCode = response.executable_unity_python_code;
            additionalNotes = string.Join("\n", response.notes);
            var inferenceTime = response.execution_time;
            
            // prepend additional notes with exection time
            additionalNotes = $"(inference took : {inferenceTime} seconds) \n {additionalNotes}";
        }
        catch (Exception e)
        {
            Debug.LogError($"Query processing error: {e.Message}");
        }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }

    private void ExecuteGeneratedCode()
    {
        if (string.IsNullOrEmpty(generatedCode))
        {
            Debug.LogWarning("No code to execute!");
            return;
        }

        try
        {
            PythonRunner.RunString(generatedCode);
            Debug.Log("Code executed successfully!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Code execution failed: {e.Message}");
        }
    }

    [System.Serializable]
    private class QueryRequest
    {
        public string agent_id;
        public string query;
        public int max_tokens;
    }

    [System.Serializable]
    private class CodeGenerationResponse
    {
        public float execution_time;
        public string notes;
        public string executable_unity_python_code;
    }
  

    [System.Serializable]
    private class ToolCall
    {
        public string tool_name;
        public string[] parameters;
    }
}
#endif