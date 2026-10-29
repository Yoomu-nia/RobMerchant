# 抢劫商人（Rob Merchant）

《杀戮尖塔 2》Mod：在商店里触碰商人的地毯挑衅他，打一架之后回到这家商店，商品全部免费。全程AI许愿式编程，没有一点我的代码。

> **Rob the Merchant** — a mod for *Slay the Spire 2* (single player & co-op).
> A “touch the rug” button appears in every shop. Poke it a few times, the merchant warns you,
> then you can rob him. Win the fight, come back to the same shop with every price set to 0.
> After robbing once, all later shops in the run have no merchant. Resets on a new run.
> Game v0.111.0 · requires RitsuLib.

## 版本

- 游戏：v0.111.0（正式版）
- 依赖：RitsuLib（创意工坊 `STS2-RitsuLib`，兼容版 0.111.0）
- Mod 版本：0.2.0

## 功能

1. 进入商店后，商人脚下的地毯上出现 **「碰一下地毯」** 按钮（暗棕底板 + 金色描边 + 地毯图标）
2. 每点一次，商人头顶冒出一句警告台词（共 4 句，台词可在 `Scripts/RobConfig.cs` 里改）
3. 第 5 次点击 → 弹出**原版样式**确认框（确认 / 取消）
4. 取消后可以继续正常购物；再次点击会直接重新弹出确认框（计数保留）
5. 确认 → 进入商人战斗（原版假商人遭遇战）
6. 战斗胜利后点「继续」→ **回到这家商店**
7. 回来后：
   - 卡牌、遗物、药水、删卡服务的价格全部变成 **0**
   - 买走的下架，没买的保留
   - 不再出现「碰一下地毯」按钮（不能重复抢）
8. 本局之后进入的任何商店：**商人不再出现**，只能离开
9. 新开一局后一切恢复

## 联机

- 任意玩家都可以发起抢劫
- 一个人确认后，**所有玩家一起进入战斗**（服务端动作队列广播，各端本地执行）
- 打赢后**所有人的商店都是 0 元**
- 后续商店对所有人都不再出现商人
- 死亡按原版规则处理

## 安装

