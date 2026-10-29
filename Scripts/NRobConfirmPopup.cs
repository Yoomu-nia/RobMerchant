using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace RobMerchant;

/// <summary>
/// 抢劫确认弹窗：包装原版 <see cref="NVerticalPopup"/>，放进 <see cref="NModalContainer"/>。
///
/// 为什么这么绕：
/// - NVerticalPopup.Close() 内部是 NModalContainer.Instance.Clear()，
///   所以它必须是模态容器里的子节点，点按钮才会正确释放自己并复位 OpenModal。
/// - NModalContainer.Add 要求节点实现 IScreenContext，所以需要这一层包装
///   （原版 NAbanonRunConfirmPopup 也是这个结构）。
///
/// 如果 ui/vertical_popup 场景加载失败，回退到代码构建的 NRobConfirmDialog。
/// </summary>
public partial class NRobConfirmPopup : Control, IScreenContext
{
	private NVerticalPopup? _popup;
	private Action? _onConfirm;
	private Action? _onCancel;

	public Control? DefaultFocusedControl => null;

	public static NRobConfirmPopup? Create(Action onConfirm, Action onCancel)
	{
		string path = SceneHelper.GetScenePath("ui/vertical_popup");
		PackedScene? scene = ResourceLoader.Load<PackedScene>(path);
		if (scene == null)
		{
			Log.Warn("[RobMerchant] 加载 " + path + " 失败，确认弹窗会回退到自绘版本");
			return null;
		}

		NVerticalPopup? popup;
		try
		{
			popup = scene.Instantiate<NVerticalPopup>();
		}
		catch (Exception e)
		{
			Log.Warn("[RobMerchant] 实例化 NVerticalPopup 失败: " + e.Message);
			return null;
		}

		if (popup == null)
		{
			return null;
		}

		var wrapper = new NRobConfirmPopup
		{
			Name = "RobMerchantConfirmPopup",
			_onConfirm = onConfirm,
			_onCancel = onCancel,
			_popup = popup
		};

		wrapper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		wrapper.MouseFilter = MouseFilterEnum.Stop;
		wrapper.AddChild(popup);
		return wrapper;
	}

	public override void _Ready()
	{
		if (_popup == null)
		{
			return;
		}

		_popup.SetText(RobConfig.ConfirmTitle, RobConfig.ConfirmBody);
		_popup.InitYesButton(
			new LocString("main_menu_ui", "GENERIC_POPUP.confirm"),
			_ => _onConfirm?.Invoke());
		_popup.InitNoButton(
			new LocString("main_menu_ui", "GENERIC_POPUP.cancel"),
			_ => _onCancel?.Invoke());
	}

	public override void _ExitTree()
	{
		if (_popup != null)
		{
			_popup.DisconnectSignals();
			_popup.DisconnectHotkeys();
		}
	}
}
