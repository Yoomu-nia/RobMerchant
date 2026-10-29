using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace RobMerchant;

/// <summary>
/// 跳过“商人战斗胜利瞬间”的自动存档。
///
/// 原版流程（源码已证实）：战斗胜利时
/// <c>room.MarkPreFinished(); await SaveManager.Instance.SaveRun(room, saveProgress: false);</c>
/// 存档里是“已结束的战斗房”，读档后只能进入那间战斗房，商店就丢了。
///
/// 我们的做法：跳过这一次存档，改为回到免费商店后由
/// <see cref="MerchantRoomResumePatch"/> 在商店里存一次，
/// 这样存档点是“免费商店节点”，读档会重新进入同一家商店。
/// </summary>
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveRun), new[] { typeof(AbstractRoom), typeof(bool) })]
internal static class SkipVictorySavePatch
{
	private static bool Prefix(AbstractRoom? __0, ref Task __result)
	{
		if (__0 is CombatRoom { IsPreFinished: true } room && room.Encounter is FakeMerchantEventEncounter)
		{
			Log.Info("[RobMerchant] 跳过商人战斗胜利时的自动存档（稍后在免费商店内存档）");
			__result = Task.CompletedTask;
			return false;
		}

		return true;
	}
}
