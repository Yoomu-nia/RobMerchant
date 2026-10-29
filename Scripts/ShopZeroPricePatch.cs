using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 抢劫过的商店：普通商品（卡牌 / 遗物 / 药水）价格归零。
///
/// 原理（源码已证实）：
/// - MerchantEntry.Cost 每次读取都会走 Hook.ModifyMerchantPrice(CurrentRoom is MerchantRoom 时)
/// - 商店槽位 UpdateVisual 是实时读 Entry.Cost，所以界面自动显示 0，不需要手动刷新
/// - 购买时 LoseGold(Cost) 里 Cost 已经是 0，因此不扣钱
///
/// 另：TheCourier（信使）遗物会在购买后让该格补货，价格同样是 0 → 会变成无限刷。
/// 所以抢劫商店里同时把 Hook.ShouldRefillMerchantEntry 压成 false。
/// </summary>
[HarmonyPatch]
internal static class ShopZeroPricePatch
{
	/// <summary>当前商店是否处于“已抢劫 / 免费”状态（内存里的那家，或读档重进的那家）。</summary>
	private static bool IsRobbingShop(IRunState? runState)
	{
		if (runState?.CurrentRoom is not MerchantRoom room)
		{
			return false;
		}

		return RobSession.IsRobbed(room) || RobRunData.IsFreeShopHere(runState);
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantPrice))]
	[HarmonyPostfix]
	private static void ModifyMerchantPrice_Postfix(IRunState __0, MerchantEntry __2, ref decimal __result)
	{
		if (__result <= 0m)
		{
			return;
		}

		// 暂时把删卡服务也一起归零（按你的要求）。
		// 注意：原版购买删卡服务时走 OneOffSynchronizer.DoLocalMerchantCardRemoval(goldCost)，
		// goldCost = Cost = 0 → 只扣 0 金币，删卡本身正常；用过后 Used=true，不会无限次。
		if (IsRobbingShop(__0))
		{
			__result = 0m;
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldRefillMerchantEntry))]
	[HarmonyPostfix]
	private static void ShouldRefillMerchantEntry_Postfix(IRunState __0, ref bool __result)
	{
		if (__result && IsRobbingShop(__0))
		{
			Log.Info("[RobMerchant] 抢劫商店内禁用补货（TheCourier）");
			__result = false;
		}
	}
}
