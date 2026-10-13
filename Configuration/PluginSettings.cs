using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using BepInEx.Configuration;

namespace TSKHook;

public enum MaintenanceAction
{
    [Description("无操作")] None,
    [Description("重新读取并应用配置")] ReloadConfiguration,
    [Description("重新加载本地译文")] ReloadTranslations,
    [Description("检查并应用翻译更新")] CheckForUpdates,
    [Description("回退上一版翻译包")] RollbackTranslations
}

// Consumed by ConfigurationManager through reflection; no hard assembly dependency.
internal sealed class ConfigurationManagerAttributes
{
    public string DispName;
    public int? Order;
    public bool? IsAdvanced;
}

public sealed class PluginSettings
{
    public ConfigFile File { get; }
    public ConfigEntry<double> Speed { get; }
    public ConfigEntry<int> Fps { get; }
    public ConfigEntry<bool> Translation { get; }
    public ConfigEntry<int> Width { get; }
    public ConfigEntry<int> Height { get; }
    public ConfigEntry<float> Zoom { get; }
    public ConfigEntry<bool> UiTranslation { get; }
    public ConfigEntry<bool> Capture { get; }
    public ConfigEntry<int> FlushSeconds { get; }
    public ConfigEntry<bool> AutoUpdate { get; }
    public ConfigEntry<string> ManifestUrl { get; }
    public ConfigEntry<bool> LocalPreview { get; }
    public ConfigEntry<MaintenanceAction> Action { get; }

    public PluginSettings(ConfigFile config, string legacyPath, Action<string> warning)
    {
        File = config;
        var hadConfig = System.IO.File.Exists(config.ConfigFilePath);
        config.SaveOnConfigSet = false;
        Speed = Bind("General", "SpeedStep", 0.5, "变速步长", "F7/F8 每次调整的速度。即时生效。", new AcceptableValueRange<double>(0.01, 10));
        Fps = Bind("General", "FPS", 60, "目标帧率", "窗口刷新及原有 FPS 限制设置。即时生效。", new AcceptableValueRange<int>(30, 360));
        Width = Bind("General", "WindowWidth", 1280, "窗口宽度", "修改后下一帧调整窗口。", new AcceptableValueRange<int>(320, 7680));
        Height = Bind("General", "WindowHeight", 720, "窗口高度", "修改后下一帧调整窗口。", new AcceptableValueRange<int>(240, 4320));
        Zoom = Bind("General", "CharacterZoom", 1f, "立绘缩放", "原立绘缩放功能；下次刷新立绘时应用。", new AcceptableValueRange<float>(0.1f, 10f));
        Translation = Bind("Translation", "Enabled", true, "翻译总开关", "与 F11 使用同一设置；已加载页面可能需要重新打开。", null);
        UiTranslation = Bind("Translation", "UIEnabled", true, "UI 文本汉化", "总翻译开启时有效；后续文本刷新生效。", null);
        Capture = Bind("Capture", "Enabled", false, "捕获新文本", "结构化捕获并增量保存；可随时开关。", null);
        FlushSeconds = Bind("Capture", "FlushSeconds", 5, "写入间隔（秒）", "批量写入捕获记录；关闭捕获时立即保存。", new AcceptableValueRange<int>(1, 300));
        AutoUpdate = Bind("Updates", "Enabled", true, "自动更新译文", "启动后及每小时检查；失败保留本地译文。", null);
        ManifestUrl = Bind("Updates", "ManifestUrl", "https://github.com/Azure-Sora/TSKHook-UITranslations/releases/latest/download/manifest.json", "更新清单地址", "HTTPS 翻译包清单；修改后下次检查使用。", null);
        LocalPreview = Bind("Updates", "LocalPreview", false, "本地预览模式", "维护者专用：读取 ui_preview.jsonl 并监听变化；暂停公共包更新。", null);
        Action = Bind("Maintenance", "Action", MaintenanceAction.None, "执行操作", "选择后执行一次并自动恢复无操作；重新读取配置包括原 F1 的窗口与译文重载。", null);
        if (!hadConfig && System.IO.File.Exists(legacyPath)) ImportLegacy(legacyPath, warning);
        Action.Value = MaintenanceAction.None;
        config.Save();
        config.SaveOnConfigSet = true;
    }

    private ConfigEntry<T> Bind<T>(string section, string key, T value, string name, string description, AcceptableValueBase range) =>
        File.Bind(section, key, value, new ConfigDescription(description, range,
            new ConfigurationManagerAttributes { DispName = name, Order = 0, IsAdvanced = false }));

    private void ImportLegacy(string path, Action<string> warning)
    {
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = doc.RootElement;
            Import(root, "speed", Speed, warning);
            Import(root, "fps", Fps, warning);
            Import(root, "translation", Translation, warning);
            Import(root, "width", Width, warning);
            Import(root, "height", Height, warning);
            Import(root, "zoom", Zoom, warning);
            Import(root, "uiTranslation", UiTranslation, warning);
            Import(root, "uiCapture", Capture, warning);
            Import(root, "uiCaptureFlushSeconds", FlushSeconds, warning);
        }
        catch (Exception e) when (e is JsonException or IOException)
        { warning("Legacy config could not be imported; original file preserved: " + e.Message); }
    }

    private static void Import<T>(JsonElement root, string name, ConfigEntry<T> entry, Action<string> warning)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value)) return;
        try
        {
            var parsed = value.Deserialize<T>();
            if (entry.Description.AcceptableValues?.IsValid(parsed) == false) throw new JsonException("Outside allowed range.");
            entry.Value = parsed;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        { warning("Legacy config field '" + name + "' is invalid; default retained."); }
    }
}
