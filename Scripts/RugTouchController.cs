using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace RobMerchant;

/// <summary>
/// 购买界面里的「碰一下地毯」按钮。
///
/// - 挂在 NMerchantInventory 节点下（随购买界面一起显隐）
/// - 每次点击先播一句商人警告台词（原版气泡，纯文本）
/// - 台词播完后，再点一次弹出确认界面
/// - 计数值只存在这个节点里：离开商店/房间销毁时自动消失，不写存档
/// </summary>
public partial class RugTouchController : Control
{
	private int _touches;
	private NSpeechBubbleVfx? _lastBubble;
	private Button _button = null!;

	public RugTouchController()
	{
		Name = "RobMerchantRugTouch";
		MouseFilter = MouseFilterEnum.Ignore;

		// 购买界面底部中间
		SetAnchorsPreset(LayoutPreset.CenterBottom);
		OffsetLeft = -110f;
		OffsetRight = 110f;
		OffsetTop = -76f;
		OffsetBottom = -20f;

		_button = new Button
		{
			Name = "TouchRugButton",
			Text = RobConfig.TouchButtonText,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop
		};
		_button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_button.Pressed += OnTouchPressed;
		AddChild(_button);

		Visible = false;
	}

	private void OnTouchPressed()
	{
		_touches++;
		Log.Info($"[RobMerchant] 触碰地毯 第 {_touches} 次");

		if (_touches <= RobConfig.TouchLines.Length)
		{
			ShowLine(RobConfig.TouchLines[_touches - 1]);
			return;
		}

		OpenConfirm();
	}

	private void ShowLine(string line)
	{
		NMerchantRoom? room = NMerchantRoom.Instance;
		NMerchantButton? merchantButton = room?.MerchantButton;

		Vector2 position = merchantButton != null
			? merchantButton.GlobalPosition + merchantButton.Size.X * Vector2.Left
			: GlobalPosition + new Vector2(0, -60);

		NSpeechBubbleVfx? bubble = NSpeechBubbleVfx.Create(
			line,
			DialogueSide.Right,
			position,
			RobConfig.BubbleSeconds,
			VfxColor.Blue);

		if (bubble == null)
		{
			Log.Warn("[RobMerchant] 气泡创建失败");
			return;
		}

		// 连续的台词不堆叠
		if (_lastBubble != null && IsInstanceValid(_lastBubble))
		{
			_lastBubble.QueueFree();
		}

		_lastBubble = bubble;
		(merchantButton?.GetParent() ?? GetParent() ?? this).AddChild(bubble);
	}

	private void OpenConfirm()
	{
		NModalContainer? modal = NModalContainer.Instance;
		if (modal == null)
		{
			Log.Warn("[RobMerchant] 找不到 NModalContainer，无法弹确认框");
			return;
		}

		if (modal.OpenModal != null)
		{
			Log.Warn("[RobMerchant] 已有其它弹窗打开，本次忽略");
			return;
		}

		NRobConfirmPopup? popup = NRobConfirmPopup.Create(OnConfirmed, OnCancelled);
		if (popup != null)
		{
			modal.Add(popup);
			Log.Info("[RobMerchant] 打开抢劫确认弹窗（原版样式）");
			return;
		}

		// 原版弹窗场景加载失败时的回退：代码自绘弹窗
		NMerchantRoom? room = NMerchantRoom.Instance;
		if (room == null)
		{
			return;
		}

		var fallback = new NRobConfirmDialog();
		fallback.Confirmed += OnConfirmed;
		fallback.Cancelled += OnCancelled;
		room.AddChild(fallback);
		Log.Info("[RobMerchant] 打开抢劫确认弹窗（自绘回退）");
	}

	private void OnConfirmed()
	{
		Log.Info("[RobMerchant] 确认抢劫（阶段1：尚未接战斗）");
	}

	private void OnCancelled()
	{
		Log.Info("[RobMerchant] 取消抢劫");
	}
}
