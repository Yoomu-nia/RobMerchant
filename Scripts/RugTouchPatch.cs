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
/// v0.113 起商店拆成了 <c>NStandardMerchantRoom</c>（普通商店）和
/// <c>NBlitzMerchantRoom</c>（Blitz 模式），基类 <c>NMerchantRoom</c> 变抽象。
/// 这里只挂普通商店，Blitz 模式不介入。
/// </summary>
[HarmonyPatch(typeof(NStandardMerchantRoom), nameof(NStandardMerchantRoom._Ready))]
internal static class RugTouchPatch
{
	private const string NodeName = "RobMerchantRugTouch";

	private static void Postfix(NStandardMerchantRoom __instance)
	{
		if (__instance.GetNodeOrNull<RugTouchController>(NodeName) != null)
		{
			return;
		}

		RunState? runState = RunManager.Instance?.DebugOnlyGetState();
		if (runState == null)
		{
			Log.Warn("[RobMerchant] 拿不到 RunState，跳地毯按钮");
			return;
		}

		if (runState.CurrentRoom is not MerchantRoom shop)
		{
			Log.Warn("[RobMerchant] 当前不在商店房间，跳地毯按钮");
			return;
		}

		RobSession.EnsureRun(runState);

		// 读档重进免费商店时，历史记录已经在存档里，跳过重复追加
		RobSession.SuppressHistoryAppend = false;

		bool isFreeShop = RobFlags.IsFreeShop(runState, shop);
		bool robbedRun = RobFlags.HasRobbed(runState);

		if (!isFreeShop && robbedRun)
		{
			HideMerchant(__instance);
			return;
		}

		if (isFreeShop)
		{
			Log.Info("[RobMerchant] 免费商店：不显示抢劫入口");
			return;
		}

		var ui = new RugTouchController();
		__instance.AddChild(ui);

		// 等布局完成后再定位（此时商人按钮的 rect 才是真实值）
		Callable.From(() => ui.PlaceNearRug(__instance)).CallDeferred();

		Log.Info("[RobMerchant] 地毯按钮已加入商店房间");
	}

	/// <summary>本局已经抢过商人：把这家商店的商人藏起来，不给交互。</summary>
	private static void HideMerchant(NStandardMerchantRoom room)
	{
		NMerchantButton? button = room.MerchantButton;
		if (button != null)
		{
			button.Visible = false;
			button.MouseFilter = Control.MouseFilterEnum.Ignore;
			button.Disable();
		}

		Log.Info("[RobMerchant] 本局已抢劫过商人，本商店不再出现商人");
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
