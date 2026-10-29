using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace RobMerchant;

/// <summary>
/// 商店房间里的「碰一下地毯」按钮。
///
/// UI 用原版配色自绘（奶油色文字 + 金色描边 + 深色半透明底板），字体从场景里已有的 MegaLabel 复制，
/// 避免出现 Godot 默认字体那种“出戏”的样子。
///
/// - 挂在 NMerchantRoom 节点下（不是购买界面）
/// - 每次点击先播一句商人警告台词（原版气泡，纯文本）
/// - 台词播完后，再点一次弹出确认界面
/// - 计数值只存在这个节点里：离开商店/房间销毁时自动消失，不写存档
/// </summary>
public partial class RugTouchController : Control
{
	private static bool _dumpedTree;

	private readonly Panel _plate = new();
	private readonly Label _label = new();

	private int _touches;
	private NSpeechBubbleVfx? _lastBubble;
	private bool _placed;

	private static readonly Color PlateColor = new(0.06f, 0.05f, 0.04f, 0.55f);
	private static readonly Color PlateColorHover = new(0.10f, 0.08f, 0.05f, 0.8f);
	private static readonly Color BorderColor = new(0.94f, 0.78f, 0.32f, 0.55f);
	private static readonly Color BorderColorHover = new(0.94f, 0.78f, 0.32f, 1f);

	public RugTouchController()
	{
		Name = "RobMerchantRugTouch";
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = new Vector2(228, 60);
		Size = new Vector2(228, 60);
		TooltipText = RobConfig.TouchButtonText;

		_plate.Name = "Plate";
		_plate.MouseFilter = MouseFilterEnum.Ignore;
		_plate.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_plate.AddThemeStyleboxOverride("panel", BuildStyle(PlateColor, BorderColor));
		AddChild(_plate);

		_label.Name = "Label";
		_label.Text = RobConfig.TouchButtonText;
		_label.MouseFilter = MouseFilterEnum.Ignore;
		_label.HorizontalAlignment = HorizontalAlignment.Center;
		_label.VerticalAlignment = VerticalAlignment.Center;
		_label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_label.AddThemeFontSizeOverride("font_size", 24);
		_label.AddThemeColorOverride("font_color", StsColors.cream);
		_label.AddThemeColorOverride("font_outline_color", Colors.Black);
		_label.AddThemeConstantOverride("outline_size", 6);
		AddChild(_label);

		MouseEntered += OnHoverIn;
		MouseExited += OnHoverOut;
	}

	public override void _Ready()
	{
		TryApplyGameFont();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			AcceptEvent();
			OnTouchPressed();
		}
	}

	private static StyleBoxFlat BuildStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(12);
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		return style;
	}

	/// <summary>从场景里已有的 MegaLabel 复制游戏字体，避免 Godot 默认字体。</summary>
	private void TryApplyGameFont()
	{
		MegaLabel? sample = FindFirstMegaLabel(GetTree()?.CurrentScene ?? GetParent());
		if (sample == null)
		{
			return;
		}

		try
		{
			Font font = sample.GetThemeFont("font", "Label");
			if (font != null)
			{
				_label.AddThemeFontOverride("font", font);
			}
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 复制字体失败: " + e.Message);
		}
	}

	private static MegaLabel? FindFirstMegaLabel(Node? node)
	{
		if (node == null)
		{
			return null;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is MegaLabel megaLabel)
			{
				return megaLabel;
			}

			MegaLabel? deep = FindFirstMegaLabel(child);
			if (deep != null)
			{
				return deep;
			}
		}

		return null;
	}

	private void OnHoverIn()
	{
		_plate.AddThemeStyleboxOverride("panel", BuildStyle(PlateColorHover, BorderColorHover));
		_label.AddThemeColorOverride("font_color", StsColors.gold);
		Scale = new Vector2(1.05f, 1.05f);
	}

	private void OnHoverOut()
	{
		_plate.AddThemeStyleboxOverride("panel", BuildStyle(PlateColor, BorderColor));
		_label.AddThemeColorOverride("font_color", StsColors.cream);
		Scale = Vector2.One;
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

		PivotOffset = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
		GlobalPosition = target - new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
		_plate.Size = Size;
		_label.Size = Size;
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

	/// <summary>一次性把关键节点打进日志（只打直接子节点）。</summary>
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
