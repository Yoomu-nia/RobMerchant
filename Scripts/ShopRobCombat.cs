using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 从商店发起“假商人”战斗。
///
/// 做法和原版假商人事件一致：
/// - 用原版遭遇战 FakeMerchantEventEncounter（就是那只假商人怪）
/// - CombatRoom 默认 ShouldResumeParentEventAfterCombat = true，所以战后点“继续”会走
///   RunManager.ProceedFromTerminalRewardsScreen -> ResumePreviousRoom -> 下层房间 Resume
/// - 用 EnterRoomWithoutExitingCurrentRoom 把战斗“压”在商店上面，商店实例（含货架）留在房间栈底
/// </summary>
internal static class ShopRobCombat
{
	public static async void Start(MerchantRoom shop)
	{
		try
		{
			var runManager = RunManager.Instance;
			if (runManager == null || !runManager.IsInProgress)
			{
				Log.Warn("[RobMerchant] 不在局内，忽略抢劫请求");
				return;
			}

			// RunManager.State 是 private，这里从商店玩家身上取当前局状态
			IRunState runState = shop.GetLocalInventory().Player.RunState;
			if (runState == null)
			{
				Log.Warn("[RobMerchant] 取不到 RunState，忽略抢劫请求");
				return;
			}

			RobSession.EnsureRun(runState);
			RobSession.BeginRob(shop);
			RobRunData.MarkRobbed(runState);

			var encounter = ModelDb.Encounter<FakeMerchantEventEncounter>().ToMutable();
			var combatRoom = new CombatRoom(encounter, runState);

			Log.Info("[RobMerchant] 进入商人战斗");
			await runManager.EnterRoomWithoutExitingCurrentRoom(combatRoom, fadeToBlack: true);
		}
		catch (Exception e)
		{
			Log.Error("[RobMerchant] 启动商人战斗失败: " + e);
			RobSession.Clear();
		}
	}
}
