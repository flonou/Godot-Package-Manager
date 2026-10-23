using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Godot;
using GodotPackageManager.Core.Utils;

namespace GodotPackageManager.Core;

public class JsonFileHandler<T> where T: new()
{
    protected readonly string _filePath;
    protected readonly JsonSerializerOptions _jsonOptions;
    public bool FileExists => File.Exists(_filePath);

    public JsonFileHandler(string filePath)
    {
        _filePath = ProjectSettings.GlobalizePath(filePath);
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters= {new GodotArrayConverter()},
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { modifier =>
                {
                    foreach (JsonPropertyInfo property in modifier.Properties)
                    {
                        // If the property DOES NOT have a JsonPropertyNameAttribute, ignore it!
                        var hasAttribute = property.AttributeProvider?
                            .IsDefined(typeof(JsonPropertyNameAttribute), inherit: true) ?? false;
                        
                        if (!hasAttribute)
                        {
                            property.ShouldSerialize = (_, _) => false;
                        }
                    }
                }}
            }
        };
    }

    

    public T Load()
    {
        if (!File.Exists(_filePath))
        {
            return new T(); // Return a clean empty fallback instance
        }

        string rawJson = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<T>(rawJson, _jsonOptions) ?? new T();
    }

    // PURE INPUT: Receives completed data from outside and commits it to disk securely
    public void Save(T data)
    {
        string updatedJson = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(_filePath, updatedJson);
    }
}