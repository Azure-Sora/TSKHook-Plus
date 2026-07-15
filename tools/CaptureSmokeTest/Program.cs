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

var identity = UiSpriteDumpIdentity.Build("common|pack", "btn_quest", 128f, 64f, true, 0);
var fileName = UiSpriteDumpIdentity.BuildFileName("btn:quest/selected", identity);
if (!fileName.StartsWith("btn_quest_selected__", StringComparison.Ordinal) || !fileName.EndsWith(".png") ||
    fileName.Length != "btn_quest_selected__".Length + 12 + ".png".Length)
{
    Console.Error.WriteLine($"FAIL sprite-file-name: {fileName}");
    failures++;
}

if (UiSpriteDumpIdentity.SanitizeFileName("  ...  ") != "unnamed_sprite")
{
    Console.Error.WriteLine("FAIL sprite-empty-file-name");
    failures++;
}

var atlasFileName = UiSpriteDumpIdentity.BuildAssetFileName("quest atlas", identity, "atlas.txt");
if (!atlasFileName.StartsWith("quest atlas__", StringComparison.Ordinal) ||
    !atlasFileName.EndsWith(".atlas.txt", StringComparison.Ordinal))
{
    Console.Error.WriteLine($"FAIL atlas-file-name: {atlasFileName}");
    failures++;
}

if (failures > 0)
{
    return 1;
}

Console.WriteLine($"PASS: {cases.Length} capture filter cases and Sprite dump identity cases");
return 0;
