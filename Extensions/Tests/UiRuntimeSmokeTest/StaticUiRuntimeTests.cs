using System.Text.Json;
using TSKHook;

internal static class StaticUiRuntimeTests
{
    internal static int Run(List<string> failures)
    {
        var count = 0;
        void Check(bool value, string message) { count++; if (!value) failures.Add(message); }
        void Reject(Action action, string message)
        {
            try { action(); Check(false, message); }
            catch (ArgumentException) { Check(true, message); }
            catch (InvalidDataException) { Check(true, message); }
        }
        var menu = "メニュー";
        var index = new UiTranslationIndex(new Dictionary<string, UiRuntimeTranslation>
        {
            ["static-ui:menu"] = new(UiJsonTranslator.Sha256(menu), "菜单", true),
            ["static-ui:recovery"] = new(UiJsonTranslator.Sha256("スタミナが {0} 回復します。必要数: {1} 個"),
                "恢复 {0} 点体力。需要: {1} 个", true, "スタミナが {0} 回復します。必要数: {1} 個"),
            ["static-ui:date"] = new(UiJsonTranslator.Sha256("{0}まで"), "截至{0}", true, "{0}まで")
        });
        Check(UiRenderedTextTranslator.TryTranslate(menu, index, out var output) && output == "菜单", "static UI exact rendered lookup");
        Check(!UiRenderedTextTranslator.TryTranslate("属性", new UiTranslationIndex(new Dictionary<string, UiRuntimeTranslation>
        { ["noop"] = new(UiJsonTranslator.Sha256("属性"), "属性", true) }), out _), "identical display values must not dirty TMP meshes repeatedly");
        Check(!index.TryGet("static-ui:menu", UiJsonTranslator.Sha256(menu), out _, out _), "rendered-only rows cannot be used through JSON exact lookup");
        var json = "{\"result\":{\"label\":\"メニュー\"}}";
        Check(!UiJsonTranslator.TryTranslate("HomeData", json, index, out var unchanged, out _, out _) && unchanged == json,
            "rendered-only source hash must never rewrite a network response");
        Check(UiRenderedTextTranslator.TryTranslate("スタミナが 240 回復します。必要数: 80 個", index, out output) &&
            output == "恢复 240 点体力。需要: 80 个", "UI template preserves current numeric quantities rather than captured values");
        Check(UiRenderedTextTranslator.TryTranslate("2026/11/01 23:59まで", index, out output) && output == "截至2026/11/01 23:59",
            "date template preserves complete date and time");
        Check(!UiRenderedTextTranslator.TryTranslate("プレイヤー名まで", index, out _), "numeric template cannot capture user names or free text");
        Check(!UiRenderedTextTranslator.TryTranslate("スタミナが <b>120</b> 回復します。必要数: 40 個", index, out _),
            "numeric template cannot capture rich text");
        var repeated = new UiTextFormatPattern("残り{0}/{0}回", "剩余{0}/{0}次");
        Check(repeated.TryTranslate("残り2/2回", out output) && output == "剩余2/2次" && !repeated.TryTranslate("残り2/3回", out _),
            "repeated placeholders must carry the same value");
        Reject(() => new UiTextFormatPattern("残り{0}回", "剩余{1}次"), "missing template token rejected");
        Reject(() => new UiTextFormatPattern("{0}", "{0}"), "whole-value wildcard rejected");
        Reject(() => new UiTextFormatPattern("残り{4}回", "剩余{4}次"), "unsupported template token rejected");
        var ambiguous = new UiTranslationIndex(new Dictionary<string, UiRuntimeTranslation>
        {
            ["a"] = new("a", "剩余{0}次", true, "残り{0}回"),
            ["b"] = new("b", "尚有{0}次", true, "残り{0}回")
        });
        Check(!UiRenderedTextTranslator.TryTranslate("残り12回", ambiguous, out _), "conflicting template output fails closed");
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, JsonSerializer.Serialize(new RuntimeRow
            {
                Key = "static-ui:menu", SourceHash = UiJsonTranslator.Sha256(menu), Translation = "菜单", RenderedOnly = true
            }, RuntimeAsset.JsonOptions) + "\n");
            Check(UiRenderedTextTranslator.TryTranslate(menu, RuntimeAsset.Load(file), out output) && output == "菜单",
                "runtime file preserves the rendered-only scope");
            File.WriteAllText(file, JsonSerializer.Serialize(new RuntimeRow
            {
                Key = "bad-template", SourceHash = UiJsonTranslator.Sha256("wrong"), Translation = "剩余{0}次",
                RenderedOnly = true, SourceTemplate = "残り{0}回"
            }, RuntimeAsset.JsonOptions));
            Reject(() => RuntimeAsset.Load(file), "template source hash mismatch rejected by runtime loader");
        }
        finally { File.Delete(file); }
        return count;
    }
}
