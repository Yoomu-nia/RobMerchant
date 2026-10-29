using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace RobMerchant;

/// <summary>
/// 原版 <c>MerchantRoom.Resume</c> 只有一句 <c>throw new NotImplementedException()</c>，
/// 因为原版从来没有“战斗结束回到商店”的流程。这里把它补上：
/// 重新创建一个 NMerchantRoom 界面节点，绑定到**同一个** MerchantRoom 实例，
/// 所以货架（MerchantRoom.Inventories）原样保留，买走的没了、没买的还在。
/// </summary>
[HarmonyPatch(typeof(MerchantRoom), nameof(MerchantRoom.Resume))]
internal static class MerchantRoomResumePatch
{
	// 参数用 __0/__1 按位置取，避免和原方法参数名冲突
	private static bool Prefix(MerchantRoom __instance, AbstractRoom __0, IRunState? __1, ref Task __result)
	{
		if (!RobSession.TryConsume(__instance))
		{
			// 不是我们发起的抢劫 → 保持原版行为（抛异常）
			return true;
		}

		__result = ReopenAsync(__instance, __1);
		return false;
	}

	private static async Task ReopenAsync(MerchantRoom shop, IRunState? runState)
	{
		try
		{
			// 保险：确保商店资源还在缓存里（正常情况从商店出发时已经加载过）
			await PreloadManager.LoadRoomMerchantAssets();

			IReadOnlyList<Player> players = runState?.Players ?? Array.Empty<Player>();
			NRun.Instance?.SetCurrentRoom(NMerchantRoom.Create(shop, players));
			NMapScreen.Instance?.SetTravelEnabled(true);

			Log.Info("[RobMerchant] 战斗结束，已返回原商店");

			// 战斗胜利时我们跳过了自动存档（见 SkipVictorySavePatch），
			// 改在这里存一次：此时人正在免费商店里，存档点就是这家商店节点。
			await SaveManager.Instance.SaveRun(null, saveProgress: false);
			Log.Info("[RobMerchant] 已在免费商店内存档");
		}
		catch (Exception e)
		{
			Log.Error("[RobMerchant] 返回商店失败: " + e);
		}
	}
}
