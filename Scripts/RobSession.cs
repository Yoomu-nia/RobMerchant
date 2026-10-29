using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 记录“这次战斗是从哪个商店打出去的”，用于战后回到同一家商店。
/// 阶段 3 先放内存里；阶段 5 会换成 RitsuLib 的单局持久化数据。
/// </summary>
internal static class RobSession
{
	private static readonly HashSet<MerchantRoom> Robbed = new();

	/// <summary>正在抢劫的那家商店（战斗结束后要回到它）。</summary>
	public static MerchantRoom? PendingShop { get; private set; }

	/// <summary>这家商店是否已经被抢过（同一局内不重复刷按钮）。</summary>
	public static bool IsRobbed(MerchantRoom shop) => Robbed.Contains(shop);

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
