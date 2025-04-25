using UnityEngine;

[CreateAssetMenu(fileName = "NERRetriever", menuName = "PSOC/Tools/NER Retriever")]
public class NERRetrieverTool : ToolBase
{
    [Header("NER Settings")]
    public string[] entityTypes;
    public string idField = "guid";
}