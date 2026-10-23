using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace GodotPackageManager.Core.Utils;

public sealed class GodotArrayConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Godot.Collections.Array<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(Inner<>).MakeGenericType(itemType))!;
    }

    private sealed class Inner<[MustBeVariant] T> : JsonConverter<Godot.Collections.Array<T>>
    {
        public override Godot.Collections.Array<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return new Godot.Collections.Array<T>();

            var items = JsonSerializer.Deserialize<T[]>(ref reader, options);
            var result = new Godot.Collections.Array<T>();
            if (items != null)
            {
                foreach (var item in items)
                    result.Add(item);
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, Godot.Collections.Array<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                JsonSerializer.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
    }
}
