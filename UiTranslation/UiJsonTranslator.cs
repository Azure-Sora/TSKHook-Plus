using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TSKHook;

internal static class UiJsonTranslator
{
    internal static bool TryTranslate(string apiName, string json,
        IReadOnlyDictionary<string, UiRuntimeTranslation> translations,
        out string translatedJson, out int replacementCount)
    {
        translatedJson = json;
        replacementCount = 0;
        if (string.IsNullOrWhiteSpace(json) || translations == null || translations.Count == 0)
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var buffer = new ArrayBufferWriter<byte>(Encoding.UTF8.GetByteCount(json));
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Rewrite(apiName, "$", document.RootElement,
                new Dictionary<string, string>(StringComparer.Ordinal), translations, writer,
                ref replacementCount);
        }

        if (replacementCount == 0)
        {
            return false;
        }

        translatedJson = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return true;
    }

    private static void Rewrite(string apiName, string path, JsonElement element,
        IReadOnlyDictionary<string, string> inheritedContext,
        IReadOnlyDictionary<string, UiRuntimeTranslation> translations,
        Utf8JsonWriter writer, ref int replacementCount)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var objectContext = UiJsonContext.Extend(inheritedContext, element);
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    Rewrite(apiName, path + "." + property.Name, property.Value, objectContext,
                        translations, writer, ref replacementCount);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Rewrite(apiName, path + "[]", item, inheritedContext,
                        translations, writer, ref replacementCount);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var source = element.GetString();
                if (!CaptureTextFilter.IsSensitivePath(path) && TryGetTranslation(apiName, path, source,
                        inheritedContext, translations, out var translation))
                {
                    writer.WriteStringValue(translation);
                    replacementCount++;
                }
                else
                {
                    writer.WriteStringValue(source);
                }
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool TryGetTranslation(string apiName, string path, string source,
        IReadOnlyDictionary<string, string> context,
        IReadOnlyDictionary<string, UiRuntimeTranslation> translations,
        out string translation)
    {
        translation = null;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var sourceHash = Sha256(source);
        var descriptor = UiTextKeyBuilder.Build(apiName, path, source, sourceHash, context);
        if (!translations.TryGetValue(descriptor.Key, out var entry) ||
            !string.Equals(entry.SourceHash, sourceHash, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(entry.Translation))
        {
            return false;
        }

        translation = entry.Translation;
        return true;
    }

    internal static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal sealed record UiRuntimeTranslation(string SourceHash, string Translation);
