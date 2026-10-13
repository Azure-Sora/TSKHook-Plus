using TSKHook;
var root = Path.Combine(Path.GetTempPath(), "tsk-capture-" + Guid.NewGuid().ToString("N"));
BepInEx.Paths.PluginPath = root;
var path = Path.Combine(root, "TSKHook/ui_capture/ui_texts_v2.jsonl");
try
{
    UiTextCaptureService.Initialize();
    UiTextCaptureService.Observe("Test", "{\"name\":\"無効\"}");
    Check(!File.Exists(path), "starts disabled without recording");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, "{\"interrupted\":");
    TSKConfig.UiCaptureEnabled = true; UiTextCaptureService.Configure();
    Check(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.incomplete-*").Length == 1, "interrupted tail preserved and repaired");
    var warnings = Plugin.Global.Log.WarningCount;
    UiTextCaptureService.Observe("Test", "encoded intermediate payload");
    Check(Plugin.Global.Log.WarningCount == warnings, "non-JSON intermediate skipped quietly");
    UiTextCaptureService.Observe("Test", "{broken JSON");
    Check(Plugin.Global.Log.WarningCount == warnings + 1, "malformed object still warns");
    UiTextCaptureService.Observe("Test", "{\"name\":\"テスト\"}");
    UiTextCaptureService.Observe("Test", "{\"name\":\"テスト\"}");
    TSKConfig.UiCaptureEnabled = false; UiTextCaptureService.Configure();
    Check(File.ReadAllLines(path).Length == 1, "enable after disabled startup, dedupe and flush on disable");
    TSKConfig.UiCaptureEnabled = true; UiTextCaptureService.Configure();
    UiTextCaptureService.Observe("Test", "{\"name\":\"再開\"}");
    UiTextCaptureService.Shutdown(); UiTextCaptureService.Shutdown();
    Check(File.ReadAllLines(path).Length == 2, "reenable and idempotent shutdown flush");
    UiTextCaptureService.Observe("Test", "{\"name\":\"終了\"}");
    Check(File.ReadAllLines(path).Length == 2, "no observations after shutdown");
    Console.WriteLine("PASS: 7 capture lifecycle assertions (managed substitutes, not game execution)");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
namespace BepInEx { public static class Paths { public static string PluginPath; } }
namespace TSKHook
{
    public static class TSKConfig { public static bool UiCaptureEnabled; public static int UiCaptureFlushSeconds = 300; }
    public static class Plugin { public static class Global { public static readonly FixtureLog Log = new(); } }
    public sealed class FixtureLog
    {
        public void LogInfo(string text) { }
        public int WarningCount;
        public void LogWarning(string text) { WarningCount++; Console.WriteLine(text); }
        public void LogError(string text) => throw new Exception(text);
    }
}
