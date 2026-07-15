using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TSKHook;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    return RunSelfTest();
}

var input = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".local/ui_texts_v2.jsonl");
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "translations/ui/source/ui_source.jsonl");
var outputDirectory = Path.GetDirectoryName(output)!;
var conflictsFile = Path.Combine(outputDirectory, "ui_conflicts.jsonl");
var summaryFile = Path.Combine(outputDirectory, "compile_summary.json");
var categoryDirectory = Path.Combine(outputDirectory, "by_category");

if (!File.Exists(input))
{
    Console.Error.WriteLine($"Input not found: {input}");
    return 2;
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};
var summaryOptions = new JsonSerializerOptions(jsonOptions) { WriteIndented = true };
var groups = new Dictionary<string, Dictionary<string, Aggregate>>(StringComparer.Ordinal);
var previousLoad = LoadPreviousEntries(output, categoryDirectory, jsonOptions);
if (previousLoad.Errors.Count > 0)
{
    Console.Error.WriteLine("Existing category translations are invalid; no output files were changed:");
    foreach (var error in previousLoad.Errors)
    {
        Console.Error.WriteLine("  " + error);
    }

    return 3;
}

var previousEntries = previousLoad.Entries;
var parseErrors = 0;
var inputRows = 0;

foreach (var line in File.ReadLines(input))
{
    inputRows++;
    CaptureRecord record;
    try
    {
        record = JsonSerializer.Deserialize<CaptureRecord>(line, jsonOptions);
    }
    catch (JsonException)
    {
        parseErrors++;
        continue;
    }

    if (record?.SchemaVersion != 2 || string.IsNullOrEmpty(record.Source) ||
        string.IsNullOrEmpty(record.SourceHash) || string.IsNullOrEmpty(record.Path))
    {
        parseErrors++;
        continue;
    }

    var descriptor = BuildKey(record);
    if (!groups.TryGetValue(descriptor.Key, out var sourceGroups))
    {
        sourceGroups = new Dictionary<string, Aggregate>(StringComparer.Ordinal);
        groups[descriptor.Key] = sourceGroups;
    }

    if (!sourceGroups.TryGetValue(record.SourceHash, out var aggregate))
    {
        aggregate = new Aggregate(record, descriptor);
        sourceGroups[record.SourceHash] = aggregate;
    }

    aggregate.Occurrences++;
    aggregate.Origins.Add(record.ApiName + ":" + record.Path);
}

var entries = new List<SourceEntry>();
var conflicts = new List<SourceEntry>();
foreach (var baseKey in groups.Keys.OrderBy(value => value, StringComparer.Ordinal))
{
    var sourceGroups = groups[baseKey];
    var hasConflict = sourceGroups.Count > 1;
    foreach (var aggregate in sourceGroups.Values.OrderBy(value => value.Record.SourceHash, StringComparer.Ordinal))
    {
        var entry = aggregate.ToEntry(hasConflict
            ? baseKey + "#" + aggregate.Record.SourceHash[..8]
            : baseKey,
            hasConflict ? "conflict" : "new");
        ApplyPreviousTranslation(entry, previousEntries);
        entries.Add(entry);
        if (hasConflict)
        {
            conflicts.Add(entry);
        }
    }
}

entries = entries
    .OrderBy(entry => entry.Category, StringComparer.Ordinal)
    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
    .ToList();

Directory.CreateDirectory(outputDirectory);
WriteJsonLines(output, entries, jsonOptions);
WriteJsonLines(conflictsFile, conflicts, jsonOptions);
Directory.CreateDirectory(categoryDirectory);
foreach (var categoryGroup in entries.GroupBy(entry => entry.Category))
{
    WriteJsonLines(Path.Combine(categoryDirectory, categoryGroup.Key + ".jsonl"), categoryGroup, jsonOptions);
}

var categoryCounts = entries
    .GroupBy(entry => entry.Category)
    .OrderBy(group => group.Key, StringComparer.Ordinal)
    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
var summary = new
{
    schemaVersion = 1,
    inputFile = Path.GetFileName(input),
    inputRows,
    parseErrors,
    stableKeys = groups.Count,
    outputEntries = entries.Count,
    conflictKeys = groups.Count(pair => pair.Value.Count > 1),
    conflictEntries = conflicts.Count,
    categoryCounts
};
File.WriteAllText(summaryFile, JsonSerializer.Serialize(summary, summaryOptions), new UTF8Encoding(false));

Console.WriteLine($"Input rows: {inputRows}");
Console.WriteLine($"Parse errors: {parseErrors}");
Console.WriteLine($"Stable keys: {groups.Count}");
Console.WriteLine($"Output entries: {entries.Count}");
Console.WriteLine($"Conflict keys: {summary.conflictKeys}");
Console.WriteLine($"Source: {output}");
Console.WriteLine($"Conflicts: {conflictsFile}");
Console.WriteLine($"Categories: {categoryDirectory}");
return parseErrors == 0 && conflicts.Count == 0 ? 0 : 1;

