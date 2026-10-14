using System.Security.Cryptography;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace TSKHook;

internal static class UiImagePixels
{
    internal static Texture2D Read(Texture source)
    {
        if (source.width <= 0 || source.height <= 0 || source.width > 8192 || source.height > 8192)
            throw new InvalidDataException("Unsupported atlas dimensions.");
        var previous = RenderTexture.active;
        var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D output = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            output.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            output.Apply(false, false);
            return output;
        }
        catch { if (output != null) UObject.Destroy(output); throw; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
    }
    internal static Texture2D Load(string path, int width, int height)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) || texture.width != width || texture.height != height)
                throw new InvalidDataException("Invalid translated PNG: " + Path.GetFileName(path));
            texture.name = "TSKHook.zh." + Path.GetFileNameWithoutExtension(path);
            texture.wrapMode = TextureWrapMode.Clamp;
            // Unity cannot see references held only in this managed plugin cache.
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return texture;
        }
        catch { UObject.Destroy(texture); throw; }
    }
    // Canonical top-down RGBA. Hidden RGB under zero alpha is deliberately ignored.
    internal static string Hash(Il2CppStructArray<Color32> pixels, int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var c = pixels[(height - 1 - y) * width + x]; var i = (y * width + x) * 4;
            if (c.a != 0) { bytes[i] = c.r; bytes[i+1] = c.g; bytes[i+2] = c.b; }
            bytes[i+3] = c.a;
        }
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
    internal static Il2CppStructArray<Color32> SpritePixels(Sprite sprite)
    {
        var atlas = Read(sprite.texture);
        try
        {
            var width = Mathf.RoundToInt(sprite.rect.width); var height = Mathf.RoundToInt(sprite.rect.height);
            var pixels = new Il2CppStructArray<Color32>(width * height);
            var source = atlas.GetPixels32(); var vertices = sprite.vertices; var uv = sprite.uv; var triangles = sprite.triangles;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var ia = triangles[t]; var ib = triangles[t+1]; var ic = triangles[t+2];
                var a = vertices[ia] * sprite.pixelsPerUnit + sprite.pivot;
                var b = vertices[ib] * sprite.pixelsPerUnit + sprite.pivot;
                var c = vertices[ic] * sprite.pixelsPerUnit + sprite.pivot;
                var denominator = (b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if (Math.Abs(denominator) < 0.00001f) continue;
                var minX = Math.Max(0, Mathf.FloorToInt(Math.Min(a.x, Math.Min(b.x,c.x))));
                var maxX = Math.Min(width-1, Mathf.CeilToInt(Math.Max(a.x, Math.Max(b.x,c.x))));
                var minY = Math.Max(0, Mathf.FloorToInt(Math.Min(a.y, Math.Min(b.y,c.y))));
                var maxY = Math.Min(height-1, Mathf.CeilToInt(Math.Max(a.y, Math.Max(b.y,c.y))));
                for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++)
                {
                    var wa = ((b.y-c.y)*(x+0.5f-c.x)+(c.x-b.x)*(y+0.5f-c.y))/denominator;
                    var wb = ((c.y-a.y)*(x+0.5f-c.x)+(a.x-c.x)*(y+0.5f-c.y))/denominator;
                    var wc = 1-wa-wb;
                    if (wa < -0.00001f || wb < -0.00001f || wc < -0.00001f) continue;
                    var coordinate = uv[ia]*wa+uv[ib]*wb+uv[ic]*wc;
                    var sx = Math.Clamp(Mathf.FloorToInt(coordinate.x*atlas.width),0,atlas.width-1);
                    var sy = Math.Clamp(Mathf.FloorToInt(coordinate.y*atlas.height),0,atlas.height-1);
                    pixels[y*width+x] = source[sy*atlas.width+sx];
                }
            }
            return pixels;
        }
        finally { UObject.Destroy(atlas); }
    }
    internal static Il2CppStructArray<Color32> RegionPixels(Texture2D atlas, Spine.AtlasRegion region)
    {
        var pixels = new Il2CppStructArray<Color32>(region.width * region.height);
        var source = atlas.GetPixels32();
        for (var y = 0; y < region.height; y++) for (var x = 0; x < region.width; x++)
        {
            // x/y in an atlas are top-down. degrees=90 rotates the packed image clockwise back to its logical rectangle.
            var topX = region.x + (region.degrees == 90 ? region.height - 1 - y : x);
            var topY = region.y + (region.degrees == 90 ? region.width - 1 - x : region.height - 1 - y);
            pixels[y*region.width+x] = source[(atlas.height-1-topY)*atlas.width+topX];
        }
        return pixels;
    }
    internal static void PatchRegion(Texture2D atlas, Spine.AtlasRegion region, Texture2D replacement)
    {
        var source = replacement.GetPixels32();
        var packedWidth = region.degrees == 90 ? region.height : region.width;
        var packedHeight = region.degrees == 90 ? region.width : region.height;
        // Refresh the existing one-pixel atlas extrusion as well, so filtering cannot sample Japanese glyph edges.
        for (var py = -1; py <= packedHeight; py++) for (var px = -1; px <= packedWidth; px++)
        {
            var cx = Math.Clamp(px,0,packedWidth-1); var cy = Math.Clamp(py,0,packedHeight-1);
            var x = region.degrees == 90 ? region.width-1-cy : cx;
            var y = region.degrees == 90 ? region.height-1-cx : region.height-1-cy;
            var ax = region.x+px; var ay = atlas.height-1-region.y-py;
            if (ax >= 0 && ax < atlas.width && ay >= 0 && ay < atlas.height) atlas.SetPixel(ax,ay,source[y*region.width+x]);
        }
    }
}


