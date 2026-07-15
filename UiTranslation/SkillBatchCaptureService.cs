using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using TKS.Network.Domain;

namespace TSKHook;

internal static class SkillBatchCaptureService
{
    private static readonly SkillBatchCatalog Catalog = new();
    private static readonly Queue<int> Pending = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string reportPath;
    private static bool running;
    private static bool requestInFlight;
    private static int total;
    private static int completed;
    private static int succeeded;
    private static int failed;
    private static int currentRequestId;
    private static string lastError;
    private static DateTime startedAtUtc;
    private static DateTime nextRequestAtUtc;
    private static DateTime requestDeadlineUtc;

    internal static bool IsRunning => running;

    internal static void Initialize()
    {
        var directory = Path.Combine(Paths.PluginPath, "TSKHook", "ui_capture");
        Directory.CreateDirectory(directory);
        reportPath = Path.Combine(directory, "skill_batch_report.json");

        Plugin.Global.Log.LogInfo(
            $"[Skill Batch] {(TSKConfig.SkillBatchCaptureEnabled ? "Available" : "Disabled")}. " +
            "Hotkey: F9; read-only requests only.");
    }

    internal static void ObserveResponse(string apiName, string formattedResponse)
    {
        if (string.Equals(apiName, ApiName.ExSkillStrengthenData.ToString(), StringComparison.Ordinal))
        {
            ObserveExSkillResponse(formattedResponse);
            return;
        }

        if (!string.Equals(apiName, ApiName.UnitList.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var added = Catalog.ObserveUnitList(formattedResponse);
            if (added > 0)
            {
                Plugin.Global.Log.LogInfo(
                    $"[Skill Batch] Cataloged {added} new owned units; total: {Catalog.UnitCount}.");
            }
        }
        catch (JsonException exception)
        {
            Plugin.Global.Log.LogWarning($"[Skill Batch] Could not inspect UnitList: {exception.Message}");
        }
    }

    internal static void ObserveExSkillRequestId(int requestId)
    {
        if (running && requestInFlight && requestId != currentRequestId)
        {
            Cancel($"Stopped because another EX skill request ({requestId}) overlapped the batch.");
            Notification.Popup("Skill capture", "Stopped: another EX skill request overlapped the batch.");
            return;
        }

        var previous = Catalog.IdentifierMode;
        var current = Catalog.ObserveKnownRequestId(requestId);
        if (previous == SkillBatchIdentifierMode.Unknown && current != SkillBatchIdentifierMode.Unknown)
        {
            Plugin.Global.Log.LogInfo(
                $"[Skill Batch] Request ID mode calibrated as {current} from normal game traffic.");
            Notification.Popup("Skill capture", "ID calibration completed. Press F9 to start batch capture.");
        }
    }

    internal static void Toggle()
    {
        if (running)
        {
            Cancel("Cancelled by user.");
            Notification.Popup("Skill capture", "Cancellation requested. The current request will finish first.");
            return;
        }

        Start();
    }

    internal static void Update()
    {
        if (requestInFlight)
        {
            if (DateTime.UtcNow >= requestDeadlineUtc)
            {
                if (running)
                {
                    CompleteCurrentRequest(false, $"Request {currentRequestId} timed out after 30 seconds.");
                }
                else
                {
                    requestInFlight = false;
                    currentRequestId = 0;
                }
            }
            return;
        }

        if (!running || DateTime.UtcNow < nextRequestAtUtc)
        {
            return;
        }

        if (Pending.Count == 0)
        {
            Finish();
            return;
        }

        currentRequestId = Pending.Dequeue();
        RequestOne(currentRequestId);
    }

    internal static void Shutdown()
    {
        if (running)
        {
            Cancel("Game is shutting down.");
        }
    }

    private static void Start()
    {
        if (requestInFlight)
        {
            Notification.Popup("Skill capture", "Wait for the previous request to finish or time out.");
            return;
        }

        if (!TSKConfig.SkillBatchCaptureEnabled)
        {
            Notification.Popup("Skill capture", "Disabled in config.json (skillBatchCapture=false). ");
            Plugin.Global.Log.LogWarning("[Skill Batch] Start refused: feature is disabled in config.json.");
            return;
        }

        if (!TSKConfig.UiCaptureEnabled)
        {
            Notification.Popup("Skill capture", "uiCapture must be enabled before the game starts.");
            Plugin.Global.Log.LogWarning("[Skill Batch] Start refused: uiCapture is disabled.");
            return;
        }

        if (Catalog.UnitCount == 0)
        {
            Notification.Popup("Skill capture", "No UnitList observed. Open the character list, then retry.");
            Plugin.Global.Log.LogWarning("[Skill Batch] Start refused: no owned units have been cataloged.");
            return;
        }

        if (Catalog.IdentifierMode == SkillBatchIdentifierMode.Unknown)
        {
            Notification.Popup("Skill capture", "Open any EX skill enhancement page once for ID calibration.");
            Plugin.Global.Log.LogWarning("[Skill Batch] Start refused: request ID mode is not calibrated.");
            return;
        }

        var requestIds = Catalog.BuildRequestIds();
        if (requestIds.Count == 0)
        {
            Notification.Popup("Skill capture", "No safe request IDs are available.");
            return;
        }

        Pending.Clear();
        foreach (var requestId in requestIds)
        {
            Pending.Enqueue(requestId);
        }

        total = requestIds.Count;
        completed = 0;
        succeeded = 0;
        failed = 0;
        currentRequestId = 0;
        lastError = null;
        startedAtUtc = DateTime.UtcNow;
        nextRequestAtUtc = DateTime.UtcNow;
        running = true;
        WriteReport("running");

        Plugin.Global.Log.LogInfo(
            $"[Skill Batch] Started {total} read-only requests using {Catalog.IdentifierMode}; " +
            $"delay: {TSKConfig.SkillBatchDelayMilliseconds} ms.");
        Notification.Popup("Skill capture", $"Started: {total} units. Press F9 again to cancel.");
    }

    private static void RequestOne(int requestId)
    {
        requestInFlight = true;
        requestDeadlineUtc = DateTime.UtcNow.AddSeconds(30);
        try
        {
            var request = new ExSkillStrengthenDataRequestEntity(requestId);
            TKS.Network.HttpClient.Post<ExSkillStrengthenDataRequestEntity,
                ExSkillStrengthenDataResultRepository>(ApiName.ExSkillStrengthenData, request, false);
            WriteReport("running");
        }
        catch (Exception exception)
        {
            CompleteCurrentRequest(false, $"Request {requestId} threw: {exception.Message}");
        }
    }

    private static void ObserveExSkillResponse(string formattedResponse)
    {
        if (!requestInFlight)
        {
            return;
        }

        if (!running)
        {
            requestInFlight = false;
            currentRequestId = 0;
            WriteReport("cancelled");
            return;
        }

        var valid = SkillBatchCatalog.HasExSkillDataResponse(formattedResponse);
        CompleteCurrentRequest(valid,
            valid ? null : $"Request {currentRequestId} returned no skill_data_list.");
    }

    private static void CompleteCurrentRequest(bool success, string error)
    {
        var requestId = currentRequestId;
        completed++;
        if (success)
        {
            succeeded++;
            Plugin.Global.Log.LogInfo(
                $"[Skill Batch] {completed}/{total} captured request ID {requestId}.");
        }
        else
        {
            failed++;
            lastError = error;
            Plugin.Global.Log.LogWarning($"[Skill Batch] {lastError}");
        }

        requestInFlight = false;
        currentRequestId = 0;
        nextRequestAtUtc = DateTime.UtcNow.AddMilliseconds(TSKConfig.SkillBatchDelayMilliseconds);
        WriteReport(running ? "running" : "cancelled");
    }

    private static void Finish()
    {
        running = false;
        WriteReport("completed");
        UiTextCaptureService.FlushNow();

        var text = $"Completed: {succeeded} succeeded, {failed} failed.";
        Plugin.Global.Log.LogInfo($"[Skill Batch] {text} Report: {reportPath}");
        Notification.Popup("Skill capture", text);
    }

    private static void Cancel(string reason)
    {
        running = false;
        Pending.Clear();
        lastError = reason;
        WriteReport("cancelled");
        UiTextCaptureService.FlushNow();
        Plugin.Global.Log.LogInfo($"[Skill Batch] {reason}");
    }

    private static void WriteReport(string status)
    {
        if (string.IsNullOrEmpty(reportPath))
        {
            return;
        }

        try
        {
            var report = new BatchReport
            {
                SchemaVersion = 1,
                Status = status,
                IdentifierMode = Catalog.IdentifierMode.ToString(),
                StartedAtUtc = startedAtUtc == default ? null : startedAtUtc.ToString("O"),
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                Total = total,
                Completed = completed,
                Succeeded = succeeded,
                Failed = failed,
                Pending = Pending.Count,
                CurrentRequestId = currentRequestId,
                LastError = lastError
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogWarning($"[Skill Batch] Could not write report: {exception.Message}");
        }
    }

    private sealed class BatchReport
    {
        public int SchemaVersion { get; set; }
        public string Status { get; set; }
        public string IdentifierMode { get; set; }
        public string StartedAtUtc { get; set; }
        public string UpdatedAtUtc { get; set; }
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Pending { get; set; }
        public int CurrentRequestId { get; set; }
        public string LastError { get; set; }
    }
}
