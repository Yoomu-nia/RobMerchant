using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace RobMerchant;

/// <summary>本局持久化数据：是否已经抢劫过商人。</summary>
public sealed class RobRunStateData
{
	public bool HasRobbedShop { get; set; }
}

/// <summary>
/// 用 RitsuLib 的 <see cref="RunSavedData{T}"/> 保存“本局已抢劫”标记。
/// - 随本局存档一起保存，读档后仍然有效
/// - 新开一局时是默认值（false），自动重置
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
		if (_slot == null || runState is not RunState state)
		{
			return false;
		}

		try
		{
			return _slot.Get(state).HasRobbedShop;
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 读取单局数据失败: " + e.Message);
			return false;
		}
	}

	public static void MarkRobbed(IRunState? runState)
	{
		if (_slot == null || runState is not RunState state)
		{
			return;
		}

		try
		{
			_slot.Modify(state, static data => data.HasRobbedShop = true);
			Log.Info("[RobMerchant] 已记录：本局抢劫过商人");
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 写入单局数据失败: " + e.Message);
		}
	}
}
