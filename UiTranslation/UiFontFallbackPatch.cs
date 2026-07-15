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
    private static int errorCount;

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
