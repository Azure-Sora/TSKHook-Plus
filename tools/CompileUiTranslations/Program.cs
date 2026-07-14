using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

Directory.CreateDirectory(outputDirectory);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};
var summaryOptions = new JsonSerializerOptions(jsonOptions) { WriteIndented = true };
var groups = new Dictionary<string, Dictionary<string, Aggregate>>(StringComparer.Ordinal);
var previousEntries = LoadPreviousEntries(output, jsonOptions);
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

    var descriptor = KeyBuilder.Build(record);
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
    var previous = new Dictionary<string, SourceEntry>(StringComparer.Ordinal)
    {
        ["same"] = new SourceEntry
        {
            Key = "same", SourceHash = "hash-a", Translation = "译文A", Status = "reviewed"
        },
        ["changed"] = new SourceEntry
        {
            Key = "changed", SourceHash = "hash-old", Translation = "旧译文", Status = "verified"
        }
    };
    var same = new SourceEntry { Key = "same", SourceHash = "hash-a", Translation = "", Status = "new" };
    var changed = new SourceEntry { Key = "changed", SourceHash = "hash-new", Translation = "", Status = "new" };
    var fresh = new SourceEntry { Key = "fresh", SourceHash = "hash-fresh", Translation = "", Status = "new" };

    ApplyPreviousTranslation(same, previous);
    ApplyPreviousTranslation(changed, previous);
    ApplyPreviousTranslation(fresh, previous);

    var missionA = KeyBuilder.Build(TestRecord("MissionList",
        "$.result.special_mission_tab.mission_group_list[].limit_date_text", "あと48日",
        ("mission_group_id", "131"), ("mission_type", "5")));
    var missionB = KeyBuilder.Build(TestRecord("MissionList",
        "$.result.special_mission_tab.mission_group_list[].limit_date_text", "無期限",
        ("mission_group_id", "132"), ("mission_type", "5")));
    var freeItem = KeyBuilder.Build(TestRecord("ShopList", "$.result.cost_mtrl_list[].item_name", "無料",
        ("item_id", "0"), ("item_type", "0"), ("cost_type", "5")));
    var platformItem = KeyBuilder.Build(TestRecord("ShopList", "$.result.cost_mtrl_list[].item_name", "プラットフォーム通貨",
        ("item_id", "0"), ("item_type", "0"), ("cost_type", "3")));
    var rewardSingle = KeyBuilder.Build(TestRecord("ShopList", "$.result.product_detail_list[].reward_name",
        "[称号]角色", ("reward_id", "1001"), ("reward_type", "2")));
    var rewardMultiline = KeyBuilder.Build(TestRecord("GetGroupItemDetail", "$.result.item_list[].reward_name",
        "[称号]\n角色", ("reward_id", "1001"), ("reward_type", "2")));

    var passed = same.Translation == "译文A" && same.Status == "reviewed" &&
                 changed.Translation == "旧译文" && changed.Status == "stale" &&
                 changed.PreviousSourceHash == "hash-old" &&
                 fresh.Translation == "" && fresh.Status == "new" &&
                 missionA.Key != missionB.Key &&
                 freeItem.Key != platformItem.Key &&
                 rewardSingle.Key != rewardMultiline.Key;
    Console.WriteLine(passed
        ? "PASS: merge behavior and context-sensitive stable keys"
        : "FAIL: merge behavior or context-sensitive stable keys");
    return passed ? 0 : 1;
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

