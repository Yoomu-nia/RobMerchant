using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 读档重进“免费商店”时的收尾处理。
///
/// 因为存档是在商店里写的，存档里的地图路径已经包含这个商店节点，
/// 读档时 <c>RunManager.EnterMapPointInternal</c> 会再次调用
/// <c>RunState.AppendToMapPointHistory</c>，导致这个节点在历史里出现两次。
/// 这里在“本次读档确实是回到免费商店”时跳过那一次追加。
///
/// 判断依据：本局抢过商人，且当前地图坐标 == 被抢商店的坐标（见 RobRunData）。
/// </summary>
[HarmonyPatch]
internal static class ReloadIntoFreeShopPatch
{
	[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
	[HarmonyPrefix]
	private static void LoadIntoLatestMapCoord_Prefix(RunManager __instance)
	{
		RunState? state = __instance.DebugOnlyGetState();
		bool freeShopReload = state != null && RobRunData.IsFreeShopHere(state);

		RobSession.SuppressHistoryAppend = freeShopReload;

		if (freeShopReload)
		{
			Log.Info("[RobMerchant] 读档：上次存档在免费商店，准备重进同一节点");
		}
	}

	[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
	[HarmonyPrefix]
	private static bool AppendToMapPointHistory_Prefix()
	{
		if (!RobSession.SuppressHistoryAppend)
		{
			return true;
		}

		RobSession.SuppressHistoryAppend = false;
		Log.Info("[RobMerchant] 读档重进商店：跳过重复的历史记录追加");
		return false;
	}
}
