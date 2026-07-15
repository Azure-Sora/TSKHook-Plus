using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spine.Unity;
using UnityEngine;

namespace TSKHook;

internal static class UiSpineAtlasDumpService
{
    private const int MaximumDimension = 8192;
    private const long MaximumPixels = 64L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    internal static SpineDumpResult DumpActive(string outputDirectory, string timestamp)
    {
        var spineDirectory = Path.Combine(outputDirectory, "spine_atlases");
        Directory.CreateDirectory(spineDirectory);

        var collection = Collect();
        var manifest = new SpineDumpManifest
        {
            SchemaVersion = 1,
            CreatedLocal = timestamp,
            TotalSkeletonGraphics = collection.SkeletonGraphicCount,
            TotalTextures = collection.Textures.Count,
            TotalAtlasFiles = collection.AtlasFiles.Count
        };

        foreach (var atlasFile in collection.AtlasFiles.Values.OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            var entry = atlasFile.ToManifestEntry();
            manifest.AtlasFiles.Add(entry);
            try
            {
                var text = atlasFile.Asset.text ?? string.Empty;
                var identity = UiSpriteDumpIdentity.Build(atlasFile.Name, text);
                entry.File = UiSpriteDumpIdentity.BuildAssetFileName(atlasFile.Name, identity, ".atlas.txt");
                File.WriteAllText(Path.Combine(spineDirectory, entry.File), text);
                entry.Exported = true;
                manifest.ExportedAtlasFiles++;
            }
            catch (Exception exception)
            {
                entry.Error = exception.Message;
                manifest.FailedAtlasFiles++;
                Plugin.Global.Log.LogWarning(
                    $"[UI Sprite Dump] Failed to export Spine atlas text '{atlasFile.Name}': {exception.Message}");
            }
        }

        foreach (var texture in collection.Textures.Values.OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            var entry = texture.ToManifestEntry();
            manifest.Textures.Add(entry);
            try
            {
                var png = ReadTexture(texture.Texture);
                var contentHash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
                entry.ContentHash = contentHash;
                entry.File = UiSpriteDumpIdentity.BuildAssetFileName(texture.Name, contentHash, ".png");
                File.WriteAllBytes(Path.Combine(spineDirectory, entry.File), png);
                entry.Exported = true;
                manifest.ExportedTextures++;
            }
            catch (Exception exception)
            {
                entry.Error = exception.Message;
                manifest.FailedTextures++;
                Plugin.Global.Log.LogWarning(
                    $"[UI Sprite Dump] Failed to export Spine texture '{texture.Name}': {exception.Message}");
            }
        }

        File.WriteAllText(Path.Combine(spineDirectory, "spine_manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions));

        Plugin.Global.Log.LogInfo(
            $"[UI Sprite Dump] Spine: {manifest.ExportedTextures}/{manifest.TotalTextures} textures, " +
            $"{manifest.ExportedAtlasFiles}/{manifest.TotalAtlasFiles} atlas files from " +
            $"{manifest.TotalSkeletonGraphics} active SkeletonGraphics.");

        return new SpineDumpResult(manifest.TotalTextures, manifest.ExportedTextures, manifest.FailedTextures);
    }

    private static SpineCollection Collect()
    {
        var collection = new SpineCollection();
        var graphics = UnityEngine.Object.FindObjectsOfType<SkeletonGraphic>(true);
        foreach (var graphic in graphics)
        {
            if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
            {
                continue;
            }

            collection.SkeletonGraphicCount++;
            var objectPath = GetObjectPath(graphic.transform);
            try
            {
                var dataAsset = graphic.SkeletonDataAsset;
                var dataAssetName = dataAsset != null ? dataAsset.name ?? string.Empty : string.Empty;
                AddTexture(collection, graphic.mainTexture, "mainTexture", objectPath, dataAssetName, null, null);
                AddTexture(collection, graphic.baseTexture, "baseTexture", objectPath, dataAssetName, null, null);
                AddTexture(collection, graphic.OverrideTexture, "overrideTexture", objectPath, dataAssetName, null, null);

                var atlasAssets = dataAsset != null ? dataAsset.atlasAssets : null;
                if (atlasAssets == null)
                {
                    continue;
                }

                for (var atlasIndex = 0; atlasIndex < atlasAssets.Length; atlasIndex++)
                {
                    var atlasAssetBase = atlasAssets[atlasIndex];
                    if (atlasAssetBase == null)
                    {
                        continue;
                    }

                    var atlasName = atlasAssetBase.name ?? string.Empty;
                    var atlasAsset = atlasAssetBase.TryCast<SpineAtlasAsset>();
                    if (atlasAsset != null)
                    {
                        AddAtlasFile(collection, atlasAsset.atlasFile, objectPath, dataAssetName, atlasName);
                        AddMaterials(collection, atlasAsset.materials, objectPath, dataAssetName, atlasName);
                    }

                    var spriteAtlasAsset = atlasAssetBase.TryCast<SpineSpriteAtlasAsset>();
                    if (spriteAtlasAsset != null)
                    {
                        AddMaterials(collection, spriteAtlasAsset.materials, objectPath, dataAssetName, atlasName);
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.Global.Log.LogWarning(
                    $"[UI Sprite Dump] Could not inspect SkeletonGraphic '{objectPath}': {exception.Message}");
            }
        }

        return collection;
    }

    private static void AddMaterials(SpineCollection collection,
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material> materials, string objectPath,
        string dataAssetName, string atlasName)
    {
        if (materials == null)
        {
            return;
        }

        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            var material = materials[materialIndex];
            if (material == null)
            {
                continue;
            }

            AddTexture(collection, material.mainTexture, "atlasMaterial", objectPath, dataAssetName,
                atlasName, material.name ?? string.Empty);
        }
    }

    private static void AddTexture(SpineCollection collection, Texture texture, string role, string objectPath,
        string dataAssetName, string atlasAssetName, string materialName)
    {
        if (texture == null)
        {
            return;
        }

        var instanceId = texture.GetInstanceID();
        if (!collection.Textures.TryGetValue(instanceId, out var candidate))
        {
            candidate = new SpineTextureCandidate(texture);
            collection.Textures.Add(instanceId, candidate);
        }

        candidate.Roles.Add(role);
        candidate.ObjectPaths.Add(objectPath);
        AddIfPresent(candidate.SkeletonDataAssets, dataAssetName);
        AddIfPresent(candidate.AtlasAssets, atlasAssetName);
        AddIfPresent(candidate.Materials, materialName);
    }

    private static void AddAtlasFile(SpineCollection collection, TextAsset asset, string objectPath,
        string dataAssetName, string atlasAssetName)
    {
        if (asset == null)
        {
            return;
        }

        var instanceId = asset.GetInstanceID();
        if (!collection.AtlasFiles.TryGetValue(instanceId, out var candidate))
        {
            candidate = new SpineAtlasFileCandidate(asset);
            collection.AtlasFiles.Add(instanceId, candidate);
        }

        candidate.ObjectPaths.Add(objectPath);
        AddIfPresent(candidate.SkeletonDataAssets, dataAssetName);
        AddIfPresent(candidate.AtlasAssets, atlasAssetName);
    }

    private static void AddIfPresent(ISet<string> target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }

    private static byte[] ReadTexture(Texture texture)
    {
        var width = texture.width;
        var height = texture.height;
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
            (long)width * height > MaximumPixels)
        {
            throw new InvalidOperationException($"Texture dimensions are unsafe: {width}x{height}.");
        }

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
                filterMode = texture.filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();
            Graphics.Blit(texture, renderTexture);
            RenderTexture.active = renderTexture;

            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = texture.name
            };
            readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            readback.Apply(false, false);
            return ImageConversion.EncodeToPNG(readback).ToArray();
        }
        finally
        {
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

    private sealed class SpineCollection
    {
        internal int SkeletonGraphicCount { get; set; }
        internal Dictionary<int, SpineTextureCandidate> Textures { get; } = new();
        internal Dictionary<int, SpineAtlasFileCandidate> AtlasFiles { get; } = new();
    }

    private sealed class SpineTextureCandidate
    {
        internal SpineTextureCandidate(Texture texture)
        {
            Texture = texture;
            Name = texture.name ?? string.Empty;
            Width = texture.width;
            Height = texture.height;
            InstanceId = texture.GetInstanceID();
        }

        internal Texture Texture { get; }
        internal string Name { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal int InstanceId { get; }
        internal SortedSet<string> Roles { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> ObjectPaths { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> SkeletonDataAssets { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> AtlasAssets { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> Materials { get; } = new(StringComparer.Ordinal);

        internal SpineTextureDumpEntry ToManifestEntry()
        {
            return new SpineTextureDumpEntry
            {
                TextureName = Name,
                Width = Width,
                Height = Height,
                InstanceId = InstanceId,
                Roles = Roles.ToList(),
                ObjectPaths = ObjectPaths.ToList(),
                SkeletonDataAssets = SkeletonDataAssets.ToList(),
                AtlasAssets = AtlasAssets.ToList(),
                Materials = Materials.ToList()
            };
        }
    }

    private sealed class SpineAtlasFileCandidate
    {
        internal SpineAtlasFileCandidate(TextAsset asset)
        {
            Asset = asset;
            Name = asset.name ?? string.Empty;
        }

        internal TextAsset Asset { get; }
        internal string Name { get; }
        internal SortedSet<string> ObjectPaths { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> SkeletonDataAssets { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> AtlasAssets { get; } = new(StringComparer.Ordinal);

        internal SpineAtlasFileDumpEntry ToManifestEntry()
        {
            return new SpineAtlasFileDumpEntry
            {
                AtlasFileName = Name,
                ObjectPaths = ObjectPaths.ToList(),
                SkeletonDataAssets = SkeletonDataAssets.ToList(),
                AtlasAssets = AtlasAssets.ToList()
            };
        }
    }

    private sealed class SpineDumpManifest
    {
        public int SchemaVersion { get; set; }
        public string CreatedLocal { get; set; }
        public int TotalSkeletonGraphics { get; set; }
        public int TotalTextures { get; set; }
        public int ExportedTextures { get; set; }
        public int FailedTextures { get; set; }
        public int TotalAtlasFiles { get; set; }
        public int ExportedAtlasFiles { get; set; }
        public int FailedAtlasFiles { get; set; }
        public List<SpineTextureDumpEntry> Textures { get; set; } = new();
        public List<SpineAtlasFileDumpEntry> AtlasFiles { get; set; } = new();
    }

    private sealed class SpineTextureDumpEntry
    {
        public string TextureName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int InstanceId { get; set; }
        public string File { get; set; }
        public string ContentHash { get; set; }
        public List<string> Roles { get; set; }
        public List<string> ObjectPaths { get; set; }
        public List<string> SkeletonDataAssets { get; set; }
        public List<string> AtlasAssets { get; set; }
        public List<string> Materials { get; set; }
        public bool Exported { get; set; }
        public string Error { get; set; }
    }

    private sealed class SpineAtlasFileDumpEntry
    {
        public string AtlasFileName { get; set; }
        public string File { get; set; }
        public List<string> ObjectPaths { get; set; }
        public List<string> SkeletonDataAssets { get; set; }
        public List<string> AtlasAssets { get; set; }
        public bool Exported { get; set; }
        public string Error { get; set; }
    }
}

internal readonly record struct SpineDumpResult(int TotalTextures, int ExportedTextures, int FailedTextures);
