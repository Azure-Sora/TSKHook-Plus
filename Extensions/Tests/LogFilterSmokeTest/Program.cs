using BepInEx.Logging;
using HarmonyLib;
using TSKHook;

var log = File.ReadAllText(args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "raycast-exception.txt") : args[0]);
const string start = "During invoking native->managed trampoline";
var begin = log.IndexOf(start, StringComparison.Ordinal);
var end = log.IndexOf("\n[", begin, StringComparison.Ordinal);
var fixture = log[begin..(end < 0 ? log.Length : end)].TrimEnd();
var listener = new RecordingListener();
Logger.Listeners.Add(listener);
using var source = Logger.CreateLogSource("Il2CppInterop");
using var other = Logger.CreateLogSource("OtherPlugin");
var harmony = Harmony.CreateAndPatchAll(typeof(KnownGameLogFilter));
try
{
    source.LogError(fixture);
    Check(listener.Events.Count == 1 && listener.Events[0].Source.SourceName == "TSKHook", "first match emits summary only");
    source.LogError(fixture);
    Check(listener.Events.Count == 1, "repeat match suppressed");
    source.LogError(fixture.Replace("\r\n", "\n"));
    Check(listener.Events.Count == 1, "LF signature suppressed");
    source.Log(LogLevel.Error, $"{fixture}");
    Check(listener.Events.Count == 1, "interpolated overload suppressed");
    other.LogError(fixture);
    source.LogWarning(fixture);
    source.LogError(fixture.Replace("System.NullReferenceException", "System.ArgumentException"));
    source.LogError(fixture.Replace("RaycastMask.IsRaycastLocationValid", "OtherMask.IsRaycastLocationValid"));
    source.LogError(fixture.Replace("Sprite.get_textureRect", "Sprite.get_texture"));
    source.LogError(fixture.Replace("EventSystem.Update", "EventSystem.Other"));
    source.LogError("unrelated error");
    source.LogError(new InvalidOperationException("keep exception objects"));
    Check(listener.Events.Count == 9, "all eight unrelated events preserved");
    Console.WriteLine("PASS: real Harmony/BepInEx log dispatch: suppression, one-time summary, LF, eight negative cases.");
}
finally { harmony.UnpatchSelf(); Logger.Listeners.Remove(listener); }
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
sealed class RecordingListener : ILogListener
{
    public LogLevel LogLevelFilter => LogLevel.All;
    public readonly List<LogEventArgs> Events = new();
    public void LogEvent(object sender, LogEventArgs eventArgs) => Events.Add(eventArgs);
    public void Dispose() { }
}
namespace TSKHook
{
    public static class Plugin { public static class Global { public static readonly ManualLogSource Log = Logger.CreateLogSource("TSKHook"); } }
}
