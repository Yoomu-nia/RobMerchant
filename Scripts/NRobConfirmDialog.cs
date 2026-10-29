using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace RobMerchant;

/// <summary>
/// “抢劫商人”的确认弹窗。
/// 纯代码构建（不依赖游戏的 tscn），全屏半透明遮罩 + 居中面板 + 确认/取消。
/// 这样不会因为某个场景没被预加载而报错。
/// </summary>
public partial class NRobConfirmDialog : Control
{
	public event Action? Confirmed;
	public event Action? Cancelled;

	private Button _yesButton = null!;

	public NRobConfirmDialog()
	{
		Name = "RobMerchantConfirmDialog";
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;

		// 遮罩
		var backstop = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop
		};
		backstop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backstop);

		// 居中容器
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
		center.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 28);
		margin.AddThemeConstantOverride("margin_right", 28);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 18);
		margin.AddChild(vbox);

		var title = new Label
		{
			Text = "抢劫商人",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		vbox.AddChild(title);

		var body = new Label
		{
			Text = "你准备抢光这家店。\n商人不会善罢甘休——必须先打赢他。\n\n"
			     + "胜利后你会回到这家商店，所有商品售价变为 0，由你自行挑选。\n"
			     + "失败则按正常死亡处理。\n\n确定要动手吗？",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		vbox.AddChild(body);

		var buttons = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttons.AddThemeConstantOverride("separation", 24);
		vbox.AddChild(buttons);

		var no = new Button { Text = "取消", CustomMinimumSize = new Vector2(160, 48) };
		no.Pressed += () =>
		{
			Log.Info("[RobMerchant] 取消抢劫");
			Cancelled?.Invoke();
			Close();
		};
		buttons.AddChild(no);

		_yesButton = new Button { Text = "动手", CustomMinimumSize = new Vector2(160, 48) };
		_yesButton.Pressed += () =>
		{
			Log.Info("[RobMerchant] 确认抢劫");
			Confirmed?.Invoke();
			Close();
		};
		buttons.AddChild(_yesButton);

		VisibilityChanged += () =>
		{
			if (IsVisibleInTree() && _yesButton != null)
			{
				_yesButton.GrabFocus();
			}
		};
	}

	private void Close()
	{
		QueueFree();
	}
}
