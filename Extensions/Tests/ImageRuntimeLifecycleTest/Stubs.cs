using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime
{
    public static class Casts { public static T TryCast<T>(this object value) where T : class => value as T; }
    public static class Il2CppType { public static Type Of<T>() => typeof(T); }
}
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace UnityEngine
{
    public class Object
    {
        private static int next; private readonly int id = ++next;
        public string name; private bool destroyed;
        public int GetInstanceID() => id;
        public static void Destroy(Object value) { if (value is not null) value.destroyed = true; }
        private static bool Missing(Object value) => value is null || value.destroyed;
        public static bool operator ==(Object a,Object b) => Missing(a) ? Missing(b) : !Missing(b) && ReferenceEquals(a,b);
        public static bool operator !=(Object a,Object b) => !(a == b);
        public override bool Equals(object value) => ReferenceEquals(this,value);
        public override int GetHashCode() => id;
    }
    public class Component : Object
    {
        public GameObject gameObject = new();
        public Component GetComponent(Type type) => gameObject.Components.GetValueOrDefault(type);
    }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public SceneManagement.Scene scene = new();
        public Dictionary<Type,Component> Components = new();
        private Transform node;
        public Transform transform => node ??= new Transform { gameObject = this };
    }
    public class Transform : Component { public int childCount => 0; public Transform GetChild(int index) => null; }
    public class Texture : Object
    {
        public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode;
        public TSKHook.RgbaPixels Pixels;
    }
    public class Texture2D : Texture
    {
        public HideFlags hideFlags;
        public Texture2D(int w,int h,TextureFormat f,bool mip,bool linear) { width=w; height=h; }
        public void LoadRawTextureData(IntPtr source,int length) { var bytes = new byte[length]; Marshal.Copy(source,bytes,0,length); Pixels = new(width,height,bytes); }
        public void Apply(bool mip,bool nonReadable) { }
    }
    public class Material : Object
    {
        public Texture mainTexture;
        public bool HasProperty(string key) => true;
        public float GetFloat(string key) => 1;
    }
    public class Sprite : Object
    {
        public Texture2D texture; public Rect rect; public Vector2 pivot; public float pixelsPerUnit = 1;
        public Vector4 border; public HideFlags hideFlags;
        public Vector2[] vertices, uv; public ushort[] triangles;
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float scale,uint extrude,SpriteMeshType mesh,Vector4 border) =>
            new() { texture=texture,rect=rect,pivot=pivot,pixelsPerUnit=scale,border=border };
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x; this.y=y; } }
    public struct Vector4 { }
    public struct Rect { public float width,height; public Rect(float x,float y,float w,float h) { width=w; height=h; } }
    public static class Mathf { public static int RoundToInt(float value) => (int)MathF.Round(value); }
    public enum HideFlags { DontUnloadUnusedAsset }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public enum SpriteMeshType { FullRect }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public bool isLoaded => true;
        public bool IsValid() => true;
        public UnityEngine.GameObject[] GetRootGameObjects() => throw new NotSupportedException("Method unstripping failed");
        public void GetRootGameObjects(Il2CppSystem.Collections.Generic.List<UnityEngine.GameObject> result)
        { SceneManager.RootEnumerations++; result.Clear(); result.AddRange(SceneManager.Roots); }
    }
    public static class SceneManager
    {
        public static UnityEngine.GameObject[] Roots = Array.Empty<UnityEngine.GameObject>();
        public static int RootEnumerations;
        public static int sceneCount => Roots.Length == 0 ? 0 : 1;
        public static Scene GetSceneAt(int index) => new();
    }
}
namespace UnityEngine.UI
{
    public class Image : UnityEngine.Component
    {
        public bool enabled = true; public UnityEngine.Sprite sprite;
        private UnityEngine.Sprite overridden;
        // Unity's getter returns the effective sprite; null setter removes the override.
        public UnityEngine.Sprite overrideSprite { get => overridden ?? sprite; set => overridden = value; }
        public bool HasOverride => overridden != null;
    }
}
namespace Spine
{
    public class AtlasRegion
    {
        public AtlasPage page = new(); public int x,y,width,height,degrees,originalWidth,originalHeight; public float offsetX,offsetY;
    }
    public class AtlasPage { public object rendererObject; }
    public class Atlas { public Dictionary<string,AtlasRegion> Regions = new(); public AtlasRegion FindRegion(string name) => Regions.GetValueOrDefault(name); }
}
namespace Spine.Unity
{
    public class SkeletonDataAsset : UnityEngine.Object { public object[] atlasAssets; }
    public class SpineAtlasAsset { public UnityEngine.Material[] materials; public Spine.Atlas Atlas = new(); public Spine.Atlas GetAtlas() => Atlas; }
    public class SkeletonGraphic : UnityEngine.Component
    {
        public bool enabled = true, allowMultipleCanvasRenderers;
        public SkeletonDataAsset SkeletonDataAsset;
        public UnityEngine.Texture baseTexture;
        public UnityEngine.Texture2D OverrideTexture;
        public UnityEngine.Material material = new();
        public Dictionary<UnityEngine.Texture,UnityEngine.Texture2D> CustomTextureOverride = new();
        public void UpdateMesh(bool force) { }
    }
}
namespace TSKHook
{
    public sealed class Setting { public bool Value = true; }
    public sealed class Settings { public Setting ImageTranslation = new(); }
    public static class TSKConfig { public static bool TranslationEnabled = true,UiTranslationEnabled = true; public static Settings Settings = new(); }
    internal static class UiRuntimeActivity { internal static bool InBattle; }
    internal static class UiTextureReadback
    {
        internal static int Reads;
        internal static Task<RgbaPixels> Next;
        internal static Task<RgbaPixels> Get(UnityEngine.Texture source) { Reads++; var next=Next; Next=null; return next ?? Task.FromResult(source.Pixels); }
        internal static void Update(bool allow) { }
        internal static void Clear() { }
    }
    internal static class Plugin { internal static class Global { internal static readonly LogSink Log = new(); } }
    internal sealed class LogSink
    {
        internal readonly List<string> Warnings = new();
        internal void LogInfo(string message) { }
        internal void LogWarning(string message) => Warnings.Add(message);
    }
}