static Dictionary<string, SourceEntry> LoadPreviousEntries(string path, JsonSerializerOptions options)
{
    var result = new Dictionary<string, SourceEntry>(StringComparer.Ordinal);
    if (!File.Exists(path))
    {
        return result;
    }

    foreach (var line in File.ReadLines(path))
    {
        try
        {
            var entry = JsonSerializer.Deserialize<SourceEntry>(line, options);
            if (entry != null && !string.IsNullOrEmpty(entry.Key))
            {
                result[entry.Key] = entry;
            }
        }
        catch (JsonException)
        {
            // A malformed previous line must not prevent rebuilding from valid capture data.
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

internal static class KeyBuilder
{
    private static readonly string[] GenericIdentityFields =
    {
        "campaign_id", "category_id", "character_id", "event_id", "group_id", "id", "quest_id",
        "reward_id", "reward_type", "shop_id", "sister_unit_id", "stage_id", "status_effect_id", "unit_id"
    };

    internal static KeyDescriptor Build(CaptureRecord record)
    {
        var context = record.Context ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var field = record.Path[(record.Path.LastIndexOf('.') + 1)..].Replace("[]", "", StringComparison.Ordinal);

        if (field.StartsWith("exclusive_unit_", StringComparison.OrdinalIgnoreCase) &&
            context.ContainsKey("exclusive_unit_id"))
        {
            return Create("unit", field, context, ("exclusive_unit_id", true));
        }

        if (field is "unit_name" or "character_name" or "character_name_kana" or "full_name" &&
            context.ContainsKey("unit_id"))
        {
            return Create("unit", field, context, ("unit_id", true), ("character_id", false));
        }

        if (context.TryGetValue("item_id", out var itemId) &&
            (field.Contains("item", StringComparison.OrdinalIgnoreCase) || field == "detail"))
        {
            return itemId == "0"
                ? Create("item", field, context, ("item_type", false), ("item_id", true), ("cost_type", true))
                : Create("item", field, context, ("item_type", false), ("item_id", true));
        }

        if (context.ContainsKey("reward_id") &&
            (field.Contains("reward", StringComparison.OrdinalIgnoreCase) ||
             record.Path.Contains("reward_list", StringComparison.OrdinalIgnoreCase) ||
             record.Path.Contains("product_detail_list", StringComparison.OrdinalIgnoreCase)))
        {
            return CreateReward(field, context, record.Source);
        }

        if (record.Path.Contains("sister_unit_list", StringComparison.OrdinalIgnoreCase) &&
            record.Path.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSisterSkill(field, record.Path, context);
        }

        if (context.TryGetValue("equip_id", out var equipId) &&
            (field.Contains("equip", StringComparison.OrdinalIgnoreCase) || record.Path.Contains("equip", StringComparison.OrdinalIgnoreCase)))
        {
            var includeSkill = context.ContainsKey("skill_id") &&
                               (record.Path.Contains("skill", StringComparison.OrdinalIgnoreCase) ||
                                record.Path.Contains("enchant", StringComparison.OrdinalIgnoreCase));
            return includeSkill
                ? Create("equip", field, context, ("equip_id", true), ("skill_id", true),
                    ("frame_no", false), ("effect_type", false), ("lv", false))
                : Create("equip", field, context, ("equip_id", true));
        }

        if (context.ContainsKey("skill_id") && record.Path.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return Create("skill", field, context,
                ("skill_data_type", false), ("skill_id", true), ("lv", false), ("unit_id", false));
        }

        if (context.ContainsKey("mission_id") || record.Path.Contains("mission", StringComparison.OrdinalIgnoreCase))
        {
            return Create("mission", field, context, ("mission_type", false), ("mission_group_id", false),
                ("mission_id", false), ("id", false));
        }

        if (context.ContainsKey("product_id") || record.Path.Contains("product", StringComparison.OrdinalIgnoreCase))
        {
            return Create("shop", field, context, ("shop_id", false), ("product_id", false), ("id", false));
        }

        if (context.ContainsKey("unit_id") &&
            (record.Path.Contains("unit", StringComparison.OrdinalIgnoreCase) ||
             record.Path.Contains("character", StringComparison.OrdinalIgnoreCase)))
        {
            return Create("unit", field, context, ("unit_id", true), ("character_id", false));
        }

        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in GenericIdentityFields)
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        if (identity.Count == 0)
        {
            identity["source_hash"] = record.SourceHash[..12];
        }

        return FromIdentity("misc", field, identity, record.ApiName);
    }

    private static KeyDescriptor CreateSisterSkill(string field, string path,
        IReadOnlyDictionary<string, string> context)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[]
                 {
                     "sister_unit_id", "active_skill_id", "support_skill_id", "status_effect_id", "skill_type", "lv"
                 })
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        identity["skill_kind"] = path.Contains("extra_support_skill_data", StringComparison.OrdinalIgnoreCase)
            ? "extra_support"
            : path.Contains("active_skill_data", StringComparison.OrdinalIgnoreCase)
                ? "active"
                : "support";
        return FromIdentity("skill", field, identity, null);
    }

    private static KeyDescriptor CreateReward(string field, IReadOnlyDictionary<string, string> context,
        string source)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "reward_type", "reward_id" })
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        if (!identity.ContainsKey("reward_id"))
        {
            identity["reward_id"] = "missing";
        }

        if (field == "reward_name")
        {
            identity["layout"] = source.Contains('\n') ? "multiline" : "singleline";
        }

        return FromIdentity("reward", field, identity, null);
    }

    private static KeyDescriptor Create(string category, string field, IReadOnlyDictionary<string, string> context,
        params (string Name, bool Required)[] fields)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var fieldSpec in fields)
        {
            if (context.TryGetValue(fieldSpec.Name, out var value))
            {
                identity[fieldSpec.Name] = value;
            }
            else if (fieldSpec.Required)
            {
                identity[fieldSpec.Name] = "missing";
            }
        }

        return FromIdentity(category, field, identity, null);
    }

    private static KeyDescriptor FromIdentity(string category, string field,
        SortedDictionary<string, string> identity, string scope)
    {
        var key = new StringBuilder(category).Append(':');
        if (!string.IsNullOrEmpty(scope))
        {
            key.Append(Escape(scope)).Append(':');
        }
        foreach (var pair in identity)
        {
            key.Append(pair.Key).Append('=').Append(Escape(pair.Value)).Append(':');
        }
        key.Append(field);
        return new KeyDescriptor(key.ToString(), category, field, identity);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);
}

internal sealed class Aggregate
{
    internal Aggregate(CaptureRecord record, KeyDescriptor descriptor)
    {
        Record = record;
        Descriptor = descriptor;
    }

    internal CaptureRecord Record { get; }
    internal KeyDescriptor Descriptor { get; }
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

internal sealed record KeyDescriptor(string Key, string Category, string Field,
    SortedDictionary<string, string> Identity);

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
