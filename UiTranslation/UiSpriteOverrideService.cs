using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace TSKHook;

internal static class UiSpriteOverrideService
{
    private const float ScanIntervalSeconds = 0.5f;
    private const int MaximumDimension = 8192;
    private const long MaximumPixels = 64L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, List<LoadedOverride>> OverridesBySpriteName =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<int, AppliedImage> AppliedImages = new();
    private static readonly HashSet<string> LoggedAmbiguities = new(StringComparer.Ordinal);

    private static string manifestPath;
    private static string manifestDirectory;
    private static float scanElapsed;
    private static int initialized;
    private static int shuttingDown;
    private static long scanCount;
    private static long matchCount;

    internal static void Initialize()
    {
        if (initialized != 0)
        {
            return;
        }

        initialized = 1;
        manifestPath = Path.Combine(Paths.PluginPath, "TSKHook", "ui_sprite_overrides.json");
        manifestDirectory = Path.GetDirectoryName(manifestPath) ?? Paths.PluginPath;
        Reload();
    }

    internal static void Reload()
    {
        if (shuttingDown != 0)
        {
            return;
        }

        RestoreAll();
        DestroyLoadedOverrides();
        OverridesBySpriteName.Clear();
        LoggedAmbiguities.Clear();
        scanElapsed = ScanIntervalSeconds;

        if (string.IsNullOrEmpty(manifestPath))
        {
            manifestPath = Path.Combine(Paths.PluginPath, "TSKHook", "ui_sprite_overrides.json");
            manifestDirectory = Path.GetDirectoryName(manifestPath) ?? Paths.PluginPath;
        }

        if (!File.Exists(manifestPath))
        {
            Plugin.Global.Log.LogInfo($"[UI Sprite Override] Manifest not found; feature inactive: {manifestPath}");
            return;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<UiSpriteOverrideManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest?.SchemaVersion != 1)
            {
                Plugin.Global.Log.LogWarning("[UI Sprite Override] Unsupported or missing manifest schemaVersion.");
                return;
            }

            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            var loadedCount = 0;
            foreach (var rule in manifest.Sprites ?? Array.Empty<UiSpriteOverrideRule>())
            {
                if (!TryValidateRule(rule, targetKeys, out var reason))
                {
                    Plugin.Global.Log.LogWarning(
                        $"[UI Sprite Override] Skipped rule '{rule?.Id ?? "<unnamed>"}': {reason}");
                    continue;
                }

                try
                {
                    var replacementPath = ResolveReplacementPath(rule.ReplacementFile);
                    var texture = LoadTexture(replacementPath, rule);
                    var loaded = new LoadedOverride(rule, texture, replacementPath);
                    if (!OverridesBySpriteName.TryGetValue(rule.SpriteName, out var rules))
                    {
                        rules = new List<LoadedOverride>();
                        OverridesBySpriteName.Add(rule.SpriteName, rules);
                    }

                    rules.Add(loaded);
                    loadedCount++;
                }
                catch (Exception exception)
                {
                    Plugin.Global.Log.LogWarning(
                        $"[UI Sprite Override] Could not load rule '{rule.Id}': {exception.Message}");
                }
            }

            Plugin.Global.Log.LogInfo(
                $"[UI Sprite Override] Loaded {loadedCount} rule(s) from {manifestPath}.");
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogError($"[UI Sprite Override] Failed to load manifest: {exception}");
        }
    }

    internal static void Tick(float unscaledDeltaTime)
    {
        if (initialized == 0 || shuttingDown != 0)
        {
            return;
        }

        if (!TSKConfig.TranslationEnabled || !TSKConfig.UiTranslationEnabled)
        {
            if (AppliedImages.Count > 0)
            {
                RestoreAll();
            }

            return;
        }

        if (OverridesBySpriteName.Count == 0)
        {
            return;
        }

        scanElapsed += Math.Max(0f, unscaledDeltaTime);
        if (scanElapsed < ScanIntervalSeconds)
        {
            return;
        }

        scanElapsed = 0f;
        ScanActiveImages();
    }

    internal static void Shutdown()
    {
        if (shuttingDown != 0)
        {
            return;
        }

        shuttingDown = 1;
        RestoreAll();
        DestroyLoadedOverrides();
        OverridesBySpriteName.Clear();
        Plugin.Global.Log.LogInfo(
            $"[UI Sprite Override] Stopped. Scans: {scanCount}, applied matches: {matchCount}.");
    }

    private static void ScanActiveImages()
    {
        scanCount++;
        CleanupDestroyedImages();

        var images = UnityEngine.Object.FindObjectsOfType<Image>(true);
        foreach (var image in images)
        {
            if (image == null || !image.enabled || !image.gameObject.activeInHierarchy)
            {
                continue;
            }

            var imageId = image.GetInstanceID();
            if (AppliedImages.TryGetValue(imageId, out var applied))
            {
                var currentSlotSprite = applied.UsesOverrideSprite ? image.overrideSprite : image.sprite;
                if (currentSlotSprite == applied.ReplacementSprite)
                {
                    continue;
                }

                AppliedImages.Remove(imageId);
            }

            var baseSprite = image.sprite;
            var activeSprite = image.overrideSprite;
            var usesOverrideSprite = activeSprite != null && activeSprite != baseSprite;
            var originalSprite = usesOverrideSprite ? activeSprite : baseSprite;
            if (originalSprite == null || !OverridesBySpriteName.TryGetValue(originalSprite.name, out var candidates))
            {
                continue;
            }

            var objectPath = GetObjectPath(image.transform);
            var textureName = originalSprite.texture != null ? originalSprite.texture.name ?? string.Empty : string.Empty;
            var width = Math.Max(1, Mathf.RoundToInt(originalSprite.rect.width));
            var height = Math.Max(1, Mathf.RoundToInt(originalSprite.rect.height));
            var matches = candidates.Where(candidate => UiSpriteOverrideMatcher.Matches(candidate.Rule,
                originalSprite.name, textureName, width, height, objectPath)).ToList();

            if (matches.Count != 1)
            {
                if (matches.Count > 1)
                {
                    var ambiguity = $"{originalSprite.name}|{textureName}|{width}x{height}|{objectPath}";
                    if (LoggedAmbiguities.Add(ambiguity))
                    {
                        Plugin.Global.Log.LogWarning(
                            $"[UI Sprite Override] Ambiguous match ({matches.Count} rules), skipped: {ambiguity}");
                    }
                }

                continue;
            }

            var replacement = matches[0].GetOrCreateSprite(originalSprite);
            if (usesOverrideSprite)
            {
                image.overrideSprite = replacement;
            }
            else
            {
                image.sprite = replacement;
            }

            AppliedImages[imageId] = new AppliedImage(image, originalSprite, replacement, usesOverrideSprite);
            matchCount++;
            Plugin.Global.Log.LogInfo(
                $"[UI Sprite Override] Applied '{matches[0].Rule.Id}' to {objectPath}.");
        }
    }

    private static bool TryValidateRule(UiSpriteOverrideRule rule, ISet<string> targetKeys, out string reason)
    {
        if (rule == null || !rule.Enabled)
        {
            reason = "disabled";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.SpriteName) ||
            string.IsNullOrWhiteSpace(rule.ReplacementFile))
        {
            reason = "id, spriteName and replacementFile are required";
            return false;
        }

        if (rule.Width <= 0 || rule.Height <= 0 || rule.Width > MaximumDimension || rule.Height > MaximumDimension ||
            (long)rule.Width * rule.Height > MaximumPixels)
        {
            reason = $"unsafe target dimensions {rule.Width}x{rule.Height}";
            return false;
        }

        var key = UiSpriteOverrideMatcher.BuildTargetKey(rule);
        if (!targetKeys.Add(key))
        {
            reason = "duplicate target selector";
            return false;
        }

        reason = null;
        return true;
    }

    private static string ResolveReplacementPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("replacementFile must be relative to the manifest directory");
        }

        var root = Path.GetFullPath(manifestDirectory + Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("replacementFile escapes the manifest directory");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("replacement PNG not found", path);
        }

        return path;
    }

    private static Texture2D LoadTexture(string path, UiSpriteOverrideRule rule)
    {
        var bytes = File.ReadAllBytes(path);
        Il2CppStructArray<byte> il2CppBytes = bytes;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = $"TSKHook override {rule.Id}",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        try
        {
            if (!ImageConversion.LoadImage(texture, il2CppBytes, true))
            {
                throw new InvalidDataException("Unity could not decode the replacement PNG");
            }

            if (texture.width != rule.Width || texture.height != rule.Height)
            {
                throw new InvalidDataException(
                    $"replacement size is {texture.width}x{texture.height}, expected {rule.Width}x{rule.Height}");
            }

            return texture;
        }
        catch
        {
            UnityEngine.Object.Destroy(texture);
            throw;
        }
    }

    private static void RestoreAll()
    {
        foreach (var applied in AppliedImages.Values)
        {
            var image = applied.Image;
            if (image == null)
            {
                continue;
            }

            if (applied.UsesOverrideSprite)
            {
                if (image.overrideSprite == applied.ReplacementSprite)
                {
                    image.overrideSprite = applied.OriginalSprite;
                }
            }
            else if (image.sprite == applied.ReplacementSprite)
            {
                image.sprite = applied.OriginalSprite;
            }
        }

        AppliedImages.Clear();
    }

    private static void CleanupDestroyedImages()
    {
        var destroyed = AppliedImages.Where(pair => pair.Value.Image == null).Select(pair => pair.Key).ToList();
        foreach (var key in destroyed)
        {
            AppliedImages.Remove(key);
        }
    }

    private static void DestroyLoadedOverrides()
    {
        foreach (var loaded in OverridesBySpriteName.Values.SelectMany(value => value))
        {
            loaded.Destroy();
        }
    }

    private static string GetObjectPath(Transform transform)
    {
        var segments = new List<string>();
        var current = transform;
        while (current != null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private sealed class LoadedOverride
    {
        private readonly Dictionary<string, Sprite> spritesByGeometry = new(StringComparer.Ordinal);

        internal LoadedOverride(UiSpriteOverrideRule rule, Texture2D texture, string sourcePath)
        {
            Rule = rule;
            Texture = texture;
            SourcePath = sourcePath;
        }

        internal UiSpriteOverrideRule Rule { get; }
        internal Texture2D Texture { get; }
        internal string SourcePath { get; }

        internal Sprite GetOrCreateSprite(Sprite original)
        {
            var rect = original.rect;
            var pivot = original.pivot;
            var border = original.border;
            var pixelsPerUnit = original.pixelsPerUnit > 0f ? original.pixelsPerUnit : 100f;
            var geometryKey = string.Join("|", rect.width, rect.height, pivot.x, pivot.y,
                border.x, border.y, border.z, border.w, pixelsPerUnit);
            if (spritesByGeometry.TryGetValue(geometryKey, out var existing))
            {
                return existing;
            }

            if (original.texture != null)
            {
                Texture.filterMode = original.texture.filterMode;
            }

            var normalizedPivot = new Vector2(pivot.x / rect.width, pivot.y / rect.height);
            var sprite = Sprite.Create(Texture, new Rect(0f, 0f, Texture.width, Texture.height), normalizedPivot,
                pixelsPerUnit, 0, SpriteMeshType.FullRect, border, false);
            sprite.name = original.name + "__tsk_zh_cn";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            spritesByGeometry.Add(geometryKey, sprite);
            return sprite;
        }

        internal void Destroy()
        {
            foreach (var sprite in spritesByGeometry.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
            }

            spritesByGeometry.Clear();
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
            }
        }
    }

    private sealed class AppliedImage
    {
        internal AppliedImage(Image image, Sprite originalSprite, Sprite replacementSprite, bool usesOverrideSprite)
        {
            Image = image;
            OriginalSprite = originalSprite;
            ReplacementSprite = replacementSprite;
            UsesOverrideSprite = usesOverrideSprite;
        }

        internal Image Image { get; }
        internal Sprite OriginalSprite { get; }
        internal Sprite ReplacementSprite { get; }
        internal bool UsesOverrideSprite { get; }
    }
}
