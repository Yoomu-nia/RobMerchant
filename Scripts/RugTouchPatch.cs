using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 把「碰一下地毯」按钮挂到商店房间（不是购买界面），所以不打开购买界面也能看到。
///
/// 位置在 <see cref="RugTouchController.PlaceNearRug"/> 里计算：
/// 优先找名字含 rug 的节点，找不到就退化为商人按钮正下方。
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
internal static class RugTouchPatch
{
	private const string NodeName = "RobMerchantRugTouch";

	private static void Postfix(NMerchantRoom __instance)
	{
		if (__instance.GetNodeOrNull<RugTouchController>(NodeName) != null)
		{
			return;
		}

		MerchantRoom shop = __instance.Room;
		IRunState? runState = shop.GetLocalInventory().Player.RunState;
		if (runState == null)
		{
			Log.Warn("[RobMerchant] 拿不到 RunState，跳地毯按钮");
			return;
		}

		RobSession.EnsureRun(runState);

		bool isFreeShop = RobFlags.IsFreeShop(runState, shop);
		bool robbedRun = RobFlags.HasRobbed(runState);

		if (!isFreeShop && robbedRun)
		{
			// 本局已抢过：这家商店没有商人，也就没有地毯可碰
			Log.Info("[RobMerchant] 本局已抢劫过，本商店不添加地毯按钮");
			return;
		}

		if (isFreeShop)
		{
			Log.Info("[RobMerchant] 免费商店不添加地毯按钮");
			return;
		}

		var ui = new RugTouchController();
		__instance.AddChild(ui);

		// 等布局完成后再定位（此时商人按钮的 rect 才是真实值）
		Callable.From(() => ui.PlaceNearRug(__instance)).CallDeferred();

		Log.Info("[RobMerchant] 地毯按钮已加入商店房间");
	}
}

/// <summary>打开购买界面时把地毯按钮藏起来，避免盖在商店面板上。</summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class RugTouchOpenPatch
{
	private static void Postfix(NMerchantInventory __instance)
	{
		if (NMerchantRoom.Instance?.GetNodeOrNull<Control>("RobMerchantRugTouch") is { } ui)
		{
			ui.Visible = false;
		}
	}
}

/// <summary>关闭购买界面时重新显示（计数保留）。</summary>
[HarmonyPatch(typeof(NMerchantInventory), "Close")]
internal static class RugTouchClosePatch
{
	private static void Postfix(NMerchantInventory __instance)
	{
		if (NMerchantRoom.Instance?.GetNodeOrNull<Control>("RobMerchantRugTouch") is { } ui)
		{
			ui.Visible = true;
		}
	}
}
