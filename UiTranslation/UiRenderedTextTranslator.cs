namespace TSKHook;

internal static class UiRenderedTextTranslator
{
    internal static bool TryTranslate(string source, UiTranslationIndex translations, out string translation)
    {
        translation = null;
        if (translations == null || translations.ExactCount == 0 ||
            !CaptureTextFilter.ShouldCapture("$.rendered_text", source))
        {
            return false;
        }

        return translations.TryGetUniqueBySourceHash(UiJsonTranslator.Sha256(source), out translation);
    }
}
