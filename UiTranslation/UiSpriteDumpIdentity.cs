using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TSKHook;

internal static class UiSpriteDumpIdentity
{
    internal static string Build(params object[] parts)
    {
        return string.Join("|", parts.Select(FormatPart));
    }

    internal static string BuildFileName(string spriteName, string identity)
    {
        return BuildAssetFileName(spriteName, identity, ".png");
    }

    internal static string BuildAssetFileName(string assetName, string identity, string extension)
    {
        var safeName = SanitizeFileName(assetName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
        return $"{safeName}__{ShortHash(identity)}{safeExtension}";
    }

    internal static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed_sprite";
        }

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(char.IsControl(character) || invalid.Contains(character) ? '_' : character);
        }

        var result = builder.ToString().Trim('.', ' ');
        if (string.IsNullOrEmpty(result))
        {
            result = "unnamed_sprite";
        }

        return result.Length <= 96 ? result : result[..96];
    }

    private static string FormatPart(object value)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
    }
}
