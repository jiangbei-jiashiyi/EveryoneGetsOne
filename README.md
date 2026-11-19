# 雨中冒险2 全员拾取（EveryoneGetsOne）v1.1.1 — 联机掉落共享 Mod

> 每个掉落每人一份 · 统率神器每人各选一次 · 已领取物品本地隐藏并过滤雷达

雨中冒险 2 联机掉落共享 Mod：同一个掉落物，每名玩家都能领取一次；开启统率神器时，每名玩家都能从同一个统率精魄里各选一次自己的物品。

已领取/已选择过的掉落物只在领取者自己的客户端隐藏，不再被雷达扫描装置扫到，也不会再出现标记图标；尚未领取的玩家仍然正常看得到。

**下载安装包**：[GitHub Releases](https://github.com/jiangbei-jiashiyi/EveryoneGetsOne/releases)

## 功能

- **普通物品掉落**：箱子、Boss、献祭、商店购买等产生的物品掉落，每名玩家都可以走过去领取一次；所有当前玩家都领取后，掉落物才消失。
- **统率神器（统率精魄）**：同一个精魄会保留到所有当前玩家都选完；每名玩家各选各的，选完的人不会再看到这个精魄。
- **本地隐藏**：玩家领取后，该掉落物只在这个玩家的客户端隐藏（模型、碰撞体、光效、音效），其他玩家不受影响。
- **雷达过滤**：雷达扫描装置（ChestScanner）不会再扫描到该玩家已领取的掉落物；已有的雷达标记、Ping 标记也会被移除。
- **死亡保护**：永久阵亡的玩家默认自动获得一份掉落，避免卡住掉落物（可关闭）。
- **聊天进度**（可选）：显示当前领取进度，默认关闭，避免和游戏自带的拾取提示刷屏。

## 安装

1. 先安装 **BepInEx 5.4.x x64**（BepInExPack）。如果游戏根目录没有 `BepInEx` 文件夹，把 BepInExPack 解压到 `Risk of Rain 2.exe` 所在目录，启动一次游戏。
2. 把 `EveryoneGetsOne.dll` 放到：
   ```
   Risk of Rain 2\BepInEx\plugins\EveryoneGetsOne\EveryoneGetsOne.dll
   ```
3. 启动游戏。日志里出现 `EveryoneGetsOne v1.1.1 loaded.` 和 `Harmony patches applied to 11 method(s).` 即安装成功。

### 联机注意

- **主机必须安装**，否则没有掉落共享逻辑。
- **建议所有玩家都安装同一个版本**。只有安装了 Mod 的客户端才会收到“隐藏已领取掉落物”的本地消息；原版客户端仍能正常领取物品，只是不会隐藏自己已领取的掉落物，统率精魄的交互提示也可能重复出现（服务器会阻止重复领取）。
- 不同版本之间不会崩溃，但隐藏效果以双方都安装为准。

## 配置

首次启动后生成：

```
BepInEx\config\com.liaojinghua.ror2.everyonegetsone.cfg
```

| 配置项 | 默认 | 说明 |
|---|---|---|
| `General.Enable` | `true` | 总开关，关闭后完全恢复原版。 |
| `General.ShareItems` | `true` | 普通物品掉落每人一份。 |
| `General.ShareCommandPicks` | `true` | 统率神器每人各选一次。 |
| `General.ShareToDead` | `true` | 永久阵亡玩家自动获得一份掉落。 |
| `General.AnnounceProgress` | `false` | 聊天栏显示领取进度。 |
| `Visibility.HideClaimedPickups` | `true` | 已领取的掉落物在本地隐藏，并从雷达/标记中移除。 |
| `Tiers.ShareLunarItems` | `true` | 是否共享月球（蓝色）物品。 |
| `Tiers.ShareVoidItems` | `true` | 是否共享虚空物品。 |
| `Tiers.ShareBossItems` | `true` | 是否共享 Boss（黄色）物品。 |
| `General.VerboseLog` | `false` | 输出调试日志。 |

配置在游戏启动时读取；修改后需要重启游戏（或重新进出一次主菜单，视 BepInEx 版本而定）。

## 不共享的内容

- **装备**：每人只有一个装备槽，装备掉落仍是原版先到先得；统率精魄里的装备选项也保持原版。
- **3D 打印机、拆解机、大锅、清洗池**：这些是消耗/转换物品的交互，不是掉落物，保持原版。
- **金钱、血量、弹药**：不是物品。

## 已知限制

- 未安装 Mod 的客户端看不到“已领取隐藏”效果；服务器仍会正确阻止重复领取。
- 玩家领取后退出再重连，本地隐藏状态会丢失；服务器按旧角色记录领取，重连后的新角色可能被当成未领取（属于联机重连的边缘情况）。
- 分屏本地双人时，隐藏状态按客户端而不是按本地玩家区分。
- 不要和 ItemShare、ShareSuite 等同样接管 `GenericPickupController.AttemptGrant` 的 Mod 同时使用，会冲突。

## 从源码构建

需要 .NET SDK 8 和已安装 BepInEx 的游戏目录。

```powershell
dotnet build .\EveryoneGetsOne.csproj -c Release
```

项目默认游戏目录为 `F:\SteamLibrary\steamapps\common\Risk of Rain 2`，可在命令后加：

```powershell
dotnet build .\EveryoneGetsOne.csproj -c Release -p:GameDir="你的游戏目录"
```

输出：`bin\Release\EveryoneGetsOne.dll`。

## 更新日志

### 1.1.1

- 修复部分角色/特殊 body 被误判为永久阵亡，导致共享玩家列表为空、掉落回退原版的问题。
- 玩家列表改为 `PlayerCharacterMasterController` + `NetworkUser` + `CharacterMaster` 三路兜底并去重。
- 修复主机领取后本地隐藏不执行的问题（不再依赖 `connectionToClient`）。
- 可见性代码加异常保护，不再影响物品发放。
- 新增 `[Diag]` 阶段日志和 `Claim` / `Command pick` 领取日志。
### 1.1.0

- 新增：已领取/已选择的掉落物在领取者客户端本地隐藏。
- 新增：雷达扫描装置不再扫描已领取的掉落物。
- 新增：移除已领取掉落物身上的雷达标记和 Ping 标记。
- 新增：客户端握手，只有安装了本 Mod 的客户端才会收到隐藏消息，原版客户端不会收到未知消息类型。
- 新增：`Visibility.HideClaimedPickups` 配置项。

### 1.0.0

- 普通物品掉落每人一份。
- 统率神器每人各选一次。
- 死亡玩家自动领取、聊天进度等配置。
