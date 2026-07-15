using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using BepInEx;

namespace TSKHook;

internal static class UiTranslationService
{
    private static readonly object ReloadLock = new();
    private static readonly ConcurrentDictionary<string, byte> LoggedApis = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static UiTranslationIndex translations = UiTranslationIndex.Empty;
    private static string assetPath;
    private static long translatedResponses;
    private static long translatedTexts;
    private static long inspectedResponses;
    private static long totalTransformMilliseconds;
    private static long renderedTextFallbacks;
    private static int errorCount;
    private static int slowLogCount;
    private static int shutdownLogged;

    internal static void Initialize()
    {
        assetPath = Path.Combine(Paths.PluginPath, "TSKHook", "ui_translations.jsonl");
        Reload();
    }

    internal static void Reload()
    {
        lock (ReloadLock)
        {
            var loaded = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = Path.Combine(Paths.PluginPath, "TSKHook", "ui_translations.jsonl");
            }

            try
            {
                if (!File.Exists(assetPath))
                {
                    Volatile.Write(ref translations, UiTranslationIndex.Empty);
                    Plugin.Global.Log.LogWarning($"[UI Translation] Runtime asset not found: {assetPath}");
                    return;
                }

                var invalidRows = 0;
                foreach (var line in File.ReadLines(assetPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var row = JsonSerializer.Deserialize<RuntimeRow>(line, JsonOptions);
                        if (row?.SchemaVersion != 1 || string.IsNullOrEmpty(row.Key) ||
                            string.IsNullOrEmpty(row.SourceHash) || string.IsNullOrWhiteSpace(row.Translation))
                        {
                            invalidRows++;
                            continue;
                        }

                        loaded[row.Key] = new UiRuntimeTranslation(row.SourceHash, row.Translation);
                    }
                    catch (JsonException)
                    {
                        invalidRows++;
                    }
                }

                var index = new UiTranslationIndex(loaded);
                Volatile.Write(ref translations, index);
                LoggedApis.Clear();
                Plugin.Global.Log.LogInfo(
                    $"[UI Translation] Loaded {index.ExactCount} entries from {assetPath}. " +
                    $"Safe source-hash fallbacks: {index.UniqueSourceHashCount}; " +
                    $"ambiguous hashes excluded: {index.AmbiguousSourceHashCount}; invalid rows: {invalidRows}.");
            }
            catch (Exception exception)
            {
                Plugin.Global.Log.LogError($"[UI Translation] Failed to load runtime asset: {exception}");
            }
        }
    }

    internal static string TranslateResponse(string apiName, string formattedResponse)
    {
        if (!TSKConfig.TranslationEnabled || !TSKConfig.UiTranslationEnabled ||
            string.IsNullOrWhiteSpace(formattedResponse))
        {
            return formattedResponse;
        }

        var trimmed = formattedResponse.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return formattedResponse;
        }

        var snapshot = Volatile.Read(ref translations);
        if (snapshot.ExactCount == 0)
        {
            return formattedResponse;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var replacements = 0;
            var hashFallbacks = 0;
            try
            {
                if (!UiJsonTranslator.TryTranslate(apiName, formattedResponse, snapshot,
                        out var result, out replacements, out hashFallbacks))
                {
                    return formattedResponse;
                }

                Interlocked.Increment(ref translatedResponses);
                Interlocked.Add(ref translatedTexts, replacements);
                if (LoggedApis.TryAdd(apiName, 0))
                {
                    Plugin.Global.Log.LogInfo(
                        $"[UI Translation] {apiName}: replaced {replacements} text values " +
                        $"({hashFallbacks} by safe source-hash fallback; first translated response). ");
                }
                return result;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Increment(ref inspectedResponses);
                Interlocked.Add(ref totalTransformMilliseconds, stopwatch.ElapsedMilliseconds);
                if (stopwatch.ElapsedMilliseconds >= 250 && Interlocked.Increment(ref slowLogCount) <= 50)
                {
                    Plugin.Global.Log.LogInfo(
                        $"[UI Translation Perf] {apiName}: {formattedResponse.Length} chars, " +
                        $"{replacements} replacements ({hashFallbacks} hash fallbacks), " +
                        $"{stopwatch.ElapsedMilliseconds} ms.");
                }
            }
        }
        catch (JsonException exception)
        {
            LogLimitedError(apiName, exception.Message);
            return formattedResponse;
        }
        catch (Exception exception)
        {
            LogLimitedError(apiName, exception.ToString());
            return formattedResponse;
        }
    }

    internal static bool TryTranslateRenderedText(string source, out string translation)
    {
        translation = null;
        if (!TSKConfig.TranslationEnabled || !TSKConfig.UiTranslationEnabled)
        {
            return false;
        }

        var snapshot = Volatile.Read(ref translations);
        if (!UiRenderedTextTranslator.TryTranslate(source, snapshot, out translation))
        {
            return false;
        }

        Interlocked.Increment(ref renderedTextFallbacks);
        return true;
    }

    internal static void Shutdown()
    {
        if (Interlocked.Exchange(ref shutdownLogged, 1) != 0)
        {
            return;
        }

        Plugin.Global.Log.LogInfo(
            $"[UI Translation] Stopped. Translated responses: {Interlocked.Read(ref translatedResponses)}, " +
            $"text values: {Interlocked.Read(ref translatedTexts)}, " +
            $"inspected responses: {Interlocked.Read(ref inspectedResponses)}, " +
            $"transform time: {Interlocked.Read(ref totalTransformMilliseconds)} ms, " +
            $"rendered text fallbacks: {Interlocked.Read(ref renderedTextFallbacks)}, " +
            $"TMP fonts configured: {UiFontFallbackPatch.ProcessedFontCount}, " +
            $"unreadable atlas insertions skipped: {UiFontFallbackPatch.SkippedUnreadableAtlasInsertions}.");
    }

    private static void LogLimitedError(string apiName, string message)
    {
        if (Interlocked.Increment(ref errorCount) <= 5)
        {
            Plugin.Global.Log.LogWarning($"[UI Translation] Could not translate {apiName}: {message}");
        }
    }

    private sealed class RuntimeRow
    {
        public int SchemaVersion { get; set; }
        public string Key { get; set; }
        public string SourceHash { get; set; }
        public string Translation { get; set; }
    }
}
