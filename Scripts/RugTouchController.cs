using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Runs;

namespace RobMerchant;

/// <summary>
/// 商店房间里的「碰一下地毯」按钮（方形边框，原版暗棕+金色配色）。
///
/// 组成：深棕半透明方板（圆角 10、金色内描边、投影）+ 内嵌地毯小图标 + 奶油色文字。
/// 字体从场景里已有的 MegaLabel 复制，避免 Godot 默认字体。
///
/// - 挂在 NMerchantRoom 节点下（不是购买界面）
/// - 每次点击先播一句商人警告台词（原版气泡，纯文本）
/// - 台词播完后，再点一次弹出确认界面
/// - 计数值只存在这个节点里：离开商店/房间销毁时自动消失，不写存档
/// </summary>
public partial class RugTouchController : Control
{
	private static readonly Vector2 PlateSize = new(300, 64);

	private static readonly Color PlateColor = new(0.165f, 0.118f, 0.090f, 0.88f);
	private static readonly Color PlateColorHover = new(0.24f, 0.17f, 0.12f, 0.95f);
	private static readonly Color BorderColor = new(0.79f, 0.64f, 0.15f, 0.65f);
	private static readonly Color BorderColorHover = new(0.79f, 0.64f, 0.15f, 1f);
	private static readonly Vector2 IconSize = new(48, 36);

	private static bool _dumpedTree;

	private readonly Panel _plate = new();
	private readonly TextureRect _icon = new();
	private readonly Label _label = new();

	private int _touches;
	private NSpeechBubbleVfx? _lastBubble;
	private bool _placed;

	public RugTouchController()
	{
		Name = "RobMerchantRugTouch";
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = PlateSize;
		Size = PlateSize;
		TooltipText = RobConfig.TouchButtonText;

		_plate.Name = "Plate";
		_plate.MouseFilter = MouseFilterEnum.Ignore;
		_plate.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_plate.AddThemeStyleboxOverride("panel", BuildStyle(PlateColor, BorderColor));
		AddChild(_plate);

		_icon.Name = "Icon";
		_icon.MouseFilter = MouseFilterEnum.Ignore;
		_icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_icon.Position = new Vector2(14, (PlateSize.Y - IconSize.Y) * 0.5f);
		_icon.Size = IconSize;
		AddChild(_icon);

		_label.Name = "Label";
		_label.Text = RobConfig.TouchButtonText;
		_label.MouseFilter = MouseFilterEnum.Ignore;
		_label.HorizontalAlignment = HorizontalAlignment.Center;
		_label.VerticalAlignment = VerticalAlignment.Center;
		_label.Position = new Vector2(14 + IconSize.X + 6, 0);
		_label.Size = new Vector2(PlateSize.X - (14 + IconSize.X + 6) - 16, PlateSize.Y);
		_label.AddThemeFontSizeOverride("font_size", 25);
		_label.AddThemeColorOverride("font_color", StsColors.cream);
		_label.AddThemeColorOverride("font_outline_color", Colors.Black);
		_label.AddThemeConstantOverride("outline_size", 6);
		AddChild(_label);

		ApplyIcon();

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
			BorderColor = border,
			ShadowColor = new Color(0f, 0f, 0f, 0.35f),
			ShadowSize = 4,
			ShadowOffset = new Vector2(0f, 3f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(10);
		return style;
	}

	/// <summary>从内嵌 base64 解出地毯图标，不需要 pck、不需要外部文件。</summary>
	private void ApplyIcon()
	{
		try
		{
			byte[] bytes = Convert.FromBase64String(RugIconData.Base64);
			var image = new Image();
			Error error = image.LoadPngFromBuffer(bytes);
			if (error != Error.Ok)
			{
				Log.Warn("[RobMerchant] 地毯图标解码失败: " + error);
				return;
			}

			_icon.Texture = ImageTexture.CreateFromImage(image);
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 地毯图标加载异常: " + e.Message);
		}
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
		Scale = new Vector2(1.04f, 1.04f);
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

		// 商店场景里没有名字带 rug 的节点（日志已确认），所以直接用商人按钮定位：
		// 商人脚下、向左偏 = 地毯上。
		Vector2 target;
		Control? merchantButton = room.MerchantButton;
		if (merchantButton != null && merchantButton.Size != Vector2.Zero)
		{
			Rect2 rect = merchantButton.GetGlobalRect();
			target = new Vector2(
				rect.Position.X + rect.Size.X * 0.15f,
				rect.End.Y + rect.Size.Y * 0.24f);
			Log.Info($"[RobMerchant] 按商人按钮定位 rect={rect} -> target={target}");
		}
		else
		{
			Rect2 roomRect = room.GetGlobalRect();
			target = new Vector2(roomRect.GetCenter().X, roomRect.Size.Y * 0.72f);
			Log.Info("[RobMerchant] 使用房间默认位置");
		}

		PivotOffset = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
		GlobalPosition = target - new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
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
		NMerchantRoom? room = NMerchantRoom.Instance;
		if (room == null)
		{
			Log.Warn("[RobMerchant] 确认抢劫时找不到商店房间");
			return;
		}

		IRunState? runState = room.Room.GetLocalInventory().Player.RunState;
		if (runState == null || RobSession.IsRobbed(room.Room) || RobRunData.HasRobbed(runState))
		{
			Log.Warn("[RobMerchant] 本商店已处理过，忽略确认");
			return;
		}

		Log.Info("[RobMerchant] 确认抢劫，准备进入商人战斗");

		// 等确认弹窗自己关掉之后再开战，避免弹窗与战斗转场打架
		Callable.From(() => ShopRobCombat.Start(room.Room)).CallDeferred();
	}

	private void OnCancelled()
	{
		Log.Info("[RobMerchant] 取消抢劫");
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
