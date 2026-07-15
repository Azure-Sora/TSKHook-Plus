using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using TMPro;

namespace TSKHook;

internal static class UiFontFallbackPatch
{
    private static readonly object FontLock = new();
    private static readonly HashSet<int> ProcessedFontIds = new();
    private static readonly HashSet<int> UnreadableAtlasFontIds = new();
    private static int errorCount;
    private static int atlasGuardErrorCount;
    private static long skippedUnreadableAtlasInsertions;

    internal static int ProcessedFontCount
    {
        get
        {
            lock (FontLock)
            {
                return ProcessedFontIds.Count;
            }
        }
    }

    internal static long SkippedUnreadableAtlasInsertions =>
        Interlocked.Read(ref skippedUnreadableAtlasInsertions);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_FontAsset), nameof(TMP_FontAsset.TryAddCharacterInternal))]
    private static bool SkipUnreadableAtlasInsertion(
        TMP_FontAsset __instance,
        ref TMP_Character character,
        ref bool __result)
    {
        if (!TSKConfig.TranslationEnabled || !TSKConfig.UiTranslationEnabled || __instance == null)
        {
            return true;
        }

        try
        {
            var atlasTexture = __instance.atlasTexture;
            if (atlasTexture == null || atlasTexture.isReadable)
            {
                return true;
            }

            character = null;
            __result = false;
            Interlocked.Increment(ref skippedUnreadableAtlasInsertions);

            var fontId = __instance.GetInstanceID();
            lock (FontLock)
            {
                if (UnreadableAtlasFontIds.Add(fontId))
                {
                    Plugin.Global.Log.LogInfo(
                        $"[UI Translation] Skipping failed glyph insertion for unreadable TMP atlas: {__instance.name}.");
                }
            }

            return false;
        }
        catch (Exception exception)
        {
            if (Interlocked.Increment(ref atlasGuardErrorCount) <= 5)
            {
                Plugin.Global.Log.LogWarning(
                    $"[UI Translation] Could not inspect TMP atlas before glyph insertion: {exception}");
            }

            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    private static void TextChanged(TMP_Text __instance)
    {
        Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
    private static void UguiEnabled(TextMeshProUGUI __instance)
    {
        Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
    private static void MeshEnabled(TextMeshPro __instance)
    {
        Apply(__instance);
    }

    private static void Apply(TMP_Text text)
    {
        if (!TSKConfig.TranslationEnabled || !TSKConfig.UiTranslationEnabled || text == null ||
            Patch.TMPTranslateFont == null)
        {
            return;
        }

        try
        {
            var font = text.font;
            if (font == null || font == Patch.TMPTranslateFont)
            {
                return;
            }

            var fontId = font.GetInstanceID();
            lock (FontLock)
            {
                if (ProcessedFontIds.Contains(fontId))
                {
                    return;
                }

                var fallbacks = font.fallbackFontAssetTable;
                if (fallbacks == null)
                {
                    fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                    font.fallbackFontAssetTable = fallbacks;
                }

                if (!fallbacks.Contains(Patch.TMPTranslateFont))
                {
                    fallbacks.Add(Patch.TMPTranslateFont);
                }

                ProcessedFontIds.Add(fontId);
            }
        }
        catch (Exception exception)
        {
            if (Interlocked.Increment(ref errorCount) <= 5)
            {
                Plugin.Global.Log.LogWarning($"[UI Translation] Could not configure TMP font fallback: {exception}");
            }
        }
    }
}
