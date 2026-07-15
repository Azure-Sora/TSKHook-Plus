using HarmonyLib;
using TKS.Network.Domain;

namespace TSKHook;

[HarmonyPatch(typeof(ExSkillStrengthenDataRequestEntity), MethodType.Constructor, new[] { typeof(int) })]
internal static class SkillBatchCapturePatch
{
    [HarmonyPostfix]
    private static void ObserveRequestId(int __0)
    {
        SkillBatchCaptureService.ObserveExSkillRequestId(__0);
    }
}
