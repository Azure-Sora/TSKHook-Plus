using System.Text.Json;
using System.Diagnostics;
using System.Text;
using TSKHook;

var failures = new List<string>();

var source = "敵全体にダメージ";
var context100 = Context(("skill_id", "100"), ("skill_data_type", "1"), ("lv", "1"));
var context200 = Context(("skill_id", "200"), ("skill_data_type", "1"), ("lv", "1"));
var path = "$.result.skill_list[].skill_detail";
var key100 = Key("UnitList", path, source, context100);
var translations = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal)
{
    [key100] = new UiRuntimeTranslation(UiJsonTranslator.Sha256(source), "对全体敌人造成伤害")
};
var translationIndex = new UiTranslationIndex(translations);

var json = "{\"result\":{\"skill_list\":[" +
           "{\"skill_id\":100,\"skill_data_type\":1,\"lv\":1,\"skill_detail\":\"敵全体にダメージ\",\"power\":120}," +
           "{\"skill_id\":200,\"skill_data_type\":1,\"lv\":1,\"skill_detail\":\"敵全体にダメージ\",\"power\":130}" +
           "]}}";
Assert(UiJsonTranslator.TryTranslate("UnitList", json, translationIndex,
        out var translated, out var count, out var fallbackCount),
    "structured response should report a translation", failures);
Assert(count == 2, "exact and unambiguous source-hash fallback should both translate", failures);
Assert(fallbackCount == 1, "one skill should use the source-hash fallback", failures);
using (var document = JsonDocument.Parse(translated))
{
    var list = document.RootElement.GetProperty("result").GetProperty("skill_list");
    Assert(list[0].GetProperty("skill_detail").GetString() == "对全体敌人造成伤害",
        "skill 100 translation", failures);
    Assert(list[1].GetProperty("skill_detail").GetString() == "对全体敌人造成伤害",
        "same unambiguous source under another skill identity should use the fallback", failures);
    Assert(list[0].GetProperty("power").GetInt32() == 120 && list[1].GetProperty("power").GetInt32() == 130,
        "non-string JSON values must remain unchanged", failures);
}

var context300 = Context(("skill_id", "300"), ("skill_data_type", "1"), ("lv", "1"));
var ambiguousTranslations = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal)
{
    [key100] = new UiRuntimeTranslation(UiJsonTranslator.Sha256(source), "对全体敌人造成伤害"),
    [Key("UnitList", path, source, context300)] =
        new UiRuntimeTranslation(UiJsonTranslator.Sha256(source), "对所有敌人造成伤害（另一语境）")
};
Assert(UiJsonTranslator.TryTranslate("UnitList", json, new UiTranslationIndex(ambiguousTranslations),
        out var ambiguitySafeResult, out var ambiguitySafeCount, out var ambiguityFallbackCount),
    "an exact translation should still work when the source hash is ambiguous", failures);
using (var document = JsonDocument.Parse(ambiguitySafeResult))
{
    var list = document.RootElement.GetProperty("result").GetProperty("skill_list");
    Assert(ambiguitySafeCount == 1 && ambiguityFallbackCount == 0,
        "ambiguous hashes must not use the fallback", failures);
    Assert(list[1].GetProperty("skill_detail").GetString() == source,
        "ambiguous same-source text under an unknown identity must remain Japanese", failures);
}

var badHash = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal)
{
    [key100] = new UiRuntimeTranslation("wrong-hash", "错误译文")
};
Assert(!UiJsonTranslator.TryTranslate("UnitList", json, new UiTranslationIndex(badHash),
        out var unchanged, out var badHashCount, out var badHashFallbackCount) &&
       unchanged == json && badHashCount == 0 && badHashFallbackCount == 0,
    "source hash mismatch must fail closed", failures);

var userSource = "プレイヤー名";
var userPath = "$.user_data.user_nm";
var userKey = Key("HomeData", userPath, userSource, new Dictionary<string, string>());
var userTranslations = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal)
{
    [userKey] = new UiRuntimeTranslation(UiJsonTranslator.Sha256(userSource), "玩家名")
};
var userJson = "{\"user_data\":{\"user_nm\":\"プレイヤー名\"}}";
Assert(!UiJsonTranslator.TryTranslate("HomeData", userJson, new UiTranslationIndex(userTranslations),
        out var userUnchanged, out var userCount, out var userFallbackCount) &&
       userUnchanged == userJson && userCount == 0 && userFallbackCount == 0,
    "sensitive player field must never be replaced", failures);

if (failures.Count == 0)
{
    RunStructuredLookupBenchmark();
    Console.WriteLine("PASS: 12 runtime translation assertions");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine("FAIL: " + failure);
}
return 1;

static Dictionary<string, string> Context(params (string Name, string Value)[] values) =>
    values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

static string Key(string apiName, string path, string source, IReadOnlyDictionary<string, string> context)
{
    var hash = UiJsonTranslator.Sha256(source);
    return UiTextKeyBuilder.Build(apiName, path, source, hash, context).Key;
}

static void Assert(bool condition, string message, ICollection<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

static void RunStructuredLookupBenchmark()
{
    const int rowCount = 3000;
    var json = new StringBuilder("{\"result\":{\"unit_list\":[");
    var translations = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal);
    for (var index = 1; index <= rowCount; index++)
    {
        if (index > 1)
        {
            json.Append(',');
        }

        var source = "キャラクター" + index;
        json.Append("{\"unit_id\":").Append(index)
            .Append(",\"unit_name\":\"").Append(source).Append("\"}");
        var context = Context(("unit_id", index.ToString()));
        var key = Key("UnitList", "$.result.unit_list[].unit_name", source, context);
        translations[key] = new UiRuntimeTranslation(UiJsonTranslator.Sha256(source), "角色" + index);
    }
    json.Append("]}}");

    var payload = json.ToString();
    var translationIndex = new UiTranslationIndex(translations);
    var stopwatch = Stopwatch.StartNew();
    UiJsonTranslator.TryTranslate("UnitList", payload, translationIndex,
        out _, out var replacements, out _);
    stopwatch.Stop();
    Console.WriteLine(
        $"BENCHMARK structured-only: {rowCount} rows, {replacements} replacements, {stopwatch.ElapsedMilliseconds} ms");
}
