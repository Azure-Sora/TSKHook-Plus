using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;

namespace TSKHook;

internal static class UiSpriteDumpService
{
    private const int DumpLayer = 31;
    private const int MaximumDimension = 8192;
    private const long MaximumPixels = 64L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    internal static string DumpCurrentUiSprites()
    {
        if (!TSKConfig.UiSpriteDumpEnabled)
        {
            const string disabled = "UI Sprite dump is disabled. Set uiSpriteDump to true and reload config first.";
            Plugin.Global.Log.LogWarning($"[UI Sprite Dump] {disabled}");
            return disabled;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDirectory = Path.Combine(Paths.PluginPath, "TSKHook", "ui_sprite_dump", timestamp);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var candidates = CollectCandidates();
            var manifest = RenderCandidates(candidates, outputDirectory, timestamp);
            var manifestPath = Path.Combine(outputDirectory, "manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            var spine = UiSpineAtlasDumpService.DumpActive(outputDirectory, timestamp);

            var summary = $"Exported {manifest.Exported} of {manifest.TotalSprites} unique active UI sprites. " +
                          $"Spine atlases: {spine.ExportedTextures}/{spine.TotalTextures}. " +
                          $"Failures: {manifest.Failed + spine.FailedTextures}. Output: {outputDirectory}";
            Plugin.Global.Log.LogInfo($"[UI Sprite Dump] {summary}");
            return summary;
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogError($"[UI Sprite Dump] Failed: {exception}");
            return $"UI Sprite dump failed: {exception.Message}";
        }
    }

    private static List<SpriteCandidate> CollectCandidates()
    {
        var candidates = new Dictionary<string, SpriteCandidate>(StringComparer.Ordinal);
        var images = UnityEngine.Object.FindObjectsOfType<Image>(true);
        foreach (var image in images)
        {
            if (image == null || !image.enabled || !image.gameObject.activeInHierarchy)
            {
                continue;
            }

            try
            {
                var displayedSprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                if (displayedSprite == null)
                {
                    continue;
                }

                var candidate = SpriteCandidate.Create(displayedSprite);
                if (!candidates.TryGetValue(candidate.Identity, out var existing))
                {
                    existing = candidate;
                    candidates.Add(candidate.Identity, existing);
                }

                existing.ObjectPaths.Add(GetObjectPath(image.transform));
            }
            catch (Exception exception)
            {
                Plugin.Global.Log.LogWarning(
                    $"[UI Sprite Dump] Could not inspect Image '{GetObjectPath(image.transform)}': {exception.Message}");
            }
        }

        return candidates.Values.OrderBy(candidate => candidate.SpriteName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Identity, StringComparer.Ordinal).ToList();
    }

    private static SpriteDumpManifest RenderCandidates(IReadOnlyList<SpriteCandidate> candidates,
        string outputDirectory, string timestamp)
    {
        var manifest = new SpriteDumpManifest
        {
            SchemaVersion = 1,
            CreatedLocal = timestamp,
            TotalSprites = candidates.Count
        };

        GameObject rendererObject = null;
        GameObject cameraObject = null;
        try
        {
            rendererObject = new GameObject("TSKHook Sprite Dump Renderer")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = DumpLayer
            };
            var spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
            spriteRenderer.enabled = false;
            spriteRenderer.color = Color.white;

            cameraObject = new GameObject("TSKHook Sprite Dump Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = DumpLayer
            };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << DumpLayer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            foreach (var candidate in candidates)
            {
                var entry = candidate.ToManifestEntry();
                manifest.Sprites.Add(entry);
                try
                {
                    entry.File = UiSpriteDumpIdentity.BuildFileName(candidate.SpriteName, candidate.Identity);
                    RenderSprite(candidate, spriteRenderer, camera, Path.Combine(outputDirectory, entry.File));
                    entry.Exported = true;
                    manifest.Exported++;
                }
                catch (Exception exception)
                {
                    entry.Error = exception.Message;
                    manifest.Failed++;
                    Plugin.Global.Log.LogWarning(
                        $"[UI Sprite Dump] Failed to export '{candidate.SpriteName}': {exception.Message}");
                }
            }

            spriteRenderer.enabled = false;
            camera.targetTexture = null;
        }
        finally
        {
            if (rendererObject != null)
            {
                rendererObject.SetActive(false);
                UnityEngine.Object.Destroy(rendererObject);
            }

            if (cameraObject != null)
            {
                cameraObject.SetActive(false);
                UnityEngine.Object.Destroy(cameraObject);
            }
        }

        return manifest;
    }

