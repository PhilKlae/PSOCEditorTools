// ToolBase.cs
using UnityEngine;

public enum ToolType { CodeLookup, ExampleRetriever, NERRetriever, NERLargeRetriever, NERSmallRetriever, SubAgent }

public abstract class ToolBase : ScriptableObject
{
    [Header("Base Configuration")]
    public string toolId;
    public string toolName;
    [TextArea] public string description;
    public DataBucketConfig dataBucket;
    public ToolType toolType;
    
    [Header("Status")]
    public string status;
    public double createdAt;
    public bool isConfigured;
}