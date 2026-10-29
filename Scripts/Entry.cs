using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace RobMerchant;

/// <summary>
/// Mod 入口。目前只做初始化与日志，用于验证“新工程能编译、能被游戏加载”。
/// 后续阶段会在这里注册补丁与单局数据。
/// </summary>
[ModInitializer(nameof(Init))]
public class Entry
{
	public const string ModId = "RobMerchant";

	public static void Init()
	{
		var harmony = new Harmony("com.QAQ.RobMerchant");
		harmony.PatchAll();

		// 让 tscn 能加载本程序集里的脚本
		ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);

		// 注册“本局已抢劫”的持久化数据槽位
		try
		{
			RobRunData.Register();
		}
		catch (Exception e)
		{
			Log.Error("[RobMerchant] 注册单局数据失败: " + e);
		}

		Log.Info("[RobMerchant] initialized (stage 5)");
	}
}
