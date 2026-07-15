using System;
using System.Text;
using HarmonyLib;
using UnityEngine.Networking;

namespace TSKHook;

[HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
internal static class SkillRequestObservationPatch
{
    [HarmonyPrefix]
    private static void ObserveRequest(UnityWebRequest __instance)
    {
        try
        {
            var uploadData = __instance?.uploadHandler?.data;
            var body = uploadData == null || uploadData.Length == 0
                ? null
                : Encoding.UTF8.GetString(uploadData.AsSpan());
            SkillBatchCaptureService.ObserveOutgoingRequest(__instance?.url, body);
        }
        catch (Exception exception)
        {
            Plugin.Global.Log.LogWarning(
                $"[Skill Batch] Could not inspect outgoing request safely: {exception.Message}");
        }
    }
}