    private static void RenderSprite(SpriteCandidate candidate, SpriteRenderer spriteRenderer, Camera camera,
        string outputPath)
    {
        var width = Math.Max(1, (int)Math.Ceiling(candidate.RectWidth));
        var height = Math.Max(1, (int)Math.Ceiling(candidate.RectHeight));
        if (width > MaximumDimension || height > MaximumDimension || (long)width * height > MaximumPixels)
        {
            throw new InvalidOperationException($"Sprite dimensions are unsafe: {width}x{height}.");
        }

        var pixelsPerUnit = candidate.PixelsPerUnit > 0f ? candidate.PixelsPerUnit : 100f;
        var centerX = (candidate.RectWidth * 0.5f - candidate.PivotX) / pixelsPerUnit;
        var centerY = (candidate.RectHeight * 0.5f - candidate.PivotY) / pixelsPerUnit;

        spriteRenderer.sprite = candidate.Sprite;
        spriteRenderer.enabled = true;
        camera.aspect = (float)width / height;
        camera.orthographicSize = candidate.RectHeight / (2f * pixelsPerUnit);
        camera.transform.position = new Vector3(centerX, centerY, -10f);

        RenderTexture renderTexture = null;
        Texture2D readback = null;
        var previous = RenderTexture.active;
        try
        {
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();

            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;

            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = candidate.SpriteName
            };
            readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            readback.Apply(false, false);

            var png = ImageConversion.EncodeToPNG(readback);
            File.WriteAllBytes(outputPath, png.ToArray());
        }
        finally
        {
            spriteRenderer.enabled = false;
            spriteRenderer.sprite = null;
            camera.targetTexture = null;
            RenderTexture.active = previous;

            if (readback != null)
            {
                UnityEngine.Object.Destroy(readback);
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
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

    private sealed class SpriteCandidate
    {
        internal Sprite Sprite { get; private init; }
        internal string Identity { get; private set; }
        internal string SpriteName { get; private init; }
        internal string TextureName { get; private init; }
        internal float RectX { get; private init; }
        internal float RectY { get; private init; }
        internal float RectWidth { get; private init; }
        internal float RectHeight { get; private init; }
        internal float PivotX { get; private init; }
        internal float PivotY { get; private init; }
        internal float BorderX { get; private init; }
        internal float BorderY { get; private init; }
        internal float BorderZ { get; private init; }
        internal float BorderW { get; private init; }
        internal float PixelsPerUnit { get; private init; }
        internal bool Packed { get; private init; }
        internal int PackingMode { get; private init; }
        internal int PackingRotation { get; private init; }
        internal float? TextureRectX { get; private set; }
        internal float? TextureRectY { get; private set; }
        internal float? TextureRectWidth { get; private set; }
        internal float? TextureRectHeight { get; private set; }
        internal SortedSet<string> ObjectPaths { get; } = new(StringComparer.Ordinal);

        internal static SpriteCandidate Create(Sprite sprite)
        {
            var rect = sprite.rect;
            var pivot = sprite.pivot;
            var border = sprite.border;
            var texture = sprite.texture;
            var candidate = new SpriteCandidate
            {
                Sprite = sprite,
                SpriteName = sprite.name ?? string.Empty,
                TextureName = texture != null ? texture.name ?? string.Empty : string.Empty,
                RectX = rect.x,
                RectY = rect.y,
                RectWidth = rect.width,
                RectHeight = rect.height,
                PivotX = pivot.x,
                PivotY = pivot.y,
                BorderX = border.x,
                BorderY = border.y,
                BorderZ = border.z,
                BorderW = border.w,
                PixelsPerUnit = sprite.pixelsPerUnit,
                Packed = sprite.packed,
                PackingMode = (int)sprite.packingMode,
                PackingRotation = (int)sprite.packingRotation
            };

            try
            {
                var textureRect = sprite.textureRect;
                candidate.TextureRectX = textureRect.x;
                candidate.TextureRectY = textureRect.y;
                candidate.TextureRectWidth = textureRect.width;
                candidate.TextureRectHeight = textureRect.height;
            }
            catch
            {
                // Tight-packed sprites may not expose a rectangular textureRect. Rendering remains valid.
            }

            candidate.Identity = UiSpriteDumpIdentity.Build(candidate.TextureName, candidate.SpriteName,
                candidate.RectX, candidate.RectY, candidate.RectWidth, candidate.RectHeight,
                candidate.PivotX, candidate.PivotY, candidate.BorderX, candidate.BorderY,
                candidate.BorderZ, candidate.BorderW, candidate.PixelsPerUnit,
                candidate.Packed, candidate.PackingMode, candidate.PackingRotation);
            return candidate;
        }

        internal SpriteDumpEntry ToManifestEntry()
        {
            return new SpriteDumpEntry
            {
                Identity = Identity,
                SpriteName = SpriteName,
                TextureName = TextureName,
                Rect = new[] { RectX, RectY, RectWidth, RectHeight },
                TextureRect = TextureRectX.HasValue
                    ? new[] { TextureRectX.Value, TextureRectY!.Value, TextureRectWidth!.Value, TextureRectHeight!.Value }
                    : null,
                Pivot = new[] { PivotX, PivotY },
                Border = new[] { BorderX, BorderY, BorderZ, BorderW },
                PixelsPerUnit = PixelsPerUnit,
                Packed = Packed,
                PackingMode = PackingMode,
                PackingRotation = PackingRotation,
                ObjectPaths = ObjectPaths.ToList()
            };
        }
    }

    private sealed class SpriteDumpManifest
    {
        public int SchemaVersion { get; set; }
        public string CreatedLocal { get; set; }
        public int TotalSprites { get; set; }
        public int Exported { get; set; }
        public int Failed { get; set; }
        public List<SpriteDumpEntry> Sprites { get; set; } = new();
    }

    private sealed class SpriteDumpEntry
    {
        public string Identity { get; set; }
        public string SpriteName { get; set; }
        public string TextureName { get; set; }
        public string File { get; set; }
        public float[] Rect { get; set; }
        public float[] TextureRect { get; set; }
        public float[] Pivot { get; set; }
        public float[] Border { get; set; }
        public float PixelsPerUnit { get; set; }
        public bool Packed { get; set; }
        public int PackingMode { get; set; }
        public int PackingRotation { get; set; }
        public List<string> ObjectPaths { get; set; }
        public bool Exported { get; set; }
        public string Error { get; set; }
    }
}
