using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using TKS.Network.Domain;

namespace TSKHook;

internal static class SkillBatchCaptureService
{
    private const string ExSkillPath = "/api/unit/exSkillStrengthenData";
    private static readonly SkillBatchCatalog Catalog = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string reportPath;
    private static bool waitingForNormalRequest;
    private static int observedRequestId;
    private static string requestEncoding;
    private static string lastError;

    internal static bool IsRunning => waitingForNormalRequest;

    internal static void Initialize()
    {
        var directory = Path.Combine(Paths.PluginPath, "TSKHook", "ui_capture");
        Directory.CreateDirectory(directory);
        reportPath = Path.Combine(directory, "skill_batch_report.json");

        Plugin.Global.Log.LogInfo(
            "[Skill Batch] Temporarily disabled for safety; no active or passive network hooks are installed.");
    }

    internal static void ObserveResponse(string apiName, string formattedResponse)
    {
        if (string.Equals(apiName, ApiName.ExSkillStrengthenData.ToString(), StringComparison.Ordinal))
        {
            if (waitingForNormalRequest)
            {
                Plugin.Global.Log.LogInfo(
                    "[Skill Batch] Normal EX skill response observed after passive request capture.");
                UiTextCaptureService.FlushNow();
            }
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

    internal static void ObserveOutgoingRequest(string url, string body)
    {
        if (!waitingForNormalRequest || string.IsNullOrEmpty(url) ||
            url.IndexOf(ExSkillPath, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        if (!TryDecodeRequestId(body, out var requestId, out var encoding))
        {
            lastError = "Normal EX request was observed, but its unit_id could not be decoded.";
            WriteReport("request_observed_unreadable");
            Plugin.Global.Log.LogWarning($"[Skill Batch] {lastError}");
            Notification.Popup("Skill capture", "Request observed, but unit_id was unreadable. Check the log.");
            return;
        }

        observedRequestId = requestId;
        requestEncoding = encoding;
        var mode = Catalog.ObserveKnownRequestId(requestId);
        waitingForNormalRequest = false;
        lastError = mode == SkillBatchIdentifierMode.Unknown
            ? $"Observed normal request ID {requestId}, but it matches neither known UnitList ID field."
            : null;

        WriteReport(mode == SkillBatchIdentifierMode.Unknown
            ? "request_id_unrecognized"
            : "passive_calibration_completed");

        if (mode == SkillBatchIdentifierMode.Unknown)
        {
            Plugin.Global.Log.LogWarning($"[Skill Batch] {lastError} Encoding: {encoding}.");
            Notification.Popup("Skill capture", $"Observed request ID {requestId}; check LogOutput.log.");
            return;
        }

        Plugin.Global.Log.LogInfo(
            $"[Skill Batch] Passive calibration completed: request ID {requestId}, " +
            $"mode {mode}, encoding {encoding}. No active request was sent.");
        Notification.Popup("Skill capture", $"Passive calibration completed: {mode}.");
    }

    internal static void Toggle()
    {
        Plugin.Global.Log.LogWarning(
            "[Skill Batch] F9 ignored: feature is temporarily disabled for game stability.");
        Notification.Popup("Skill capture", "Temporarily disabled for game stability.");
    }

    internal static void Update()
    {
        // Active network requests are intentionally disabled. Normal game traffic drives calibration.
    }

    internal static void Shutdown()
    {
        if (!waitingForNormalRequest)
        {
            return;
        }

        waitingForNormalRequest = false;
        lastError = "Game shut down while passive calibration was waiting.";
        WriteReport("cancelled");
    }

    private static bool TryDecodeRequestId(string body, out int requestId, out string encoding)
    {
        if (SkillBatchCatalog.TryExtractRequestId(body, out requestId))
        {
            encoding = "plain-json";
            return true;
        }

        foreach (var candidate in new[]
                 {
                     (Name: "http-key", Key: TKS.Network.HttpClient.key),
                     (Name: "http-key2", Key: TKS.Network.HttpClient.key2)
                 })
        {
            if (string.IsNullOrEmpty(candidate.Key))
            {
                continue;
            }

            try
            {
                var decoded = TKS.Network.HttpClient.Decrypt(body, candidate.Key);
                if (SkillBatchCatalog.TryExtractRequestId(decoded, out requestId))
                {
                    encoding = candidate.Name;
                    return true;
                }
            }
            catch (Exception exception)
            {
                Plugin.Global.Log.LogDebug(
                    $"[Skill Batch] Passive request decode with {candidate.Name} failed: {exception.Message}");
            }
        }

        requestId = 0;
        encoding = "unknown";
        return false;
    }

    private static void WriteReport(string status)
    {
        if (string.IsNullOrEmpty(reportPath))
        {
            return;
        }

        try
        {
            var report = new PassiveCalibrationReport
            {
                SchemaVersion = 2,
                Status = status,
                IdentifierMode = Catalog.IdentifierMode.ToString(),
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                ObservedRequestId = observedRequestId,
                RequestEncoding = requestEncoding,
                LastError = lastError
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogWarning($"[Skill Batch] Could not write report: {exception.Message}");
        }
    }

    private sealed class PassiveCalibrationReport
    {
        public int SchemaVersion { get; set; }
        public string Status { get; set; }
        public string IdentifierMode { get; set; }
        public string UpdatedAtUtc { get; set; }
        public int ObservedRequestId { get; set; }
        public string RequestEncoding { get; set; }
        public string LastError { get; set; }
    }
}
