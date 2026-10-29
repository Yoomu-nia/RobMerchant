using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace RobMerchant;

/// <summary>
/// 商店房间里的「碰一下地毯」按钮。
///
/// - 挂在 NMerchantRoom 节点下（不是购买界面），所以不进商店也能看到
/// - 位置尽量贴着商人脚下/地毯；找不到地毯节点时退化为“商人按钮正下方”
/// - 每次点击先播一句商人警告台词（原版气泡，纯文本）
/// - 台词播完后，再点一次弹出确认界面
/// - 计数值只存在这个节点里：离开商店/房间销毁时自动消失，不写存档
/// </summary>
public partial class RugTouchController : Control
{
	private static bool _dumpedTree;

	private int _touches;
	private NSpeechBubbleVfx? _lastBubble;
	private Button _button = null!;
	private bool _placed;

	public RugTouchController()
	{
		Name = "RobMerchantRugTouch";
		MouseFilter = MouseFilterEnum.Ignore;

		_button = new Button
		{
			Name = "TouchRugButton",
			Text = RobConfig.TouchButtonText,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
			CustomMinimumSize = new Vector2(200, 46)
		};
		_button.Size = new Vector2(200, 46);
		_button.Pressed += OnTouchPressed;
		AddChild(_button);
	}

	/// <summary>放到商人脚下 / 地毯旁边。布局完成后再调用（用 CallDeferred 触发）。</summary>
	public void PlaceNearRug(NMerchantRoom room)
	{
		if (_placed)
		{
			return;
		}

		_placed = true;
		DumpRoomNodesOnce(room);

		Vector2 target;
		Node? rug = FindRugNode(room);
		if (rug is Control rugControl && rugControl.Size != Vector2.Zero)
		{
			Rect2 rugRect = rugControl.GetGlobalRect();
			target = new Vector2(rugRect.GetCenter().X, rugRect.Position.Y);
			Log.Info($"[RobMerchant] 地毯节点(Control) {rug.Name} rect={rugRect}");
		}
		else if (rug is Node2D rugNode2D)
		{
			target = rugNode2D.GlobalPosition + new Vector2(0, -40f);
			Log.Info($"[RobMerchant] 地毯节点(Node2D) {rug.Name} pos={rugNode2D.GlobalPosition}");
		}
		else
		{
			Control? merchantButton = room.MerchantButton;
			if (merchantButton != null && merchantButton.Size != Vector2.Zero)
			{
				Rect2 rect = merchantButton.GetGlobalRect();
				// 地毯在商人脚下、并向左侧延伸：取商人按钮左下方作为落点
				target = new Vector2(
					rect.Position.X + rect.Size.X * 0.15f,
					rect.End.Y + rect.Size.Y * 0.24f);
				Log.Info($"[RobMerchant] 未找到地毯节点，放在商人脚下的地毯上 rect={rect}");
			}
			else
			{
				Rect2 roomRect = room.GetGlobalRect();
				target = new Vector2(roomRect.GetCenter().X, roomRect.Size.Y * 0.72f);
				Log.Info("[RobMerchant] 使用房间默认位置");
			}
		}

		Size = _button.Size;
		GlobalPosition = target - new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
		_button.Size = Size;
		Log.Info($"[RobMerchant] 地毯按钮位置 = {GlobalPosition}");
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

	/// <summary>在房间里找名字含 rug / carpet 的节点（Control 或 Node2D 都算）。</summary>
	private static Node? FindRugNode(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			string name = child.Name.ToString();
			if ((child is Control || child is Node2D) &&
			    name.Contains("rug", StringComparison.OrdinalIgnoreCase))
			{
				return child;
			}

			Node? deep = FindRugNode(child);
			if (deep != null)
			{
				return deep;
			}
		}

		return null;
	}

	/// <summary>一次性把关键节点打进日志（只打直接子节点 + 名字带 rug 的节点）。</summary>
	private static void DumpRoomNodesOnce(NMerchantRoom room)
	{
		if (_dumpedTree || !RobConfig.DumpRoomNodes)
		{
			return;
		}

		_dumpedTree = true;
		Log.Info("[RobMerchant] ==== 商店房间直接子节点 ====");
		foreach (Node child in room.GetChildren())
		{
			Log.Info("[RobMerchant]   " + Describe(child));
		}
		Log.Info("[RobMerchant] ==== 结束 ====");
	}

	private static string Describe(Node node)
	{
		string text = node.Name + " (" + node.GetType().Name + ")";
		if (node is Control control)
		{
			return text + " rect=" + control.GetGlobalRect();
		}

		if (node is Node2D node2D)
		{
			return text + " pos=" + node2D.GlobalPosition;
		}

		return text;
	}
}
