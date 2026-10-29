using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// “本局是否抢过商人 / 当前商店是不是免费商店”的统一判断。
///
/// 为什么不只依赖 RunSavedData：
/// 联机时动作会在每个端本地执行，但持久化数据槽位的写入权限与同步时机由 RitsuLib 决定，
/// 客户端不一定立刻拿到主机的值。所以内存里也记一份（<see cref="RobSession"/>），
/// 存档里的那份只负责“读档/重连后恢复”。
/// </summary>
internal static class RobFlags
{
	/// <summary>本局是否已经抢过商人（内存或存档任一为真）。</summary>
	public static bool HasRobbed(IRunState? runState)
	{
		return RobSession.HasRobbedRun || RobRunData.HasRobbed(runState);
	}

	/// <summary>当前这家商店是不是被抢过的那家（价格 0 / 不再出现商人）。</summary>
	public static bool IsFreeShop(IRunState? runState, MerchantRoom shop)
	{
		return RobSession.IsRobbed(shop) || RobRunData.IsFreeShopHere(runState);
	}
}
