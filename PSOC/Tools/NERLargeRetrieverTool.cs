using UnityEngine;

[CreateAssetMenu(fileName = "NERLargeRetriever", menuName = "PSOC/Tools/NER Large Retriever")]
public class NERLargeRetrieverTool : ToolBase
{
    [Header("NER Settings")] 
    public string[] fields_for_fuzzy_search;
    public string[] fields_for_embedding_search;
    public string idField = "guid";
}