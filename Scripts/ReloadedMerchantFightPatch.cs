using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 修复“战斗胜利后存档退出，读档回不到商店”的问题。
///
/// 背景（源码已证实）：
/// - 战斗胜利瞬间游戏会 `room.MarkPreFinished(); SaveRun(room)`，存档里是“已结束的战斗房”
/// - 读档后房间栈里**只有**这间战斗房（`CurrentRoomCount == 1`）
/// - 点“继续”时 `RunManager.ProceedFromTerminalRewardsScreen` 因为 `CurrentRoomCount == 1`
///   直接打开地图，商店就没了
///
/// 解决：在 ProceedFromTerminalRewardsScreen 上加前缀，如果满足
///   “只有一间已结束的战斗房 + 遭遇战是假商人 + 本局已抢劫”
/// 就不开地图，改为进入一间新的免费商店。
///
/// 注意与正常流程的区别：正常打赢后商店还压在栈底（`CurrentRoomCount == 2`），
/// 走的是原版 `ResumePreviousRoom` → 我们补写的 `MerchantRoom.Resume`，不受这里影响。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class ReloadedMerchantFightPatch
{
	private static bool Prefix(RunManager __instance, ref Task __result)
	{
		RunState? state = __instance.DebugOnlyGetState();
		if (state == null || state.CurrentRoomCount != 1)
		{
			return true;
		}

		if (state.CurrentRoom is not CombatRoom room || !room.IsPreFinished)
		{
			return true;
		}

		if (room.Encounter is not FakeMerchantEventEncounter)
		{
			return true;
		}

		if (!RobRunData.HasRobbed(state))
		{
			// 本局没抢过（理论上不会走到），交还给原版
			return true;
		}

		__result = ReopenFreeShopAsync(__instance, state);
		return false;
	}

	private static async Task ReopenFreeShopAsync(RunManager runManager, RunState state)
	{
		try
		{
			// 关掉奖励界面/地图，避免叠在新房间上面
			NOverlayStack.Instance?.Clear();
			NMapScreen.Instance?.Close(animateOut: false);

			var shop = new MerchantRoom();
			RobSession.MarkFreeShop(shop, state);

			Log.Info("[RobMerchant] 读档后重新进入免费商店");
			await runManager.EnterRoom(shop);
		}
		catch (Exception e)
		{
			Log.Error("[RobMerchant] 读档后重开商店失败: " + e);
		}
	}
}
