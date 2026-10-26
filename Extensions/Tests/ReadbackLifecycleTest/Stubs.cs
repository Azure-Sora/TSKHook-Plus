using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UnityEngine
{
    public class Object { public static void Destroy(Object value) { } }
    public class Texture : Object
    {
        public int width, height; public readonly int Id;
        public Texture(int id,int w,int h) { Id=id; width=w; height=h; }
        public int GetInstanceID() => Id;
    }
    public sealed class Texture2D : Texture
    {
        public Texture2D() : base(0,2,2) { }
        public RawBytes GetRawTextureData() => new();
    }
    public sealed class RawBytes
    {
        public int Length => TSKHook.NativeFixture.Pixels.Length;
        public IntPtr Pointer => TSKHook.NativeFixture.ArrayPointer;
    }
    public sealed class RenderTexture : Texture
    {
        public static int Releases;
        public RenderTexture(int w,int h) : base(0,w,h) { }
        public static RenderTexture GetTemporary(int w,int h,int depth,RenderTextureFormat f,RenderTextureReadWrite rw) => new(w,h);
        public static void ReleaseTemporary(RenderTexture target) { Releases++; }
    }
    public enum RenderTextureFormat { ARGB32 }
    public enum RenderTextureReadWrite { sRGB }
    public static class Graphics { public static void Blit(Texture source,RenderTexture destination) { } }
    public static class Application { public static string unityVersion => "2021.3.25f1"; }
    public static class SystemInfo { public static bool supportsAsyncGPUReadback; }
}
namespace UnityEngine.Experimental.Rendering { public enum GraphicsFormat { R8G8B8A8_SRGB } }
namespace Il2CppInterop.Runtime
{
    public static class IL2CPP
    {
        public static IntPtr Il2CppObjectBaseToPtr(object value) => (IntPtr)1;
        public static IntPtr il2cpp_resolve_icall(string name) => TSKHook.NativeFixture.Resolve(name);
    }
}
namespace TSKHook
{
    internal static class UiImagePixels { internal static int Reads; internal static UnityEngine.Texture2D Read(UnityEngine.Texture value) { Reads++; return new(); } }
    internal static class Plugin { internal static class Global { internal static readonly LogSink Log = new(); } }
    internal sealed class LogSink { internal void LogInfo(string message) { } }
    internal static unsafe class NativeFixture
    {
        internal static bool Done, Error;
        internal static int Submissions;
        internal static readonly byte[] Pixels = Enumerable.Range(0,16).Select(i => (byte)i).ToArray();
        internal static readonly IntPtr ArrayPointer = Marshal.AllocHGlobal(IntPtr.Size*4+16);
        internal static readonly IntPtr PixelPointer = IntPtr.Add(ArrayPointer,IntPtr.Size*4);
        static NativeFixture() => Marshal.Copy(Pixels,0,PixelPointer,16);
        [StructLayout(LayoutKind.Sequential)] private struct Request { public IntPtr Pointer; public int Version; }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Submit(IntPtr source,int mip,int format,IntPtr destination,Request* request)
        { Submissions++; request->Pointer = (IntPtr)Submissions; request->Version = 1; }
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static byte IsDone(Request* request) => (byte)(Done ? 1 : 0);
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static byte HasError(Request* request) => (byte)(Error ? 1 : 0);
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static int Size(Request* request) => 16;
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static IntPtr Data(Request* request,int layer) => PixelPointer;
        internal static IntPtr Resolve(string name) => name switch
        {
            "UnityEngine.Rendering.AsyncGPUReadback::Request_Internal_Texture_2_Injected" => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr,int,int,IntPtr,Request*,void>)&Submit,
            "UnityEngine.Rendering.AsyncGPUReadbackRequest::IsDone_Injected" => (IntPtr)(delegate* unmanaged[Cdecl]<Request*,byte>)&IsDone,
            "UnityEngine.Rendering.AsyncGPUReadbackRequest::HasError_Injected" => (IntPtr)(delegate* unmanaged[Cdecl]<Request*,byte>)&HasError,
            "UnityEngine.Rendering.AsyncGPUReadbackRequest::GetLayerDataSize_Injected" => (IntPtr)(delegate* unmanaged[Cdecl]<Request*,int>)&Size,
            "UnityEngine.Rendering.AsyncGPUReadbackRequest::GetDataRaw_Injected" => (IntPtr)(delegate* unmanaged[Cdecl]<Request*,int,IntPtr>)&Data,
            _ => IntPtr.Zero
        };
    }
}
