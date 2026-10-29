using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 把「碰一下地毯」按钮挂进购买界面（NMerchantInventory）。
///
/// 为什么用 Initialize 而不是 _Ready：
/// NMerchantRoom._Ready 里先 GetNode("%Inventory") 再 Inventory.Initialize(...)，
/// 也就是说库存数据是在 _Ready 之后才注入的；Initialize 结束时才拿得到 Player/RunState。
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class RugTouchPatch
{
	private const string NodeName = "RobMerchantRugTouch";

	private static void Postfix(NMerchantInventory __instance)
	{
		if (__instance.GetNodeOrNull<RugTouchController>(NodeName) != null)
		{
			return;
		}

		MerchantInventory? inventory = __instance.Inventory;
		IRunState? runState = inventory?.Player?.RunState;
		NMerchantRoom? room = NMerchantRoom.Instance ?? __instance.GetParent() as NMerchantRoom;

		if (runState == null || room == null)
		{
			Log.Warn("[RobMerchant] 拿不到 RunState/Room，跳过地毯按钮");
			return;
		}

		RobSession.EnsureRun(runState);

		bool isFreeShop = RobSession.IsRobbed(room.Room) || RobRunData.IsFreeShopHere(runState);
		bool robbedRun = RobRunData.HasRobbed(runState);

		if (!isFreeShop && robbedRun)
		{
			// 本局已抢过：这家商店没有商人，也就不给碰地毯的按钮
			Log.Info("[RobMerchant] 本局已抢劫过，本商店不添加地毯按钮");
			return;
		}

		if (isFreeShop)
		{
			Log.Info("[RobMerchant] 免费商店不添加地毯按钮");
			return;
		}

		var ui = new RugTouchController { Visible = __instance.IsOpen };
		__instance.AddChild(ui);

		Log.Info("[RobMerchant] 地毯按钮已加入购买界面");
	}
}

/// <summary>打开购买界面时显示按钮。</summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class RugTouchOpenPatch
{
	private static void Postfix(NMerchantInventory __instance)
	{
		if (__instance.GetNodeOrNull<Control>("RobMerchantRugTouch") is { } ui)
		{
			ui.Visible = true;
		}
	}
}

/// <summary>关闭购买界面时隐藏按钮（计数保留，下次打开继续）。</summary>
[HarmonyPatch(typeof(NMerchantInventory), "Close")]
internal static class RugTouchClosePatch
{
	private static void Postfix(NMerchantInventory __instance)
	{
		if (__instance.GetNodeOrNull<Control>("RobMerchantRugTouch") is { } ui)
		{
			ui.Visible = false;
		}
	}
}
