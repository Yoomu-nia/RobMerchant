using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace RobMerchant;

/// <summary>“抢劫商人”网络动作的载荷：只带定位信息，各端据此自行校验。</summary>
public sealed class RobPayload
{
	public int ActIndex { get; set; }
	public int Col { get; set; }
	public int Row { get; set; }
}

/// <summary>
/// 把“抢劫商人”做成网络动作。
///
/// STS2 的联机模型是“主机权威 + 动作广播 + 各端确定性执行”：
/// 原版事件战斗也是每个端各自 EnterCombat，而不是主机把状态发给客户端。
/// 所以这里只广播“谁在哪个商店节点发起了抢劫”，
/// 各端在自己的进程里跑同一段逻辑（标记 + 进战斗）。
///
/// 动作类型必须是 NonCombat：商店不在战斗中。
/// </summary>
internal static class RobNetAction
{
	public static readonly RitsuLibManagedNetActionDescriptor<RobPayload> Descriptor = new(
		ModuleId: Entry.ModId,
		ActionKey: "rob_merchant_v1",
		Serialize: static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
		Deserialize: static bytes => JsonSerializer.Deserialize<RobPayload>(bytes)!,
		Execute: ExecuteAsync,
		ActionType: GameActionType.NonCombat);

	public static void Register()
	{
		RitsuLibManagedNetActions.Register(Descriptor);
		Log.Info("[RobMerchant] 抢劫网络动作已注册");
	}

	/// <summary>请求发起抢劫（单人也走同一条动作队列）。返回 false 表示入队失败。</summary>
	public static bool Request(RunManager runManager, RobPayload payload)
	{
		return RitsuLibManagedNetActions.Request(runManager, Descriptor, payload);
	}

	/// <summary>在所有端执行：校验 → 标记 → 进入商人战斗。</summary>
	private static Task ExecuteAsync(RitsuLibManagedNetActionContext<RobPayload> context)
	{
		RunManager? runManager = RunManager.Instance;
		RunState? state = runManager?.DebugOnlyGetState();
		if (runManager == null || state == null)
		{
			return Task.CompletedTask;
		}

		RobPayload payload = context.Message;
		MapCoord? coord = state.CurrentMapCoord;

		// 各端用同一份 payload + 自己的 runState 做校验，保证结论一致
		if (coord is not MapCoord current ||
		    current.col != payload.Col ||
		    current.row != payload.Row ||
		    state.CurrentActIndex != payload.ActIndex)
		{
			Log.Warn($"[RobMerchant] 忽略抢劫动作：坐标不匹配（当前 act={state.CurrentActIndex} " +
			         $"col={coord?.col} row={coord?.row}，动作 act={payload.ActIndex} col={payload.Col} row={payload.Row}）");
			return Task.CompletedTask;
		}

		if (state.CurrentRoom is not MerchantRoom shop)
		{
			Log.Warn("[RobMerchant] 忽略抢劫动作：当前不在商店");
			return Task.CompletedTask;
		}

		// 先到先得：已经抢过 / 正在抢，后来的动作直接作废
		if (RobSession.IsRobbed(shop) || RobFlags.HasRobbed(state))
		{
			Log.Info("[RobMerchant] 忽略抢劫动作：本商店已处理过");
			return Task.CompletedTask;
		}

		RobSession.EnsureRun(state);
		RobSession.BeginRob(shop);
		RobRunData.MarkRobbed(state);

		var encounter = ModelDb.Encounter<FakeMerchantEventEncounter>().ToMutable();
		var combatRoom = new CombatRoom(encounter, state);

		Log.Info("[RobMerchant] 执行抢劫动作：进入商人战斗");

		// 和原版 EventCombatSynchronizer.EnterCombat 一样：不 await 房间切换，
		// 否则会在动作队列里等待自身完成而死锁。
		TaskHelper.RunSafely(runManager.EnterRoomWithoutExitingCurrentRoom(combatRoom, fadeToBlack: true));
		return Task.CompletedTask;
	}
}
