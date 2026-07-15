using TSKHook;

var cases = new[]
{
    ("katakana", "$.skill_name", "スターアタック", true),
    ("kanji-only", "$.item_detail", "攻撃力上昇", true),
    ("multiline", "$.skill_detail", "敵全体にダメージ\n追加効果", true),
    ("long-text", "$.description", new string('あ', 200), true),
    ("english", "$.label", "Attack up", false),
    ("empty", "$.label", "", false),
    ("player-name", "$.data.user_name", "日本語の名前", false),
    ("user-nm", "$.user_data.user_nm", "青空", false),
    ("user-data-child", "$.result.user_data.title", "日本語称号", false),
    ("comment", "$.data.comment", "よろしくお願いします", false)
};

var failures = 0;
foreach (var test in cases)
{
    var actual = CaptureTextFilter.ShouldCapture(test.Item2, test.Item3);
    if (actual != test.Item4)
    {
        Console.Error.WriteLine($"FAIL {test.Item1}: expected {test.Item4}, got {actual}");
        failures++;
    }
}

if (failures > 0)
{
    return 1;
}

Console.WriteLine($"PASS: {cases.Length} capture filter cases");

var catalog = new SkillBatchCatalog();
var added = catalog.ObserveUnitList(
    "{\"result\":{\"unit_list\":[" +
    "{\"u_unit_id\":9002,\"unit_id\":1002}," +
    "{\"u_unit_id\":\"9001\",\"unit_id\":\"1001\"}," +
    "{\"u_unit_id\":9002,\"unit_id\":1002}," +
    "{\"u_unit_id\":0,\"unit_id\":9999}]}}");

if (added != 2 || catalog.UnitCount != 2)
{
    Console.Error.WriteLine($"FAIL skill catalog parse: added={added}, total={catalog.UnitCount}");
    return 1;
}

if (catalog.BuildRequestIds().Count != 0)
{
    Console.Error.WriteLine("FAIL skill catalog must not emit IDs before calibration");
    return 1;
}

if (catalog.ObserveKnownRequestId(9001) != SkillBatchIdentifierMode.UserUnitId ||
    !catalog.BuildRequestIds().SequenceEqual(new[] { 9001, 9002 }))
{
    Console.Error.WriteLine("FAIL skill catalog user-unit ID calibration");
    return 1;
}

var masterCatalog = new SkillBatchCatalog();
masterCatalog.ObserveUnitList(
    "{\"result\":{\"unit_list\":[{\"u_unit_id\":8001,\"unit_id\":2002}," +
    "{\"u_unit_id\":8002,\"unit_id\":2001}]}}");
if (masterCatalog.ObserveKnownRequestId(2001) != SkillBatchIdentifierMode.MasterUnitId ||
    !masterCatalog.BuildRequestIds().SequenceEqual(new[] { 2001, 2002 }))
{
    Console.Error.WriteLine("FAIL skill catalog master-unit ID calibration");
    return 1;
}

var ambiguousCatalog = new SkillBatchCatalog();
ambiguousCatalog.ObserveUnitList(
    "{\"result\":{\"unit_list\":[{\"u_unit_id\":3001,\"unit_id\":3001}]}}");
if (ambiguousCatalog.ObserveKnownRequestId(3001) != SkillBatchIdentifierMode.Unknown)
{
    Console.Error.WriteLine("FAIL skill catalog ambiguous calibration must fail closed");
    return 1;
}

Console.WriteLine("PASS: skill batch catalog parse, dedupe, calibration, and fail-closed cases");

if (!SkillBatchCatalog.HasExSkillDataResponse(
        "{\"result\":{\"skill_data_list\":[{\"ex_skill_id\":1001}]}}") ||
    SkillBatchCatalog.HasExSkillDataResponse("{\"result\":{\"skill_data_list\":[]}}") ||
    SkillBatchCatalog.HasExSkillDataResponse("{\"result\":{}}") ||
    SkillBatchCatalog.HasExSkillDataResponse("not-json"))
{
    Console.Error.WriteLine("FAIL skill batch response validation");
    return 1;
}

Console.WriteLine("PASS: skill batch response validation cases");
return 0;
