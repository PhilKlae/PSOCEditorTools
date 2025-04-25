using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class JsonDescriptionIgnore : Attribute {}

public static class JsonObjectDescriptor
{
    public static string DescribeObject(object obj, int maxDepth = 3, HashSet<string> excludedFields = null, bool excludeGetOnlyProperties = true)
    {
        if (obj == null) return "{}";

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None
        };

        JObject jsonObj = DescribeObjectRecursive(obj, new HashSet<object>(), maxDepth, excludedFields ?? new HashSet<string>(), excludeGetOnlyProperties);
        return jsonObj.ToString(Formatting.Indented);
    }

    private static JObject DescribeObjectRecursive(object obj, HashSet<object> visited, int depth, HashSet<string> excludedFields, bool excludeGetOnlyProperties)
    {
        if (obj == null)
            return new JObject { ["__info"] = "null" };

        if (depth <= 0 || visited.Contains(obj))
            return new JObject { ["__info"] = "Max depth reached or already visited" };
        
        
        
        visited.Add(obj);
        JObject jsonObject = new JObject();
        Type type = obj.GetType();

        jsonObject["__type"] = type.Name;
        // get unity managed guid for this object

        if (obj is UnityEngine.ScriptableObject unityObject)
            jsonObject["guid"] = GetGuidForScriptableObject(unityObject);
            
        // Handle Fields
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (excludedFields.Contains(field.Name) || Attribute.IsDefined(field, typeof(JsonDescriptionIgnore)))
                continue;

            object value = field.GetValue(obj);
            jsonObject[field.Name] = SerializeValue(value, visited, depth - 1, excludedFields, excludeGetOnlyProperties);
        }

        // Handle Properties
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
            if (excludedFields.Contains(prop.Name) || Attribute.IsDefined(prop, typeof(JsonDescriptionIgnore)))
                continue;

            if (excludeGetOnlyProperties && prop.SetMethod == null) 
                continue; // Skip get-only properties
  
            object value = prop.GetValue(obj);
            jsonObject[prop.Name] = SerializeValue(value, visited, depth - 1, excludedFields, excludeGetOnlyProperties);
        }

        return jsonObject;
    }

    private static JToken SerializeValue(object value, HashSet<object> visited, int depth, HashSet<string> excludedFields, bool excludeGetOnlyProperties)
    {
        if (value == null)
            return JValue.CreateNull();

        Type type = value.GetType();

        if (type.IsPrimitive || value is string || value is decimal)
            return new JValue(value);

        if (type.IsEnum)
            return new JValue(value.ToString());

        if (value is IEnumerable enumerable)
        {
            JArray array = new JArray();
            foreach (var item in enumerable)
            {
                array.Add(SerializeValue(item, visited, depth - 1, excludedFields, excludeGetOnlyProperties));
            }
            return array;
        }

        return DescribeObjectRecursive(value, visited, depth, excludedFields, excludeGetOnlyProperties);
    }
    
    
    public static string GetGuidForScriptableObject(ScriptableObject scriptableObject) {
        if (scriptableObject == null) return null;
    
        // Get the asset path (e.g., "Assets/Data/MySO.asset")
        string path = AssetDatabase.GetAssetPath(scriptableObject);
    
        // Convert path to GUID
        return AssetDatabase.AssetPathToGUID(path);
    
    }
}
