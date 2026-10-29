using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 商店房间加载后处理“商人是否出现”。
///
/// 交互入口已经改成购买界面里的「碰一下地毯」（见 <see cref="RugTouchController"/>），
/// 这里只负责：
/// 1. 本局已经抢过商人 → 把商人藏起来（后续商店不再出现商人）
/// 2. 刚刚抢完回到的那家免费商店 → 什么都不做（价格 0 由价格补丁处理）
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
internal static class ShopRobButtonPatch
{
	private static void Postfix(NMerchantRoom __instance)
	{
		MerchantRoom shop = __instance.Room;
		IRunState? runState = shop.GetLocalInventory().Player.RunState;

		RobSession.EnsureRun(runState);

		// 读档重进免费商店时，历史记录已经在存档里，跳过重复追加
		RobSession.SuppressHistoryAppend = false;

		bool isFreeShop = RobSession.IsRobbed(shop) || RobRunData.IsFreeShopHere(runState);
		bool robbedRun = RobRunData.HasRobbed(runState);

		if (!isFreeShop && robbedRun)
		{
			HideMerchant(__instance);
			return;
		}

		if (isFreeShop)
		{
			Log.Info("[RobMerchant] 免费商店：不显示抢劫入口");
		}
	}

	/// <summary>本局已经抢过商人：把这家商店的商人藏起来，不给交互。</summary>
	private static void HideMerchant(NMerchantRoom room)
	{
		var button = room.MerchantButton;
		if (button != null)
		{
			button.Visible = false;
			button.MouseFilter = Control.MouseFilterEnum.Ignore;
			button.Disable();
		}

		Log.Info("[RobMerchant] 本局已抢劫过商人，本商店不再出现商人");
	}
}
