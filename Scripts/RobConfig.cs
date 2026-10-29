namespace RobMerchant;

/// <summary>
/// 交互文案与数值配置。想改台词/阈值/弹窗文字，只改这里就行。
/// </summary>
internal static class RobConfig
{
	/// <summary>
	/// 触碰地毯时依次播放的警告台词（按点击次数）。
	/// 全部播完后，再点一次就弹确认框。
	/// </summary>
	public static readonly string[] TouchLines =
	[
		"我这可是非卖品",
		"买不起别碰",
		"你是不是存心找麻烦？",
		"够了！你到底想干什么？",
	];

	/// <summary>购买界面里的按钮文字。</summary>
	public const string TouchButtonText = "碰一下地毯";

	/// <summary>气泡停留秒数（原版 PlayDialogue 默认 2.0）。</summary>
	public const double BubbleSeconds = 2.5;

	/// <summary>确认弹窗标题。</summary>
	public const string ConfirmTitle = "抢劫商人";

	/// <summary>确认弹窗正文。</summary>
	public const string ConfirmBody = "商人已经警告过你了。确认动手吗？\n失败会按正常死亡处理。";
}
