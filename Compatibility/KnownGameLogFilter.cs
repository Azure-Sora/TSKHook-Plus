using System;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;

namespace TSKHook;

// Only suppress the observed log signature; never intercept game raycasting.
[HarmonyPatch(typeof(Logger), "InternalLogEvent")]
internal static class KnownGameLogFilter
{
    private static int reported;
    private static readonly Regex Signature = new(
        @"\ADuring invoking native->managed trampoline\r?\nException: Il2CppInterop\.Runtime\.Il2CppException: System\.NullReferenceException: [^\r\n]+\r?\n--- BEGIN IL2CPP STACK TRACE ---\r?\nSystem\.NullReferenceException: [^\r\n]+\r?\n  at UnityEngine\.Sprite\.get_textureRect \(\) [^\r\n]+\r?\n  at RaycastMask\.IsRaycastLocationValid \(UnityEngine\.Vector2 sp, UnityEngine\.Camera eventCamera\) [^\r\n]+\r?\n",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    [HarmonyPrefix]
    private static bool Prefix(LogEventArgs __1)
    {
        if (__1.Level != LogLevel.Error || __1.Source.SourceName != "Il2CppInterop" ||
            __1.Data is not string message || message.Length > 32768) return true;
        try
        {
            if (!Signature.IsMatch(message) ||
                !message.Contains("  at UnityEngine.EventSystems.EventSystem.Update () ", StringComparison.Ordinal) ||
                !message.Contains("--- END IL2CPP STACK TRACE ---", StringComparison.Ordinal)) return true;
        }
        catch (RegexMatchTimeoutException) { return true; }

        if (Interlocked.Exchange(ref reported, 1) == 0)
            Plugin.Global.Log.LogWarning("[Log Filter] Suppressed known Sprite.get_textureRect / RaycastMask null-reference log; further identical signatures are hidden. Game behavior is unchanged.");
        return false;
    }
}
