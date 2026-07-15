using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var masterPath = Path.GetFullPath(args.ElementAtOrDefault(0) ??
                                  "translations/ui/source/ui_source.jsonl");
var categoryDirectory = Path.GetFullPath(args.ElementAtOrDefault(1) ??
                                         "translations/ui/source/by_category");
var outputPath = Path.GetFullPath(args.ElementAtOrDefault(2) ??
                                  "translations/ui/runtime/ui_translations.jsonl");
var summaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, "compile_summary.json");

if (!File.Exists(masterPath) || !Directory.Exists(categoryDirectory))
{
    Console.Error.WriteLine($"Input missing. Master: {masterPath}; categories: {categoryDirectory}");
    return 2;
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
var summaryOptions = new JsonSerializerOptions(jsonOptions) { WriteIndented = true };
var masters = new Dictionary<string, TranslationRow>(StringComparer.Ordinal);
var parseErrors = 0;
var duplicateMasterKeys = 0;

foreach (var line in File.ReadLines(masterPath))
{
    if (!TryRead(line, jsonOptions, out var row))
    {
        parseErrors++;
        continue;
    }

    if (!masters.TryAdd(row.Key, row))
    {
        duplicateMasterKeys++;
    }
}

var seenCategoryKeys = new HashSet<string>(StringComparer.Ordinal);
var runtime = new List<RuntimeTranslation>();
var categoryRows = 0;
var duplicateCategoryKeys = 0;
var unknownKeys = 0;
var hashMismatches = 0;
var protectedSourceDifferences = 0;
var untranslatedRows = 0;
var ineligibleStatusRows = 0;
var richTextMismatches = 0;
var placeholderMismatches = 0;
var numberMismatches = 0;
var newlineMismatches = 0;
var eligibleStatuses = new HashSet<string>(new[] { "translated", "reviewed", "verified" },
    StringComparer.OrdinalIgnoreCase);

foreach (var file in Directory.EnumerateFiles(categoryDirectory, "*.jsonl").OrderBy(value => value,
             StringComparer.Ordinal))
{
    foreach (var line in File.ReadLines(file))
    {
        categoryRows++;
        if (!TryRead(line, jsonOptions, out var row))
        {
            parseErrors++;
            continue;
        }

        if (!seenCategoryKeys.Add(row.Key))
        {
            duplicateCategoryKeys++;
            continue;
        }

        if (!masters.TryGetValue(row.Key, out var master))
        {
            unknownKeys++;
            continue;
        }

        if (!string.Equals(row.SourceHash, master.SourceHash, StringComparison.Ordinal))
        {
            hashMismatches++;
            continue;
        }

        if (!string.Equals(row.Source, master.Source, StringComparison.Ordinal))
        {
            protectedSourceDifferences++;
        }

        if (string.IsNullOrWhiteSpace(row.Translation))
        {
            untranslatedRows++;
            continue;
        }

        if (!eligibleStatuses.Contains(row.Status ?? string.Empty))
        {
            ineligibleStatusRows++;
            continue;
        }

        if (!TokensEqual(master.Source, row.Translation, "<[^>]+>"))
        {
            richTextMismatches++;
        }
        if (!TokensEqual(master.Source, row.Translation,
                @"\{[^{}]+\}|%[0-9$.*+\-]*[A-Za-z]"))
        {
            placeholderMismatches++;
        }
        if (!TokensEqual(master.Source, row.Translation, @"\d+(?:[.,]\d+)?", sort: true))
        {
            numberMismatches++;
        }
        if (CountNewlines(master.Source) != CountNewlines(row.Translation))
        {
            newlineMismatches++;
        }

        runtime.Add(new RuntimeTranslation
        {
            SchemaVersion = 1,
            Key = master.Key,
            SourceHash = master.SourceHash,
            Translation = row.Translation
        });
    }
}

runtime = runtime.OrderBy(row => row.Key, StringComparer.Ordinal).ToList();
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
WriteJsonLines(outputPath, runtime, jsonOptions);

var fatalErrors = parseErrors + duplicateMasterKeys + duplicateCategoryKeys + unknownKeys + hashMismatches;
var summary = new
{
    schemaVersion = 1,
    masterFile = Path.GetFileName(masterPath),
    categoryFiles = Directory.EnumerateFiles(categoryDirectory, "*.jsonl").Count(),
    masterRows = masters.Count,
    categoryRows,
    outputEntries = runtime.Count,
    untranslatedRows,
    ineligibleStatusRows,
    protectedSourceDifferences,
    richTextMismatches,
    placeholderMismatches,
    numberMismatches,
    newlineMismatches,
    parseErrors,
    duplicateMasterKeys,
    duplicateCategoryKeys,
    unknownKeys,
    hashMismatches
};
File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, summaryOptions), new UTF8Encoding(false));

Console.WriteLine($"Master rows: {masters.Count}");
Console.WriteLine($"Category rows: {categoryRows}");
Console.WriteLine($"Runtime entries: {runtime.Count}");
Console.WriteLine($"Untranslated rows: {untranslatedRows}");
Console.WriteLine($"Ineligible status rows: {ineligibleStatusRows}");
Console.WriteLine($"Protected source differences ignored: {protectedSourceDifferences}");
Console.WriteLine($"Validation warnings: rich text {richTextMismatches}, placeholders {placeholderMismatches}, " +
                  $"numbers {numberMismatches}, newlines {newlineMismatches}");
Console.WriteLine($"Fatal validation errors: {fatalErrors}");
Console.WriteLine($"Runtime asset: {outputPath}");
return fatalErrors == 0 ? 0 : 1;

static bool TryRead(string line, JsonSerializerOptions options, out TranslationRow row)
{
    row = null;
    if (string.IsNullOrWhiteSpace(line))
    {
        return false;
    }

    try
    {
        row = JsonSerializer.Deserialize<TranslationRow>(line, options);
        return row != null && !string.IsNullOrEmpty(row.Key) && !string.IsNullOrEmpty(row.SourceHash);
    }
    catch (JsonException)
    {
        return false;
    }
}

static bool TokensEqual(string source, string translation, string pattern, bool sort = false)
{
    var sourceTokens = Regex.Matches(source ?? string.Empty, pattern).Select(match => match.Value);
    var translationTokens = Regex.Matches(translation ?? string.Empty, pattern).Select(match => match.Value);
    if (sort)
    {
        sourceTokens = sourceTokens.OrderBy(value => value, StringComparer.Ordinal);
        translationTokens = translationTokens.OrderBy(value => value, StringComparer.Ordinal);
    }

    return sourceTokens.SequenceEqual(translationTokens, StringComparer.Ordinal);
}

static int CountNewlines(string value) => (value ?? string.Empty).Count(character => character == '\n');

static void WriteJsonLines(string path, IEnumerable<RuntimeTranslation> rows, JsonSerializerOptions options)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    foreach (var row in rows)
    {
        writer.WriteLine(JsonSerializer.Serialize(row, options));
    }
}

internal sealed class TranslationRow
{
    public string Key { get; set; }
    public string Source { get; set; }
    public string SourceHash { get; set; }
    public string Translation { get; set; }
    public string Status { get; set; }
}

internal sealed class RuntimeTranslation
{
    public int SchemaVersion { get; set; }
    public string Key { get; set; }
    public string SourceHash { get; set; }
    public string Translation { get; set; }
}
