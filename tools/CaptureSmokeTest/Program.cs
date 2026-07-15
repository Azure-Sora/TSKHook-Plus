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

var spriteRule = new UiSpriteOverrideRule
{
    Id = "footer-quest",
    SpriteName = "btn_quest",
    TextureName = "CommonPack",
    Width = 196,
    Height = 64,
    ObjectPathSuffix = "FooterRoot\\FooterView(Clone)\\Quest",
    ReplacementFile = "ui_textures/btn_quest.png"
};
if (!UiSpriteOverrideMatcher.Matches(spriteRule, "btn_quest", "CommonPack", 196, 64,
        "Controller/HeaderFooterCanvas/FooterRoot/FooterView(Clone)/Quest"))
{
    Console.Error.WriteLine("FAIL sprite-override-exact-match");
    failures++;
}

if (UiSpriteOverrideMatcher.Matches(spriteRule, "btn_quest", "CommonPack", 195, 64,
        "Controller/HeaderFooterCanvas/FooterRoot/FooterView(Clone)/Quest") ||
    UiSpriteOverrideMatcher.Matches(spriteRule, "btn_home", "CommonPack", 196, 64,
        "Controller/HeaderFooterCanvas/FooterRoot/FooterView(Clone)/Quest") ||
    UiSpriteOverrideMatcher.Matches(spriteRule, "btn_quest", "OtherPack", 196, 64,
        "Controller/HeaderFooterCanvas/FooterRoot/FooterView(Clone)/Quest"))
{
    Console.Error.WriteLine("FAIL sprite-override-fail-closed");
    failures++;
}

if (failures > 0)
{
    return 1;
}

Console.WriteLine($"PASS: {cases.Length} capture filter cases and Sprite override matcher cases");
return 0;
