using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GitReleaseManager.Core.Extensions
{
    internal static class JsonExtensions
    {
        /// <summary>
        /// Get a JsonElement from a path. Each level in the path is seperated by a dot.
        /// </summary>
        /// <param name="jsonElement">The parent Json element.</param>
        /// <param name="path">The path of the desired child element.</param>
        /// <returns>The child element.</returns>
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

        /// <summary>
        /// Get the first JsonElement matching a path from the provided list of paths.
        /// </summary>
        /// <param name="jsonElement">The parent Json element.</param>
        /// <param name="paths">The path of the desired child element.</param>
        /// <returns>The child element.</returns>
        public static JsonElement GetFirstJsonElement(this JsonElement jsonElement, IEnumerable<string> paths)
        {
            if (jsonElement.ValueKind is JsonValueKind.Null || jsonElement.ValueKind is JsonValueKind.Undefined)
            {
                return default(JsonElement);
            }

            var element = default(JsonElement);

            foreach (var path in paths)
            {
                element = jsonElement.GetJsonElement(path);

                if (element.ValueKind is JsonValueKind.Null || element.ValueKind is JsonValueKind.Undefined)
                {
                    continue;
                }

                break;
            }

            return element;
        }

        public static string GetJsonElementValue(this JsonElement jsonElement) => jsonElement.ValueKind != JsonValueKind.Null &&
                                                                                   jsonElement.ValueKind != JsonValueKind.Undefined
            ? jsonElement.ToString()
            : default;
    }
}
