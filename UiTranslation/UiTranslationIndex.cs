using System;
using System.Collections.Generic;

namespace TSKHook;

internal sealed class UiTranslationIndex
{
    internal static readonly UiTranslationIndex Empty =
        new(new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, UiRuntimeTranslation> exact;
    private readonly IReadOnlyDictionary<string, UiRuntimeTranslation> uniqueBySourceHash;

    internal UiTranslationIndex(IReadOnlyDictionary<string, UiRuntimeTranslation> translations)
    {
        exact = translations ?? new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal);

        var unique = new Dictionary<string, UiRuntimeTranslation>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in exact.Values)
        {
            if (string.IsNullOrEmpty(entry.SourceHash) || ambiguous.Contains(entry.SourceHash))
            {
                continue;
            }

            if (!unique.TryGetValue(entry.SourceHash, out var existing))
            {
                unique[entry.SourceHash] = entry;
                continue;
            }

            if (!string.Equals(existing.Translation, entry.Translation, StringComparison.Ordinal))
            {
                unique.Remove(entry.SourceHash);
                ambiguous.Add(entry.SourceHash);
            }
        }

        uniqueBySourceHash = unique;
        AmbiguousSourceHashCount = ambiguous.Count;
    }

    internal int ExactCount => exact.Count;

    internal int UniqueSourceHashCount => uniqueBySourceHash.Count;

    internal int AmbiguousSourceHashCount { get; }

    internal bool TryGet(string key, string sourceHash, out string translation, out bool usedHashFallback)
    {
        usedHashFallback = false;
        translation = null;

        if (exact.TryGetValue(key, out var exactEntry) &&
            string.Equals(exactEntry.SourceHash, sourceHash, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(exactEntry.Translation))
        {
            translation = exactEntry.Translation;
            return true;
        }

        if (!uniqueBySourceHash.TryGetValue(sourceHash, out var fallbackEntry) ||
            string.IsNullOrWhiteSpace(fallbackEntry.Translation))
        {
            return false;
        }

        translation = fallbackEntry.Translation;
        usedHashFallback = true;
        return true;
    }
}