static int RunSelfTest()
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    var tempDirectory = Path.Combine(Path.GetTempPath(), "tsk-ui-compile-" + Guid.NewGuid().ToString("N"));
    var categoryDirectory = Path.Combine(tempDirectory, "by_category");
    Directory.CreateDirectory(categoryDirectory);

    try
    {
        var masterPath = Path.Combine(tempDirectory, "ui_source.jsonl");
        WriteJsonLines(masterPath, new[]
        {
            new SourceEntry
            {
                Key = "same", SourceHash = "hash-a", Translation = "主文件译文", Status = "translated"
            },
            new SourceEntry
            {
                Key = "cleared", SourceHash = "hash-clear", Translation = "应被清空", Status = "reviewed"
            },
            new SourceEntry
            {
                Key = "changed", SourceHash = "hash-old", Translation = "旧译文", Status = "verified"
            }
        }, jsonOptions);
        var categoryPath = Path.Combine(categoryDirectory, "unit.jsonl");
        WriteJsonLines(categoryPath, new[]
        {
            new SourceEntry
            {
                Key = "same", SourceHash = "hash-a", Translation = "分类译文", Status = "reviewed"
            },
            new SourceEntry
            {
                Key = "cleared", SourceHash = "hash-clear", Translation = "", Status = "new"
            }
        }, jsonOptions);

        var previousLoad = LoadPreviousEntries(masterPath, categoryDirectory, jsonOptions);
        var same = new SourceEntry { Key = "same", SourceHash = "hash-a", Translation = "", Status = "new" };
        var cleared = new SourceEntry { Key = "cleared", SourceHash = "hash-clear", Translation = "", Status = "new" };
        var changed = new SourceEntry { Key = "changed", SourceHash = "hash-new", Translation = "", Status = "new" };
        var fresh = new SourceEntry { Key = "fresh", SourceHash = "hash-fresh", Translation = "", Status = "new" };

        ApplyPreviousTranslation(same, previousLoad.Entries);
        ApplyPreviousTranslation(cleared, previousLoad.Entries);
        ApplyPreviousTranslation(changed, previousLoad.Entries);
        ApplyPreviousTranslation(fresh, previousLoad.Entries);

        var duplicatePath = Path.Combine(categoryDirectory, "misc.jsonl");
        WriteJsonLines(duplicatePath, new[]
        {
            new SourceEntry
            {
                Key = "same", SourceHash = "hash-a", Translation = "重复译文", Status = "translated"
            }
        }, jsonOptions);
        var duplicateLoad = LoadPreviousEntries(masterPath, categoryDirectory, jsonOptions);
        File.Delete(duplicatePath);

        var malformedPath = Path.Combine(categoryDirectory, "broken.jsonl");
        File.WriteAllText(malformedPath, "{not-json}", new UTF8Encoding(false));
        var malformedLoad = LoadPreviousEntries(masterPath, categoryDirectory, jsonOptions);

        var missionA = BuildKey(TestRecord("MissionList",
            "$.result.special_mission_tab.mission_group_list[].limit_date_text", "あと48日",
            ("mission_group_id", "131"), ("mission_type", "5")));
        var missionB = BuildKey(TestRecord("MissionList",
            "$.result.special_mission_tab.mission_group_list[].limit_date_text", "無期限",
            ("mission_group_id", "132"), ("mission_type", "5")));
        var freeItem = BuildKey(TestRecord("ShopList", "$.result.cost_mtrl_list[].item_name", "無料",
            ("item_id", "0"), ("item_type", "0"), ("cost_type", "5")));
        var platformItem = BuildKey(TestRecord("ShopList", "$.result.cost_mtrl_list[].item_name", "プラットフォーム通貨",
            ("item_id", "0"), ("item_type", "0"), ("cost_type", "3")));
        var rewardSingle = BuildKey(TestRecord("ShopList", "$.result.product_detail_list[].reward_name",
            "[称号]角色", ("reward_id", "1001"), ("reward_type", "2")));
        var rewardMultiline = BuildKey(TestRecord("GetGroupItemDetail", "$.result.item_list[].reward_name",
            "[称号]\n角色", ("reward_id", "1001"), ("reward_type", "2")));

        var passed = previousLoad.Errors.Count == 0 &&
                     same.Translation == "分类译文" && same.Status == "reviewed" &&
                     cleared.Translation == "" && cleared.Status == "new" &&
                     changed.Translation == "旧译文" && changed.Status == "stale" &&
                     changed.PreviousSourceHash == "hash-old" &&
                     fresh.Translation == "" && fresh.Status == "new" &&
                     duplicateLoad.Errors.Any(error => error.Contains("Duplicate key", StringComparison.Ordinal)) &&
                     malformedLoad.Errors.Any(error => error.Contains("Invalid JSON", StringComparison.Ordinal)) &&
                     missionA.Key != missionB.Key &&
                     freeItem.Key != platformItem.Key &&
                     rewardSingle.Key != rewardMultiline.Key;
        Console.WriteLine(passed
            ? "PASS: category overlay, safe validation, merge behavior and context-sensitive stable keys"
            : "FAIL: category overlay, safe validation, merge behavior or context-sensitive stable keys");
        return passed ? 0 : 1;
    }
    finally
    {
        Directory.Delete(tempDirectory, true);
    }
}

