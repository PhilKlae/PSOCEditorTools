using UnityEngine;

[CreateAssetMenu(fileName = "ExampleRetriever", menuName = "PSOC/Tools/Example Retriever")]
public class ExampleRetrieverTool : ToolBase
{
    [Header("Example Retriever Settings")]
    public string[] focusedFields;
    public int similarityTopK = 3;
    public bool includeFullJson = true;
}