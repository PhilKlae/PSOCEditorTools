// AgentBase.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEditor;

[CreateAssetMenu(fileName = "Agent", menuName = "PSOC/Agents/Base Agent")]
public class AgentBase : ScriptableObject
{
    [Header("Identification")]
    public string agentId;
    public string agentName;
    [TextArea] public string description;
    
    [Header("Configuration")]
    public List<ToolBase> tools;
    public LLMConfig llmConfig = new LLMConfig();

    [Header("Execution Settings")]
    [Range(1, 100)] public int maxIterations = 10;
    public bool verbose;
    
    [Header("Status")]
    public string status;
    public DateTime lastUpdated;
    
    /// <summary>
    /// A .md file containing the default prompt with instructions specific for this agent, use the keyword $USER_PROMPT$
    /// to specify the words that will be replaced with the users query. This default prompt will contain step by step instructions
    /// or similar agent instructions that will be mixed with a user request
    /// </summary>
    [Header("Default Prompt")]
    [SerializeField] public TextAsset promptTextFile;
    
    // this method combines the user query with the default prompt text
    public string GetPromptText(string userQuery)
    {
        if (promptTextFile == null)
        {
            return "";
        }
        
        var defaultPrompt = promptTextFile.text;
        
        return defaultPrompt.Replace("$USER_PROMPT$", userQuery);
    }
}

[Serializable]
public class LLMConfig
{
    public string modelName = "gpt-4";
    [Range(0, 1)] public float temperature = 0.7f;
    public int maxTokens = 1000;
}
