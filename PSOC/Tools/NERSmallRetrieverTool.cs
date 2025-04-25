using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NERSmallRetriever", menuName = "PSOC/Tools/NER Small Retriever")]
public class NERSmallRetrieverTool : ToolBase
{
    [Header("NER Settings")] 
    public string choiceDescription;
    public string[] included_fields;
    public string idField = "guid";
    
}