1. 安装 RitsuLib（创意工坊）
2. 把 `RobMerchant` 文件夹放进：
   `...\Steam\steamapps\common\Slay the Spire 2\mods\`
   （里面应有 `RobMerchant.dll` 和 `RobMerchant.json`）
3. 启动游戏，主菜单模组列表里能看到「抢劫商人」

## 实现要点（给后来维护的人）

- 战斗用原版 `FakeMerchantEventEncounter`
- 战斗房是用 `RunManager.EnterRoomWithoutExitingCurrentRoom` **压**在商店上面的，
  所以商店实例（`MerchantRoom.Inventories`）留在房间栈底，货架不会重新生成
- 原版 `MerchantRoom.Resume` 只有 `throw new NotImplementedException()`，
  本 Mod 用 Harmony Prefix 补上了实现（重建 `NMerchantRoom` 界面）
- 价格归零走 `Hook.ModifyMerchantPrice` Postfix（UI 每次实时读 `MerchantEntry.Cost`）
- 抢劫商店内禁用 `TheCourier` 的补货（`Hook.ShouldRefillMerchantEntry`），否则会无限刷免费货
- 「本局已抢劫」用 RitsuLib `RunSavedData<T>` 持久化（主机写盘），内存里另有一份镜像给客户端立即生效
- 联机用 RitsuLib `ManagedNetAction`（`GameActionType.NonCombat`）广播“抢劫意图”，
  各端校验坐标后各自进入战斗 —— 与游戏原版事件战斗的同步模型一致
- 台词气泡用原版 `NSpeechBubbleVfx.Create(string, ...)`；确认框用原版 `NVerticalPopup` + `NModalContainer`
- 按钮图标以 base64 内嵌在 `RugIconData.cs`，不需要 pck（`tools/make_rug_icon.ps1` 可重新生成）

## 已知限制

1. **SL（读档）之后，商人卖的东西会变**：原版商店库存本来就不进存档
   （`AbstractRoom.FromSerializable` 只支持战斗房和事件房），
   所以读档重进免费商店时货架会**随机重新生成**——商品种类和数量都会变，
   但价格仍然是 0。
2. 因为存档点就在免费商店里，**每次读档都会重新刷出一批免费商品**（可反复刷）。
3. **战斗胜利前存档退出**：此时最后存档还是“进入商店前”，读档后商人在场，需要重新抢一次。
4. 敌人目前是原版假商人（名字/血量/意图的自定义方案见 `docs/自定义商人敌人方案.md`，暂缓）。

## 目录说明

- `Scripts/Entry.cs`：Mod 入口（注册补丁 / 单局数据 / 网络动作）
- `Scripts/RobConfig.cs`：**台词、阈值、弹窗文案、按钮文字都在这里**
- `Scripts/RugTouchController.cs`：商店房间里的「碰一下地毯」按钮（自绘 UI + 计数 + 气泡）
- `Scripts/RugTouchPatch.cs`：把按钮挂到 `NMerchantRoom`，跟随购买界面显隐
- `Scripts/NRobConfirmPopup.cs`：原版样式确认弹窗（`NVerticalPopup` 包装）
- `Scripts/NRobConfirmDialog.cs`：弹窗加载失败时的自绘兜底
- `Scripts/RobNetAction.cs`：联机网络动作（广播抢劫意图）
- `Scripts/ShopRobCombat.cs`：本地兜底开战路径
- `Scripts/MerchantRoomResumePatch.cs`：补写 `MerchantRoom.Resume`，战斗后回商店并在此存档
- `Scripts/ShopZeroPricePatch.cs`：价格归零 + 禁用补货
- `Scripts/ShopRobButtonPatch.cs`：本局已抢过 → 隐藏商人
- `Scripts/SkipVictorySavePatch.cs`：跳过战斗胜利时的自动存档（改在免费商店存）
- `Scripts/ReloadIntoFreeShopPatch.cs`：读档重进免费商店时跳过重复历史记录
- `Scripts/RobRunData.cs`：本局持久化标记 + 免费商店坐标（RitsuLib）
- `Scripts/RobSession.cs` / `RobFlags.cs`：本局内存状态与统一判断
- `Scripts/RugIconData.cs`：内嵌图标 base64
- `tools/make_rug_icon.ps1`：重新生成按钮图标

## 构建

前置：.NET SDK 9 或更新、游戏本体、已安装 RitsuLib（创意工坊）。

游戏目录按以下顺序解析：

1. `-p:Sts2Dir="你的游戏目录"`
2. 环境变量 `STS2_DIR`
3. 常见 Steam 安装位置（会检查 `sts2.dll` 是否存在）
4. 都没有 → 构建时给出明确报错

推荐在工程目录建一个 **不提交** 的 `Directory.Build.props`（已加入 `.gitignore`）：

```xml
<Project>
  <PropertyGroup>
    <Sts2Dir>D:\Steam\steamapps\common\Slay the Spire 2</Sts2Dir>
  </PropertyGroup>
</Project>
```

然后：

```powershell
dotnet build -c Release
```

默认会把 dll 与 json 复制到 `<游戏目录>\mods\RobMerchant\`；想输出到别处：

```powershell
dotnet build -c Release -p:ModsOutDir="D:\out"
```

RitsuLib 路径默认是
`<游戏目录>\..\..\workshop\content\2868840\3747602295\compat\0.111.0`，
如果你的工坊目录不同，用 `-p:RitsuLibDir="..."` 指定。

## 仓库结构

```
RobMerchant/
├─ Scripts/                 源码
├─ tools/                   图标生成脚本
├─ RobMerchant.csproj       工程（路径可覆盖，无硬编码）
├─ RobMerchant.json         Mod 清单
├─ project.godot            Godot 工程标识
├─ README.md
└─ LICENSE                  MIT
```
