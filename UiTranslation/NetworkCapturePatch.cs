using HarmonyLib;
using TKS.Network.Domain;

namespace TSKHook;

[HarmonyPatch(typeof(TKS.Network.HttpClient), "FormatData",
    new[] { typeof(ApiName), typeof(string), typeof(string) })]
internal static class NetworkCapturePatch
{
    [HarmonyPostfix]
    private static void CaptureFormattedResponse(ApiName apiName, ref string __result)
    {
        UiTextCaptureService.Observe(apiName.ToString(), __result);
    }
}
