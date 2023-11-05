using System;
using System.Linq;
using System.Text.Json;

namespace GitReleaseManager.Core.Extensions
{
    internal static class JsonExtensions
    {
        public static JsonElement GetJsonElement(this JsonElement jsonElement, string path)
        {
            if (jsonElement.ValueKind is JsonValueKind.Null || jsonElement.ValueKind is JsonValueKind.Undefined)
            {
                return default(JsonElement);
            }

            string[] segments = path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                if (int.TryParse(segment, out var index) && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    jsonElement = jsonElement.EnumerateArray().ElementAtOrDefault(index);
                    if (jsonElement.ValueKind is JsonValueKind.Null || jsonElement.ValueKind is JsonValueKind.Undefined)
                    {
                        return default(JsonElement);
                    }

                    continue;
                }

                jsonElement = jsonElement.TryGetProperty(segment, out var value) ? value : default;

                if (jsonElement.ValueKind is JsonValueKind.Null || jsonElement.ValueKind is JsonValueKind.Undefined)
                {
                    return default(JsonElement);
                }
            }

            return jsonElement;
        }

        public static string GetJsonElementValue(this JsonElement jsonElement) => jsonElement.ValueKind != JsonValueKind.Null &&
                                                                                   jsonElement.ValueKind != JsonValueKind.Undefined
            ? jsonElement.ToString()
            : default;
    }
}
