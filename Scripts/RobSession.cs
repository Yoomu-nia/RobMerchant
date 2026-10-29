using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 本局内的临时状态（只放内存，不涉及存档）：
/// - 哪一家商店刚被抢过（战后要回到它、价格 0）
/// - 哪一家商店正在等待战斗结束
///
/// 注意：跨局不能残留，所以每次商店界面创建时都会调用 <see cref="EnsureRun"/>，
/// 发现 RunState 实例变了（新的一局）就整体清空。
/// </summary>
internal static class RobSession
{
	private static readonly HashSet<MerchantRoom> Robbed = new();

	private static IRunState? _runState;

	/// <summary>正在抢劫的那家商店（战斗结束后要回到它）。</summary>
	public static MerchantRoom? PendingShop { get; private set; }

	/// <summary>如果换了新的一局，清空上一局的内存状态。</summary>
	public static void EnsureRun(IRunState? runState)
	{
		if (runState == null || ReferenceEquals(_runState, runState))
		{
			return;
		}

		if (_runState != null)
		{
			Log.Info("[RobMerchant] 检测到新的 RunState，清空上一局的抢劫状态");
		}

		Robbed.Clear();
		PendingShop = null;
		_runState = runState;
	}

	/// <summary>这家商店是否已经被抢过（价格 0 / 不再出按钮）。</summary>
	public static bool IsRobbed(MerchantRoom shop) => Robbed.Contains(shop);

	/// <summary>
	/// 读档后重新开出来的一间免费商店：登记成“已抢劫”，
	/// 这样价格归零补丁会生效、界面也不会再生成抢劫按钮。
	/// </summary>
	public static void MarkFreeShop(MerchantRoom shop, IRunState? runState)
	{
		EnsureRun(runState);
		Robbed.Add(shop);
		Log.Info("[RobMerchant] 读档后登记免费商店");
	}

	public static void BeginRob(MerchantRoom shop)
	{
		PendingShop = shop;
		Robbed.Add(shop);
		Log.Info("[RobMerchant] 标记抢劫商店，准备进入商人战斗");
	}

	/// <summary>战后代商店界面重建时调用，返回是否是本次要回的商店。</summary>
	public static bool TryConsume(MerchantRoom room)
	{
		if (!ReferenceEquals(PendingShop, room))
		{
			return false;
		}

		PendingShop = null;
		return true;
	}

	public static void Clear()
	{
		PendingShop = null;
	}
}
