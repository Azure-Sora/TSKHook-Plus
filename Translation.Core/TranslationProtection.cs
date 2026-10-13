namespace TSKHook;

public static class TranslationProtection
{
    // BattleEnd uses this string as a control identifier as well as a display label.
    // The rule runs before both exact matching and hash fallback.
    public static bool IsProtectedPath(string apiName, string path) =>
        string.Equals(apiName, "BattleEnd", StringComparison.Ordinal) &&
        path.EndsWith(".retry_data.cost_name", StringComparison.Ordinal);
}
