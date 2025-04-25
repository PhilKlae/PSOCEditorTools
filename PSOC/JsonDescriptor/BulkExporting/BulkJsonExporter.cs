using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Scripts.JsonDescriptor
{
    [CreateAssetMenu(fileName = "BulkExporter", menuName = "PSOC/BulkExporter")]
    public class BulkJsonExporter : ScriptableObject
    {
        [Header("Settings")]
        public string exportPath = "Assets/LLamaIndex/AbilityTemplates";
        public string sourceRootPath = "Assets/ScriptableObjects/Abilities";
        
        /// <summary>
        /// used to retrieve all objects of this type from the project
        /// </summary>
        public string ClassName;
        
        [ContextMenu("Export JSON Templates")]
        public void ExportAllTemplates()
        {

            // Ensure export root exists
            CreateExportRootDirectory();

            var objectEnumerator = GetAllScriptableObjects(ClassName);

            while (objectEnumerator.MoveNext())
            {
                ExportTemplate(objectEnumerator.Current);
            }

            AssetDatabase.Refresh();
        }
        
        /// <summary>
        /// 
        /// </summary>
        private void OnValidate()
        {
            // set the source root path to the current project path relative to the project assets folder root
            if (string.IsNullOrEmpty(sourceRootPath))
            {
                sourceRootPath = Application.dataPath;
            }
            else
            {
                sourceRootPath = Path.GetFullPath(sourceRootPath);
            }
        }

        /// <summary>
        /// uses unity editor utility to get all scriptable objects of a given type
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private IEnumerator<ScriptableObject> GetAllScriptableObjects(string className)
        {

            string[] guids = AssetDatabase.FindAssets($"t:{className}");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                yield return obj;
            }
        }

        void CreateExportRootDirectory()
        {
            string systemExportPath = Path.Combine(Application.dataPath, exportPath.Substring("Assets/".Length));
            if (!Directory.Exists(systemExportPath))
            {
                Directory.CreateDirectory(systemExportPath);
                AssetDatabase.Refresh();
            }
        }

        void ExportTemplate(UnityEngine.Object targetObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(targetObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"Skipping {targetObject.name} - not an asset");
                return;
            }

            // Normalize paths for comparison
            string normalizedSourceRoot = sourceRootPath.Replace('\\', '/').TrimEnd('/') + '/';
            string normalizedAssetPath = assetPath.Replace('\\', '/');

            if (!normalizedAssetPath.StartsWith(normalizedSourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Skipping {targetObject.name} - not under source root");
                return;
            }

            // Calculate relative path
            string relativePath = normalizedAssetPath.Substring(normalizedSourceRoot.Length);
            string targetFilePath = Path.Combine(exportPath, relativePath);
            targetFilePath = Path.ChangeExtension(targetFilePath, ".json");

            // Create directory structure
            string targetDirectory = Path.GetDirectoryName(targetFilePath);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Generate JSON
            HashSet<string> ignoredKeys = new HashSet<string> { "hideFlags" };
            string json = JsonObjectDescriptor.DescribeObject(targetObject, maxDepth: 10, ignoredKeys);

            // Write to file
            File.WriteAllText(targetFilePath, json);
            Debug.Log($"Exported {targetObject.name} to {targetFilePath}");
        }
    }
}