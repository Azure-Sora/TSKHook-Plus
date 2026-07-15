using System;

namespace TSKHook;

internal static class UiSpriteOverrideMatcher
{
    internal static bool Matches(UiSpriteOverrideRule rule, string spriteName, string textureName,
        int width, int height, string objectPath)
    {
        if (rule == null || !rule.Enabled || !string.Equals(rule.SpriteName, spriteName, StringComparison.Ordinal) ||
            rule.Width != width || rule.Height != height)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.TextureName) &&
            !string.Equals(rule.TextureName, textureName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.ObjectPathSuffix) &&
            !(objectPath ?? string.Empty).EndsWith(NormalizePath(rule.ObjectPathSuffix), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    internal static string BuildTargetKey(UiSpriteOverrideRule rule)
    {
        return string.Join("|", rule.SpriteName ?? string.Empty, rule.TextureName ?? string.Empty,
            rule.Width, rule.Height, NormalizePath(rule.ObjectPathSuffix));
    }

    internal static string NormalizePath(string value)
    {
        return (value ?? string.Empty).Replace('\\', '/').Trim('/');
    }
}

internal sealed class UiSpriteOverrideManifest
{
    public int SchemaVersion { get; set; }
    public UiSpriteOverrideRule[] Sprites { get; set; } = Array.Empty<UiSpriteOverrideRule>();
}

internal sealed class UiSpriteOverrideRule
{
    public string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string SpriteName { get; set; }
    public string TextureName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string ObjectPathSuffix { get; set; }
    public string ReplacementFile { get; set; }
}
