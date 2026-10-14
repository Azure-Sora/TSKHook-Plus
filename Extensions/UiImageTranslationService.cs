using BepInEx;
using Il2CppInterop.Runtime;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace TSKHook;

// Runs on Unity's thread. Owns only per-component overrides; never changes shared game materials or skeleton data.
internal static class UiImageTranslationService
{
    private static string Root = Path.Combine(Paths.PluginPath, "TSKHook", "ui_images");
    private static UiImageManifest manifest;
    private static Dictionary<string, UiSpriteRule> spriteRules = new(StringComparer.Ordinal);
    private static Dictionary<string, UiSpineRule> spineRules = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, SpriteAsset> sprites = new();
    private static readonly Dictionary<int, AtlasAsset> atlases = new();
    private static readonly Dictionary<string, Texture2D> files = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, ImageBinding> images = new();
    private static readonly Dictionary<int, SpineBinding> graphics = new();
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private static DateTime nextScan;
    private static bool enabled;

    internal static void Initialize() => Reload(Root);
    internal static bool Reload(string directory)
    {
        try
        {
            if (!File.Exists(Path.Combine(directory, "manifest.json"))) return false;
            var candidate = UiImageManifest.Read(directory);
            var candidateSprites = candidate.Sprites.ToDictionary(x => x.Sprite, StringComparer.Ordinal);
            var candidateSpine = candidate.Spine.ToDictionary(x => x.Asset, StringComparer.Ordinal);
            // Changing only the package/version/path does not invalidate identical image overrides.
            // Avoid restoring every visible image while the bounded GPU scan rebuilds the same data.
            var sameRules = candidate.HasSameRules(manifest);
            if (!sameRules) Shutdown();
            Root = directory;
            manifest = candidate;
            spriteRules = candidateSprites;
            spineRules = candidateSpine;
            reported.Clear();
            if (!sameRules) enabled = false;
            nextScan = DateTime.MinValue;
            Plugin.Global.Log.LogInfo($"[UI Images] Loaded {manifest.Version}: {spriteRules.Count} sprites, {spineRules.Count} Spine cards.");
            return true;
        }
        catch (Exception e) { Warn("manifest", "Package unavailable; previous images retained: " + e.Message); return false; }
    }
    internal static void Update()
    {
        var requested = manifest != null && TSKConfig.TranslationEnabled && TSKConfig.UiTranslationEnabled;
        if (!requested)
        {
            if (enabled) Restore();
            enabled = false;
            return;
        }
        enabled = true;
        if (DateTime.UtcNow < nextScan) return;
        nextScan = DateTime.UtcNow.AddMilliseconds(250);
        try
        {
            foreach (var pair in images.ToArray())
            {
                var binding = pair.Value;
                if (binding.Image == null) { images.Remove(pair.Key); continue; }
                if (binding.Image.sprite != binding.Base || binding.Image.overrideSprite != binding.Replacement)
                {
                    if (binding.Image.overrideSprite == binding.Replacement) binding.Image.overrideSprite = null;
                    images.Remove(pair.Key);
                }
            }
            foreach (var pair in graphics.ToArray())
                if (pair.Value.Graphic == null || pair.Value.Graphic.SkeletonDataAsset != pair.Value.Asset || !Owns(pair.Value))
                { Restore(pair.Value); graphics.Remove(pair.Key); }

            // Page bundles may destroy the original assets. Release our retained copies once no live component owns them.
            foreach (var pair in sprites.ToArray())
                if (pair.Value.Source == null && !images.Values.Any(x => x.Replacement == pair.Value.Replacement))
                {
                    if (pair.Value.Replacement != null) UObject.Destroy(pair.Value.Replacement);
                    sprites.Remove(pair.Key);
                }
            foreach (var pair in atlases.ToArray())
                if (pair.Value.Source == null && !graphics.Values.Any(x => x.Replacement == pair.Value.Replacement))
                {
                    if (pair.Value.Replacement != null) UObject.Destroy(pair.Value.Replacement);
                    atlases.Remove(pair.Key);
                }

            var readbacks = 0;
            foreach (var candidate in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Image>()))
            {
                var image = candidate == null ? null : candidate.TryCast<Image>();
                if (image == null || !image.enabled || !image.gameObject.activeInHierarchy || !image.gameObject.scene.IsValid() || !image.gameObject.scene.isLoaded ||
                    images.ContainsKey(image.GetInstanceID())) continue;
                var source = image.overrideSprite ?? image.sprite;
                if (source == null || source.texture == null || !spriteRules.TryGetValue(source.name, out var rule)) continue;
                // Different UI atlases can reuse a name (for example the plain footer icon_gacha).
                if (Mathf.RoundToInt(source.rect.width) != rule.Width || Mathf.RoundToInt(source.rect.height) != rule.Height) continue;
                var id = source.GetInstanceID();
                if (!sprites.TryGetValue(id, out var asset) || asset.Source != source || (!asset.Rejected && asset.Replacement == null))
                {
                    if (readbacks >= 1) continue;
                    readbacks++;
                    asset = MakeSprite(source, rule);
                    sprites[id] = asset;
                }
                if (asset.Replacement == null || asset.Source != source) continue;
                var originalOverride = source == image.sprite ? null : source;
                images[image.GetInstanceID()] = new ImageBinding(image, image.sprite, originalOverride, asset.Replacement);
                image.overrideSprite = asset.Replacement;
            }
            foreach (var candidate in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SkeletonGraphic>()))
            {
                var graphic = candidate == null ? null : candidate.TryCast<SkeletonGraphic>();
                if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy || !graphic.gameObject.scene.IsValid() || !graphic.gameObject.scene.isLoaded ||
                    graphic.SkeletonDataAsset == null || graphics.ContainsKey(graphic.GetInstanceID()) ||
                    !spineRules.TryGetValue(graphic.SkeletonDataAsset.name, out var rule)) continue;
                try
                {
                    foreach (var atlasBase in graphic.SkeletonDataAsset.atlasAssets)
                    {
                        var atlas = atlasBase == null ? null : atlasBase.TryCast<SpineAtlasAsset>();
                        if (atlas == null) continue;
                        foreach (var material in atlas.materials)
                        {
                            var source = material?.mainTexture;
                            if (source == null || source.name != rule.Texture) continue;
                            var id = source.GetInstanceID();
                            if (!atlases.TryGetValue(id, out var asset) || asset.Source != source || (!asset.Rejected && asset.Replacement == null))
                            {
                                if (readbacks >= 1) continue;
                                readbacks++;
                                var shader = graphic.material;
                                if (shader == null || !shader.HasProperty("_StraightAlphaInput") || shader.GetFloat("_StraightAlphaInput") < 0.5f)
                                    throw new InvalidDataException("Unsupported Spine alpha mode.");
                                asset = MakeAtlas(source, atlas, rule);
                                atlases[id] = asset;
                            }
                            if (asset.Replacement == null || asset.Source != source) continue;
                            var multiple = graphic.allowMultipleCanvasRenderers;
                            if (!multiple && graphic.baseTexture != source) continue;
                            var hadMapping = graphic.CustomTextureOverride.TryGetValue(source, out var mapping);
                            // Let a separate mod or an inspector preview retain its own override.
                            if (multiple && hadMapping && mapping == null) { graphic.CustomTextureOverride.Remove(source); hadMapping = false; }
                            if ((multiple && hadMapping) || (!multiple && graphic.OverrideTexture != null)) continue;
                            var binding = new SpineBinding(graphic, graphic.SkeletonDataAsset, source, asset.Replacement, multiple);
                            graphics[graphic.GetInstanceID()] = binding;
                            if (multiple) graphic.CustomTextureOverride[source] = asset.Replacement;
                            else graphic.OverrideTexture = asset.Replacement;
                            graphic.UpdateMesh(true);
                        }
                    }
                }
                catch (Exception e) { Warn(rule.Asset, e.Message); }
            }
        }
        catch (Exception e) { Warn("scan", e.Message); }
    }
    private static SpriteAsset MakeSprite(Sprite source, UiSpriteRule rule)
    {
        try
        {
            if (Mathf.RoundToInt(source.rect.width) != rule.Width || Mathf.RoundToInt(source.rect.height) != rule.Height ||
                UiImagePixels.Hash(UiImagePixels.SpritePixels(source), rule.Width, rule.Height) != rule.SourceRgbaSha256)
                throw new InvalidDataException("Source sprite geometry or pixels changed; kept current game artwork.");
            var texture = GetFile(rule.File, rule.Width, rule.Height);
            texture.filterMode = source.texture.filterMode;
            var pivot = new Vector2(source.pivot.x / rule.Width, source.pivot.y / rule.Height);
            var sprite = Sprite.Create(texture, new Rect(0,0,rule.Width,rule.Height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
            sprite.name = "TSKHook.zh." + source.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return new SpriteAsset(source, sprite, false);
        }
        catch (Exception e) { Warn(rule.Sprite, e.ToString()); return new SpriteAsset(source, null, e is InvalidDataException); }
    }
    private static AtlasAsset MakeAtlas(Texture source, SpineAtlasAsset atlas, UiSpineRule rule)
    {
        Texture2D translated = null;
        try
        {
            translated = UiImagePixels.Read(source);
            var regions = new List<(AtlasRegion Region, UiSpineRegionRule Rule)>();
            foreach (var item in rule.Regions)
            {
                var region = atlas.GetAtlas().FindRegion(item.Name);
                if (region == null || region.page?.rendererObject?.TryCast<Material>()?.mainTexture != source ||
                    region.width != item.Width || region.height != item.Height || region.degrees != item.Degrees ||
                    region.originalWidth != item.OriginalWidth || region.originalHeight != item.OriginalHeight ||
                    Math.Abs(region.offsetX-item.OffsetX) > 0.01f || Math.Abs(region.offsetY-item.OffsetY) > 0.01f ||
                    UiImagePixels.Hash(UiImagePixels.RegionPixels(translated,region),item.Width,item.Height) != item.SourceRgbaSha256)
                    throw new InvalidDataException("Source title geometry or pixels changed; kept current game artwork: " + item.Name);
                regions.Add((region,item));
            }
            // Only the checked title regions are replaced. Character art and event covers come from the current atlas.
            foreach (var item in regions) UiImagePixels.PatchRegion(translated,item.Region,GetFile(item.Rule.File,item.Rule.Width,item.Rule.Height));
            translated.Apply(false,false);
            translated.filterMode = source.filterMode;
            translated.wrapMode = source.wrapMode;
            translated.name = "TSKHook.zh." + source.name;
            translated.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return new AtlasAsset(source,translated,false);
        }
        catch (Exception e)
        {
            if (translated != null) UObject.Destroy(translated);
            Warn(rule.Asset,e.Message);
            return new AtlasAsset(source,null,e is InvalidDataException);
        }
    }
    private static Texture2D GetFile(string file,int width,int height)
    {
        if (!files.TryGetValue(file,out var texture) || texture == null) files[file] = texture = UiImagePixels.Load(UiImageManifest.AssetPath(Root,file),width,height);
        return texture;
    }
    private static bool Owns(SpineBinding item) => item.Graphic != null &&
        (item.Multiple ? item.Graphic.CustomTextureOverride.TryGetValue(item.Source,out var mapped) && mapped == item.Replacement : item.Graphic.OverrideTexture == item.Replacement);
    private static void Restore(SpineBinding item)
    {
        if (!Owns(item)) return;
        if (item.Multiple) item.Graphic.CustomTextureOverride.Remove(item.Source);
        else item.Graphic.OverrideTexture = null;
        item.Graphic.UpdateMesh(true);
    }
    private static void Restore()
    {
        foreach (var item in images.Values)
            if (item.Image != null && item.Image.overrideSprite == item.Replacement)
                item.Image.overrideSprite = item.Image.sprite == item.Base ? item.OriginalOverride : null;
        images.Clear();
        foreach (var item in graphics.Values) Restore(item);
        graphics.Clear();
    }
    internal static void Shutdown()
    {
        Restore();
        foreach (var item in sprites.Values) if (item.Replacement != null) UObject.Destroy(item.Replacement);
        foreach (var item in atlases.Values) if (item.Replacement != null) UObject.Destroy(item.Replacement);
        foreach (var texture in files.Values) if (texture != null) UObject.Destroy(texture);
        sprites.Clear(); atlases.Clear(); files.Clear();
    }
    private static void Warn(string key,string message)
    {
        if (reported.Add(key)) Plugin.Global.Log.LogWarning("[UI Images] " + key + ": " + message);
    }
    private sealed record SpriteAsset(Sprite Source,Sprite Replacement,bool Rejected);
    private sealed record AtlasAsset(Texture Source,Texture2D Replacement,bool Rejected);
    private sealed record ImageBinding(Image Image,Sprite Base,Sprite OriginalOverride,Sprite Replacement);
    private sealed record SpineBinding(SkeletonGraphic Graphic,SkeletonDataAsset Asset,Texture Source,Texture2D Replacement,bool Multiple);
}
