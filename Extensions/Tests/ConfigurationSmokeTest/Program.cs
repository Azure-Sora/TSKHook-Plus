using BepInEx.Configuration;
using TSKHook;

var directory = Path.Combine(Path.GetTempPath(), "tsk-config-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var count = 0;
try
{
    var legacy = Path.Combine(directory, "config.json");
    File.WriteAllText(legacy, """{"speed":0.7,"fps":120,"width":1600,"height":900,"zoom":1.3,"translation":false,"uiTranslation":false,"uiCapture":true,"uiCaptureFlushSeconds":2}""");
    var cfg = Path.Combine(directory, "plugin.cfg");
    var warnings = new List<string>();
    var settings = new PluginSettings(new ConfigFile(cfg, false), legacy, warnings.Add);
    Check(settings.Speed.Value == 0.7 && settings.Fps.Value == 120, "legacy speed/fps");
    Check(settings.Width.Value == 1600 && settings.Height.Value == 900, "legacy size");
    Check(Math.Abs(settings.Zoom.Value - 1.3f) < .001f, "legacy zoom");
    Check(!settings.Translation.Value && !settings.UiTranslation.Value && settings.Capture.Value, "legacy toggles");
    Check(settings.FlushSeconds.Value == 2 && warnings.Count == 0, "legacy flush");
    Check(File.Exists(legacy), "legacy file preserved");
    Check(settings.Script.Value == ChineseScript.Original, "conversion defaults to original translation");
    Check(settings.ManifestUrl.Value != settings.ImageManifestUrl.Value && settings.ImageManifestUrl.Value.Contains("image-channel"), "separate resource manifests");
    settings.Script.Value = ChineseScript.Traditional;
    settings.Translation.Value = true;
    settings.Width.Value = 1920;
    settings.Action.Value = MaintenanceAction.ReloadConfiguration;
    var reopened = new PluginSettings(new ConfigFile(cfg, false), legacy, warnings.Add);
    Check(reopened.Translation.Value && reopened.Width.Value == 1920, "new settings survive restart, old JSON not reimported");
    Check(reopened.Action.Value == MaintenanceAction.None, "persisted action cannot replay at startup");
    Check(reopened.Script.Value == ChineseScript.Traditional, "script selection survives configuration reload");
    var edited = File.ReadAllText(cfg).Replace("WindowWidth = 1920", "WindowWidth = 1800");
    File.WriteAllText(cfg, edited);
    reopened.Action.Value = MaintenanceAction.ReloadConfiguration;
    reopened.LastResult.Value = "status must not save stale settings";
    Check(File.ReadAllText(cfg).Contains("WindowWidth = 1800"), "maintenance action does not overwrite external edits before reload");
    reopened.Action.Value = MaintenanceAction.None;
    reopened.Reload();
    Check(reopened.Width.Value == 1800, "disk reload updates same config entries");
    File.WriteAllText(legacy, """{"width":-1,"height":"bad","fps":144}""");
    var bad = new PluginSettings(new ConfigFile(Path.Combine(directory, "bad.cfg"), false), legacy, warnings.Add);
    Check(bad.Width.Value == 1280 && bad.Height.Value == 720 && bad.Fps.Value == 144 && warnings.Count == 2, "invalid fields retain defaults without losing valid fields");
    Console.WriteLine($"PASS: {count} real BepInEx configuration assertions");
}
finally { Directory.Delete(directory, true); }
void Check(bool valid, string message) { count++; if (!valid) throw new Exception(message); }
