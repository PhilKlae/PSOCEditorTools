using UnityEngine;

[CreateAssetMenu(fileName = "CodeLookup", menuName = "PSOC/Tools/Code Lookup")]
public class CodeLookupTool : ToolBase
{
    [Header("Code Lookup Settings")]
    [Range(0, 1)] public float similarityThreshold = 0.8f;
    public int maxMatches = 3;
    public string[] fileExtensions = { ".cs" };
    public string indexStrategy = "semantic";
}