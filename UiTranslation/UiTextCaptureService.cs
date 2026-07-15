using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using BepInEx;

namespace TSKHook;

internal static class UiTextCaptureService
{
    private static readonly ConcurrentDictionary<string, byte> Seen = new();
    private static readonly ConcurrentQueue<CaptureRecord> Pending = new();
    private static readonly object FlushLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static Timer flushTimer;
    private static string outputDirectory;
    private static string outputFile;
    private static int initialized;
    private static int shuttingDown;
    private static int parseErrorCount;
    private static long observedResponses;
    private static long capturedTexts;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref initialized, 1) != 0 || !TSKConfig.UiCaptureEnabled)
        {
            return;
        }

        outputDirectory = Path.Combine(Paths.PluginPath, "TSKHook", "ui_capture");
        outputFile = Path.Combine(outputDirectory, "ui_texts_v2.jsonl");
        Directory.CreateDirectory(outputDirectory);
        LoadExistingKeys();

        var period = TimeSpan.FromSeconds(Math.Max(1, TSKConfig.UiCaptureFlushSeconds));
        flushTimer = new Timer(_ => Flush(), null, period, period);

        Plugin.Global.Log.LogInfo($"[UI Capture] Enabled. Output: {outputFile}");
        Plugin.Global.Log.LogInfo("[UI Capture] Only Japanese-looking strings are saved; raw API responses are not written.");
    }

    internal static void Observe(string apiName, string formattedResponse)
    {
        if (!TSKConfig.UiCaptureEnabled || Volatile.Read(ref initialized) == 0 ||
            Volatile.Read(ref shuttingDown) != 0 || string.IsNullOrWhiteSpace(formattedResponse))
        {
            return;
        }

        Interlocked.Increment(ref observedResponses);

        try
        {
            using var document = JsonDocument.Parse(formattedResponse);
            ExtractStrings(apiName, "$", document.RootElement,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }
        catch (JsonException exception)
        {
            var errorNumber = Interlocked.Increment(ref parseErrorCount);
            if (errorNumber <= 5)
            {
                Plugin.Global.Log.LogWarning(
                    $"[UI Capture] FormatData result for {apiName} was not JSON: {exception.Message}");
            }
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogWarning($"[UI Capture] Failed to inspect {apiName}: {exception}");
        }
    }

    internal static void Shutdown()
    {
        if (Interlocked.Exchange(ref shuttingDown, 1) != 0)
        {
            return;
        }

        flushTimer?.Dispose();
        flushTimer = null;
        Flush();

        if (Volatile.Read(ref initialized) != 0 && TSKConfig.UiCaptureEnabled)
        {
            Plugin.Global.Log.LogInfo(
                $"[UI Capture] Stopped. Responses: {Interlocked.Read(ref observedResponses)}, " +
                $"unique texts: {Interlocked.Read(ref capturedTexts)}, pending: {Pending.Count}.");
        }
    }

    private static void ExtractStrings(string apiName, string path, JsonElement element,
        IReadOnlyDictionary<string, string> inheritedContext)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var objectContext = UiJsonContext.Extend(inheritedContext, element);
                foreach (var property in element.EnumerateObject())
                {
                    ExtractStrings(apiName, path + "." + property.Name, property.Value, objectContext);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ExtractStrings(apiName, path + "[]", item, inheritedContext);
                }
                break;

            case JsonValueKind.String:
                var source = element.GetString();
                if (CaptureTextFilter.ShouldCapture(path, source))
                {
                    AddRecord(apiName, path, source, inheritedContext);
                }
                break;
        }
    }

    private static void AddRecord(string apiName, string path, string source,
        IReadOnlyDictionary<string, string> context)
    {
        var sourceHash = Sha256(source);
        var key = BuildKey(apiName, path, sourceHash, context);
        if (!Seen.TryAdd(key, 0))
        {
            return;
        }

        Pending.Enqueue(new CaptureRecord
        {
            SchemaVersion = 2,
            ApiName = apiName,
            Path = path,
            Context = context.Count == 0
                ? null
                : ToSortedDictionary(context),
            Source = source,
            SourceHash = sourceHash,
            FirstSeenUtc = DateTime.UtcNow.ToString("O")
        });
        Interlocked.Increment(ref capturedTexts);
    }

    private static string BuildKey(string apiName, string path, string sourceHash,
        IReadOnlyDictionary<string, string> context)
    {
        var builder = new StringBuilder(apiName)
            .Append('\u001f').Append(path)
            .Append('\u001f').Append(sourceHash);

        foreach (var pair in ToSortedDictionary(context))
        {
            builder.Append('\u001f').Append(pair.Key).Append('=').Append(pair.Value);
        }

        return builder.ToString();
    }

    private static SortedDictionary<string, string> ToSortedDictionary(
        IReadOnlyDictionary<string, string> source)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void Flush()
    {
        if (Pending.IsEmpty || string.IsNullOrEmpty(outputFile))
        {
            return;
        }

        lock (FlushLock)
        {
            if (Pending.IsEmpty)
            {
                return;
            }

            var records = new List<CaptureRecord>();
            try
            {
                Directory.CreateDirectory(outputDirectory);
                while (Pending.TryDequeue(out var record))
                {
                    records.Add(record);
                }

                if (records.Count > 0)
                {
                    var lines = new List<string>(records.Count);
                    foreach (var record in records)
                    {
                        lines.Add(JsonSerializer.Serialize(record, JsonOptions));
                    }

                    File.AppendAllLines(outputFile, lines, new UTF8Encoding(false));
                    Plugin.Global.Log.LogInfo($"[UI Capture] Saved {lines.Count} new texts.");
                }
            }
            catch (Exception exception)
            {
                foreach (var record in records)
                {
                    Pending.Enqueue(record);
                }

                Plugin.Global.Log.LogError($"[UI Capture] Failed to write capture file: {exception}");
            }
        }
    }

    private static void LoadExistingKeys()
    {
        if (!File.Exists(outputFile))
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(outputFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var record = JsonSerializer.Deserialize<CaptureRecord>(line, JsonOptions);
                if (record == null || string.IsNullOrEmpty(record.ApiName) || string.IsNullOrEmpty(record.Path) ||
                    string.IsNullOrEmpty(record.SourceHash))
                {
                    continue;
                }

                IReadOnlyDictionary<string, string> context = record.Context ??
                    (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
                Seen.TryAdd(BuildKey(record.ApiName, record.Path, record.SourceHash, context), 0);
            }

            Plugin.Global.Log.LogInfo($"[UI Capture] Loaded {Seen.Count} existing capture keys.");
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogWarning($"[UI Capture] Could not read existing capture file: {exception.Message}");
        }
    }

    private sealed class CaptureRecord
    {
        public int SchemaVersion { get; set; }
        public string ApiName { get; set; }
        public string Path { get; set; }
        public SortedDictionary<string, string> Context { get; set; }
        public string Source { get; set; }
        public string SourceHash { get; set; }
        public string FirstSeenUtc { get; set; }
    }
}
