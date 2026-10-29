using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace RobMerchant;

/// <summary>本局持久化数据：是否抢过商人，以及那家“免费商店”在地图上的位置。</summary>
public sealed class RobRunStateData
{
	public bool HasRobbedShop { get; set; }

	// 免费商店所在的章节与地图坐标（-1 表示未记录）
	public int FreeShopAct { get; set; } = -1;
	public int FreeShopCol { get; set; } = -1;
	public int FreeShopRow { get; set; } = -1;
}

/// <summary>
/// 用 RitsuLib 的 <see cref="RunSavedData{T}"/> 保存本局状态：
/// - 本局是否抢过商人（决定后续商店有没有商人）
/// - 被抢的那家商店在哪（决定读档回来时哪个节点算“免费商店”）
///
/// 新开一局时是默认值，自动重置。
/// </summary>
internal static class RobRunData
{
	private const string SlotKey = "rob_state";

	private static RunSavedData<RobRunStateData>? _slot;

	public static void Register()
	{
		using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
		{
			var store = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId);
			_slot = store.Register(
				key: SlotKey,
				defaultFactory: static () => new RobRunStateData(),
				options: new RunSavedDataOptions
				{
					WritePolicy = RunSavedDataWritePolicy.WhenNonDefault
				});
		}

		Log.Info("[RobMerchant] 单局数据槽位已注册");
	}

	public static bool HasRobbed(IRunState? runState)
	{
		return TryGet(runState)?.HasRobbedShop ?? false;
	}

	/// <summary>当前所在的商店节点，是否就是被抢的那家（用于价格归零 / 读档重进）。</summary>
	public static bool IsFreeShopHere(IRunState? runState)
	{
		var data = TryGet(runState);
		if (data == null || !data.HasRobbedShop)
		{
			return false;
		}

		if (runState is not RunState state || state.CurrentMapCoord is not MapCoord coord)
		{
			return false;
		}

		return data.FreeShopAct == state.CurrentActIndex
			&& data.FreeShopCol == coord.col
			&& data.FreeShopRow == coord.row;
	}

	/// <summary>记录本局已抢劫，并记住被抢商店的位置。</summary>
	public static void MarkRobbed(IRunState? runState)
	{
		if (_slot == null || runState is not RunState state)
		{
			return;
		}

		MapCoord? coord = state.CurrentMapCoord;
		int act = state.CurrentActIndex;

		try
		{
			_slot.Modify(state, data =>
			{
				data.HasRobbedShop = true;
				data.FreeShopAct = act;
				data.FreeShopCol = coord?.col ?? -1;
				data.FreeShopRow = coord?.row ?? -1;
			});
			Log.Info($"[RobMerchant] 已记录：本局抢劫过商人（节点 act={act} col={coord?.col} row={coord?.row}）");
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 写入单局数据失败: " + e.Message);
		}
	}

	private static RobRunStateData? TryGet(IRunState? runState)
	{
		if (_slot == null || runState is not RunState state)
		{
			return null;
		}

		try
		{
			return _slot.Get(state);
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 读取单局数据失败: " + e.Message);
			return null;
		}
	}
}