static CaptureRecord TestRecord(string apiName, string path, string source,
    params (string Name, string Value)[] context) => new()
{
    SchemaVersion = 2,
    ApiName = apiName,
    Path = path,
    Source = source,
    SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),
    Context = context.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal),
    FirstSeenUtc = "2026-01-01T00:00:00Z"
};

static UiTextKeyDescriptor BuildKey(CaptureRecord record) => UiTextKeyBuilder.Build(
    record.ApiName, record.Path, record.Source, record.SourceHash,
    record.Context ?? new Dictionary<string, string>(StringComparer.Ordinal));

static PreviousEntryLoadResult LoadPreviousEntries(
    string masterPath,
    string categoryDirectory,
    JsonSerializerOptions options)
{
    var result = new PreviousEntryLoadResult();
    if (File.Exists(masterPath))
    {
        foreach (var line in File.ReadLines(masterPath))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<SourceEntry>(line, options);
                if (entry != null && !string.IsNullOrEmpty(entry.Key))
                {
                    result.Entries[entry.Key] = entry;
                }
            }
            catch (JsonException)
            {
                // The master file is generated and recoverable from capture plus category files.
            }
        }
    }

    if (!Directory.Exists(categoryDirectory))
    {
        return result;
    }

    var categoryKeys = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(categoryDirectory, "*.jsonl")
                 .OrderBy(value => value, StringComparer.Ordinal))
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            SourceEntry entry;
            try
            {
                entry = JsonSerializer.Deserialize<SourceEntry>(line, options);
            }
            catch (JsonException exception)
            {
                result.Errors.Add(
                    $"Invalid JSON in {path}:{lineNumber}: {exception.Message}");
                continue;
            }

            if (entry == null || string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.SourceHash))
            {
                result.Errors.Add($"Missing key or sourceHash in {path}:{lineNumber}.");
                continue;
            }

            if (categoryKeys.TryGetValue(entry.Key, out var previousLocation))
            {
                result.Errors.Add(
                    $"Duplicate key '{entry.Key}' in {path}:{lineNumber}; first seen at {previousLocation}.");
                continue;
            }

            categoryKeys[entry.Key] = path + ":" + lineNumber;
            result.Entries[entry.Key] = entry;
        }
    }

    return result;
}

static void ApplyPreviousTranslation(SourceEntry entry, IReadOnlyDictionary<string, SourceEntry> previousEntries)
{
    if (!previousEntries.TryGetValue(entry.Key, out var previous) || string.IsNullOrEmpty(previous.Translation))
    {
        return;
    }

    entry.Translation = previous.Translation;
    if (string.Equals(previous.SourceHash, entry.SourceHash, StringComparison.Ordinal))
    {
        entry.Status = string.IsNullOrEmpty(previous.Status) ? "translated" : previous.Status;
    }
    else
    {
        entry.Status = "stale";
        entry.PreviousSourceHash = previous.SourceHash;
    }
}

static void WriteJsonLines(string path, IEnumerable<SourceEntry> records, JsonSerializerOptions options)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    foreach (var record in records)
    {
        writer.WriteLine(JsonSerializer.Serialize(record, options));
    }
}

internal sealed class Aggregate
{
    internal Aggregate(CaptureRecord record, UiTextKeyDescriptor descriptor)
    {
        Record = record;
        Descriptor = descriptor;
    }

    internal CaptureRecord Record { get; }
    internal UiTextKeyDescriptor Descriptor { get; }
    internal int Occurrences { get; set; }
    internal SortedSet<string> Origins { get; } = new(StringComparer.Ordinal);

    internal SourceEntry ToEntry(string key, string status) => new()
    {
        SchemaVersion = 1,
        Key = key,
        Category = Descriptor.Category,
        Field = Descriptor.Field,
        Identity = Descriptor.Identity,
        Source = Record.Source,
        SourceHash = Record.SourceHash,
        Translation = string.Empty,
        Status = status,
        Occurrences = Occurrences,
        Origins = Origins.ToArray(),
        FirstSeenUtc = Record.FirstSeenUtc
    };
}

internal sealed class CaptureRecord
{
    public int SchemaVersion { get; set; }
    public string ApiName { get; set; }
    public string Path { get; set; }
    public Dictionary<string, string> Context { get; set; }
    public string Source { get; set; }
    public string SourceHash { get; set; }
    public string FirstSeenUtc { get; set; }
}

internal sealed class SourceEntry
{
    public int SchemaVersion { get; set; }
    public string Key { get; set; }
    public string Category { get; set; }
    public string Field { get; set; }
    public SortedDictionary<string, string> Identity { get; set; }
    public string Source { get; set; }
    public string SourceHash { get; set; }
    public string PreviousSourceHash { get; set; }
    public string Translation { get; set; }
    public string Status { get; set; }
    public int Occurrences { get; set; }
    public string[] Origins { get; set; }
    public string FirstSeenUtc { get; set; }
}

internal sealed class PreviousEntryLoadResult
{
    internal Dictionary<string, SourceEntry> Entries { get; } = new(StringComparer.Ordinal);
    internal List<string> Errors { get; } = new();
}
