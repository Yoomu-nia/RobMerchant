using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 在商店界面（NMerchantRoom）加载完成后，加一个“抢劫商人”按钮。
///
/// 三种情况：
/// 1. 普通商店：显示按钮
/// 2. 刚抢完、战斗结束回来的那家商店：不显示按钮（但价格已经是 0）
/// 3. 本局已经抢过商人，之后进入的商店：隐藏商人，玩家只能离开
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
internal static class ShopRobButtonPatch
{
	private const string ButtonName = "RobMerchantButton";

	private static void Postfix(NMerchantRoom __instance)
	{
		if (__instance.GetNodeOrNull<Button>(ButtonName) != null)
		{
			return;
		}

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
			Log.Info("[RobMerchant] 该商店已被抢劫，不重复生成按钮");
			return;
		}

		var button = new Button
		{
			Name = ButtonName,
			Text = "抢劫商人",
			CustomMinimumSize = new Vector2(168, 52),
			MouseFilter = Control.MouseFilterEnum.Stop
		};

		// 左下角，避开顶栏与右侧的继续按钮
		button.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		button.OffsetLeft = 40;
		button.OffsetTop = -92;
		button.OffsetRight = 208;
		button.OffsetBottom = -40;

		button.Pressed += () => OnPressed(__instance);
		__instance.AddChild(button);

		Log.Info("[RobMerchant] 商店按钮已创建");
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

	private static void OnPressed(NMerchantRoom room)
	{
		if (room.GetNodeOrNull<NRobConfirmDialog>("RobMerchantConfirmDialog") != null)
		{
			return;
		}

		var dialog = new NRobConfirmDialog();
		dialog.Confirmed += () => ShopRobCombat.Start(room.Room);
		room.AddChild(dialog);
	}
}
