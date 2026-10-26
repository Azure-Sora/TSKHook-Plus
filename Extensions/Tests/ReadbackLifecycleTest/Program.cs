using TSKHook;
using UnityEngine;

var checks = 0;
void Check(bool condition,string message) { checks++; if (!condition) throw new Exception(message); }
SystemInfo.supportsAsyncGPUReadback = !args.Contains("--fallback");
var source = new Texture(1,2,2);
var a = UiTextureReadback.Get(source);
var b = UiTextureReadback.Get(source);
Check(ReferenceEquals(a,b),"same atlas shares pending request");
UiTextureReadback.Update(false);
Check(!a.IsCompleted && NativeFixture.Submissions == 0 && UiImagePixels.Reads == 0,"battle blocks new readback work");
UiTextureReadback.Update(true);
if (SystemInfo.supportsAsyncGPUReadback)
{
    Check(!a.IsCompleted && NativeFixture.Submissions == 1,"submission returns before GPU completion");
    UiTextureReadback.Update(true);
    Check(!a.IsCompleted && RenderTexture.Releases == 0,"in-flight render target remains retained");
    NativeFixture.Done = true;
    UiTextureReadback.Update(false);
    Check(a.IsCompletedSuccessfully && a.Result.Bytes.SequenceEqual(NativeFixture.Pixels),"completion is drained even during battle");
    Check(RenderTexture.Releases == 1,"completed target released once");
}
else Check(a.IsCompletedSuccessfully && UiImagePixels.Reads == 1,"fallback readback only outside battle");
Check(ReferenceEquals(a,UiTextureReadback.Get(source)),"successful atlas snapshot reused");
UiTextureReadback.Clear();
NativeFixture.Done = false;
var old = UiTextureReadback.Get(source);
UiTextureReadback.Update(true);
UiTextureReadback.Clear();
var fresh = UiTextureReadback.Get(source);
Check(!ReferenceEquals(old,fresh),"reload cannot reuse stale pixel task");
if (SystemInfo.supportsAsyncGPUReadback)
{
    var releases = RenderTexture.Releases;
    UiTextureReadback.Update(false);
    Check(RenderTexture.Releases == releases && !old.IsCompleted,"cancel does not free GPU resources early");
    NativeFixture.Done = true;
    UiTextureReadback.Update(false);
    Check(old.IsCanceled && RenderTexture.Releases == releases+1,"discard old readback after GPU finishes");
    UiTextureReadback.Update(true);
    NativeFixture.Error = true;
    UiTextureReadback.Update(false);
    Check(fresh.IsFaulted && fresh.Exception.GetBaseException() is InvalidDataException,"GPU error fails closed");
    Check(ReferenceEquals(fresh,UiTextureReadback.Get(source)),"failed source is not retried every frame");
    NativeFixture.Error = false;
}
else
{
    UiTextureReadback.Update(true);
    Check(fresh.IsCompletedSuccessfully,"compatibility reload prepares a fresh result");
}
UiTextureReadback.Clear();
var waiting = UiTextureReadback.Get(new Texture(2,2,2));
UiTextureReadback.Clear();
Check(waiting.IsCanceled,"queued but unsubmitted work canceled immediately");
Console.WriteLine($"PASS: {checks} readback lifecycle assertions ({(SystemInfo.supportsAsyncGPUReadback ? "simulated native async" : "compatibility")}; not an engine integration test)");
