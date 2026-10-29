# 抢劫商人（Rob Merchant）

《杀戮尖塔 2》的单人模式 Mod：在商店里抢劫商人，打一架之后回到原商店，商品全部免费。

## 版本

- 游戏：v0.111.0（正式版）
- 依赖：RitsuLib（创意工坊 `STS2-RitsuLib`，兼容版 0.111.0）
- Mod 版本：0.1.0

## 功能

1. 进入商店后，左下角出现 **「抢劫商人」** 按钮
2. 点击 → 确认弹窗 → 选择「动手」进入商人战斗（原版假商人遭遇战）
3. 战斗胜利后点「继续」→ **回到这家商店**
4. 回来后：
   - 卡牌、遗物、药水、删卡服务的价格全部变成 **0**
   - 买走的下架，没买的保留
   - 不再出现「抢劫商人」按钮（不能重复抢）
5. 本局之后进入的任何商店：**商人不再出现**，只能离开
6. 新开一局后一切恢复

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
- 「本局已抢劫」用 RitsuLib `RunSavedData<T>` 持久化，新局自动重置

## 已知限制

1. **读档会丢失“同一批商品”**：商店库存本来就不进原版存档（`AbstractRoom.FromSerializable`
   只支持战斗房和事件房），读档后商店会重新生成。
2. **战斗中途存档退出**：游戏在胜利瞬间会自动存一次“已结束的战斗房”，
   如果此时退出再进，会直接进入那个战斗房领奖励，**不会回到商店**。
3. 读档后如果人还在被抢过的那家商店里，商人会按“本局已抢劫”处理（隐藏），不再给免费购物。
4. 单人模式设计，未测试联机。
5. 敌人目前是原版假商人（名字/血量/意图的自定义方案见 `自定义商人敌人方案.md`，暂缓）。

## 目录说明

- `Scripts/Entry.cs`：Mod 入口
- `Scripts/ShopRobButtonPatch.cs`：商店按钮 + 后续商店隐藏商人
- `Scripts/NRobConfirmDialog.cs`：确认弹窗（代码构建，不依赖游戏场景）
- `Scripts/ShopRobCombat.cs`：发起商人战斗
- `Scripts/MerchantRoomResumePatch.cs`：补写 `MerchantRoom.Resume`，战斗后回商店
- `Scripts/ShopZeroPricePatch.cs`：价格归零 + 禁用补货
- `Scripts/RobRunData.cs`：本局持久化标记（RitsuLib）
- `Scripts/RobSession.cs`：本局内的临时状态（内存）

## 构建

```powershell
dotnet build -c Release -p:ModsOutDir="<你想输出到的目录>"
```

默认输出到 `...\Slay the Spire 2\mods\RobMerchant\`。
