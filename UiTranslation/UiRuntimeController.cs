using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace TSKHook;

internal static class UiRuntimeController
{
    private static readonly CancellationTokenSource Lifetime = new();
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };
    private static readonly ConcurrentQueue<Action> MainThread = new();
    private static readonly string Root = Path.Combine(Paths.PluginPath, "TSKHook");
    private static readonly TranslationPackageStore Packages = new(Path.Combine(Root, "packages"));
    private static DateTime nextUpdate = DateTime.UtcNow.AddSeconds(15);
    private static DateTime nextPreview;
    private static DateTime previewStamp;
    private static int updateRunning;
    private static int stopped;
    private static int width, height, fps;
    private static bool preview;
    private static object manager;
    private static PropertyInfo managerVisible;
    private static bool managerSearched;

    internal static void Initialize()
    {
        width = TSKConfig.width; height = TSKConfig.height; fps = TSKConfig.FPS;
        preview = TSKConfig.Settings.LocalPreview.Value;
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("TSKHook/" + MyPluginInfo.PLUGIN_VERSION);
        UiTextCaptureService.Initialize();
        UiTranslationService.Initialize();
        LoadActiveTranslations();
        Patch.EnsureTranslationFontLoaded();
    }

    // All configuration application and Unity-facing operations happen here, on the game thread.
    internal static bool Update()
    {
        if (Volatile.Read(ref stopped) != 0) return false;
        while (MainThread.TryDequeue(out var callback)) callback();
        if (TSKConfig.ConsumeChange())
        {
            try
            {
                if (width != TSKConfig.width || height != TSKConfig.height || fps != TSKConfig.FPS)
                {
                    Window.Init();
                    width = TSKConfig.width; height = TSKConfig.height; fps = TSKConfig.FPS;
                }
                UiTextCaptureService.Configure();
                if (preview != TSKConfig.Settings.LocalPreview.Value)
                {
                    preview = TSKConfig.Settings.LocalPreview.Value;
                    LoadActiveTranslations();
                }
            }
            catch (Exception e) { Report("配置应用失败：" + e.Message); }
        }
        var action = TSKConfig.Settings.Action.Value;
        if (action != MaintenanceAction.None)
        {
            TSKConfig.Settings.Action.Value = MaintenanceAction.None;
            try
            {
                switch (action)
                {
                    case MaintenanceAction.ReloadConfiguration:
                        TSKConfig.Read();
                        Window.Init();
                        Patch.EnsureTranslationFontLoaded();
                        LoadActiveTranslations();
                        Report("配置已重新读取并应用。");
                        break;
                    case MaintenanceAction.ReloadTranslations: LoadActiveTranslations(); break;
                    case MaintenanceAction.CheckForUpdates: StartUpdate(false); break;
                    case MaintenanceAction.RollbackTranslations: StartUpdate(true); break;
                }
            }
            catch (Exception e) { Report("操作失败：" + e.Message); }
        }
        var now = DateTime.UtcNow;
        if (now >= nextUpdate)
        {
            nextUpdate = now.AddHours(1);
            if (TSKConfig.Settings.AutoUpdate.Value && !TSKConfig.Settings.LocalPreview.Value) StartUpdate(false);
        }
        if (preview && now >= nextPreview)
        {
            nextPreview = now.AddSeconds(2);
            var stamp = File.GetLastWriteTimeUtc(Path.Combine(Root, "ui_preview.jsonl"));
            if (stamp != previewStamp) { previewStamp = stamp; LoadActiveTranslations(); }
        }
        return IsManagerVisible();
    }

    internal static void LoadActiveTranslations()
    {
        try
        {
            if (TSKConfig.Settings.LocalPreview.Value)
                UiTranslationService.Reload(Path.Combine(Root, "ui_preview.jsonl"));
            else
            {
                var state = Packages.ReadState();
                UiTranslationService.Reload(state.Current != null ? Packages.PackagePath(state.Current) : Path.Combine(Root, "ui_translations.jsonl"));
            }
        }
        catch (Exception e) { Plugin.Global.Log.LogWarning("[UI Translation] Active package unavailable; previous index retained: " + e.Message); }
    }

    private static void StartUpdate(bool rollback)
    {
        if (TSKConfig.Settings.LocalPreview.Value) { Report("本地预览模式已暂停公共翻译包更新；请先关闭预览模式。"); return; }
        if (Interlocked.CompareExchange(ref updateRunning, 1, 0) != 0) return;
        var url = TSKConfig.Settings.ManifestUrl.Value.Trim();
        _ = Task.Run(async () =>
        {
            try
            {
                var changed = true;
                if (rollback) await Packages.RollbackAsync(Lifetime.Token);
                else changed = await Packages.UpdateAsync(Client, url, MyPluginInfo.PLUGIN_VERSION, Lifetime.Token);
                if (!Lifetime.IsCancellationRequested) MainThread.Enqueue(() =>
                {
                    LoadActiveTranslations();
                    Report(changed ? "翻译包已切换；已打开页面可能需要重新进入。" : "翻译包已是当前版本。");
                });
            }
            catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
            catch (Exception e)
            {
                if (!Lifetime.IsCancellationRequested) MainThread.Enqueue(() => Report("翻译包更新未完成，保留现有译文：" + e.Message));
            }
            finally { Interlocked.Exchange(ref updateRunning, 0); }
        });
    }

    private static bool IsManagerVisible()
    {
        if (!managerSearched)
        {
            managerSearched = true;
            foreach (var entry in IL2CPPChainloader.Instance.Plugins.Values)
                if (entry.Metadata.GUID == "com.bepis.bepinex.configurationmanager")
                {
                    manager = entry.Instance;
                    managerVisible = manager?.GetType().GetProperty("DisplayingWindow");
                    break;
                }
            if (manager == null) Plugin.Global.Log.LogWarning("[Config] Install ConfigurationManager IL2CPP to open settings with F1.");
        }
        return managerVisible?.GetValue(manager) is true;
    }

    private static void Report(string message)
    {
        Plugin.Global.Log.LogInfo("[UI Translation] " + message);
        Notification.Popup("TSKHook", message);
    }

    internal static void Shutdown()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0) return;
        Lifetime.Cancel();
        UiTextCaptureService.Shutdown();
        UiTranslationService.Shutdown();
    }
}
