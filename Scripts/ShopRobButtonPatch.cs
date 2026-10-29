using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RobMerchant;

/// <summary>
/// 在商店界面（NMerchantRoom）加载完成后，加一个“抢劫商人”按钮。
/// 阶段 2 只做按钮 + 确认弹窗；确认后的战斗逻辑在下一阶段接入。
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

	private static void OnPressed(NMerchantRoom room)
	{
		if (room.GetNodeOrNull<NRobConfirmDialog>("RobMerchantConfirmDialog") != null)
		{
			return;
		}

		var dialog = new NRobConfirmDialog();
		dialog.Confirmed += () =>
		{
			// 阶段 3：这里启动商人战斗
			Log.Info("[RobMerchant] （阶段3将在这里启动战斗）");
		};
		room.AddChild(dialog);
	}
}
