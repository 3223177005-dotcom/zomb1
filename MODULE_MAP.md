# MODULE_MAP — 改代码前先查爆炸半径

用法：找到你要改的模块 → 看「改它会影响什么」和「易错点」→ 再动手。
基线（2026-11，`tools/check-hygiene.ps1`）：**44 处违规**（R1×6、R2×8、R3×7、R5×21、R11×2），数字与本文一致。

| 模块 | 一句话职责 | 依赖谁 | 谁依赖它 | 改它会影响什么 | 易错点 |
|---|---|---|---|---|---|
| `scripts/autoload/` | 4 个常驻单例：`EventBus`(信号总线) / `ItemDatabase`(物品表) / `NavigationManager`(寻路网格) / `SceneManager`(场景跳转) | `data/items.json` | 全项目 | **全局**：改任何单例接口 = 全仓库编译风险 | `GameManager`/`SaveManager`/`AudioManager` 是空壳（0 引用），别误以为有存档/音频；`Player._ExitTree` 必须退订 EventBus，否则下一局回调已释放对象 |
| `scripts/item/` | 背包与物品：`Inventory`/`GridInventory`(格子占位)、`ItemData`/`ItemStack`、`WeaponMods`+`AttachmentFinder`(改装数值) | `data/*.json`、Godot 基础类型 | `ui`、`weapon`、`Player`、`world` | 拖拽、堆叠、拆分、改装面板、掉落显示全部受影响 | **`WeaponMods.Active` 是全局可变状态**：由 `Player.ApplyActiveWeapon` 写入，`Weapon` 读取（`Weapon.cs:159/190/212`）。不先调 `ApplyActiveWeapon` 就开火 → 用的是上一把枪的配件；`ItemData.cs:59` 单方法 151 行（表头解析），动字段要同步 `items.json` |
| `scripts/weapon/` | 开火/换弹/散布（`Weapon`）、近战（`MeleeWeapon`）、纯表现（`WeaponVisual`：360° 旋转 + 枪焰） | `item`(配件数值)、`autoload.EventBus`、**`Player`（反向依赖）** | `Player`、`ui` | 所有枪械手感、枪焰位置、CDDA/旧贴图两条渲染路径 | `Weapon.cs:178 ResolvePlayer()` 用 `GetFirstNodeInGroup("player")` 字符串查找；`WeaponVisual` **三处**写 `_flash.Position`（CDDA 分支、360 旧分支、方向分支）——改枪焰要三处一起改，漏一处就是「某个角度不跟随」 |
| `scripts/enemy/` | 丧尸 AI：感知(视野/听觉)、A* 追击、攻击、受击、尸体与掉落 | `world.NavigationManager`、**`Player`（反向依赖）**、`ZombieLoot` | `Game`、`world` | 战斗难度、掉落来源、寻路 | 427 行；`Enemy.cs:44/205` 两次用 group 字符串取玩家；`Enemy.SetAnim` 的帧偏移公式与 `Player.SetPlayerAnim` 重复（`Player.cs:842` / `Enemy.cs:97`），改一处不改另一处会导致体型错位 |
| `scripts/world/` | 地图与地面：`MapLayout`(硬编码 48×32 布局)、`MapGrid`(格坐标)、`WorldItems`(地面物品/搜刮点)、`ExtractionZone`(5 秒撤离)、`GroundItemsLayer`(格子高亮) | `item`、**`Game`/`Player`（反向依赖）** | `Game`、`Player`、`enemy` | 地图、撤离点、地面掉落、搜刮容器 | `MapLayout.Build()` 是**硬编码**，改地图要同时改 `SpawnChests` 的固定格坐标与撤离点 `(23,29)`；`GroundItemsLayer` 用 `"game"`/`"player"` 组名取节点，改名即静默失效 |
| `scripts/ui/` | 全部面板：装备/背包/搜刮(`EquipmentPanel` 798 行)、改装(`ModifyPanel`)、HUD(`Hud`)、结算(`ResultPanel`)、拖拽(`ItemDrag`)、行/块(`ItemRow`/`GridItemBlock`) | `item`、`weapon` | `Game`、`Player` | 所有界面布局与交互 | `CellPx = 40` 在 `EquipmentPanel.cs:445/713` 各写一份（外加 `GridPanel.cs:53` 默认值）；`new StyleBoxFlat` 有 **9 处**手搓样式；面板锚点魔数 `0.66/0.97`。改格子尺寸要三处一起改 |
| `scripts/Game.cs` | 场景总装：建地图、放箱子、撤离点、结算面板、调试掉落 | 几乎所有层 | `GroundItemsLayer`(反向)、`ui` | 整局初始化顺序 | 391 行；`_Ready` 里初始化顺序敏感（先 `MapLayout`→`SpawnChests`→`SetupWorldZoom`→`BuildExtraction`）；调试掉落坐标已 ×2（世界格 32） |
| `scripts/Player.cs` | 玩家：移动、瞄准、射击、拾取、动画、死亡 | 全部下层 | `weapon`/`enemy`/`world`（反向依赖） | **一切** | **1005 行 / 6 件事**，是全仓最大风险点；`ApplyActiveWeapon()` 有 3 个调用点（`:192`/`:686`/`:767`），顺序契约：先有 `_weaponVisual` 才能挂枪；`BuildSprite()` 与 `GetNode<Weapon>` 挤在同一行（`:121`） |
| `data/` | 物品表（14 件）、枪、近战、附件、容器、敌人数值 | — | `ItemDatabase`、`Game` | 数值与掉落 | 物品 `Id` 必须与 `assets/sprites/items32/<id>.png`、`weapons_cdda/<id>.png` 同名；改数值必须回填 `docs/02` §4 |
| `tests/` | `GridSelfTest`：50 项，**只覆盖背包/改装/掉落** | `item` | CI/人工 | 无 | 战斗、撤离、死亡、UI 拖拽**无测试**；修这些模块的 bug 必须新写测试 |
| `tools/` | 美术管线（切图/地形重排/去手/字体检查） | `_pkg/`（**不在仓库里**） | 人工 | 重新生成图集与图标 | 脚本默认读 `_pkg/msx`、`_pkg/chesthole16`，**clone 后跑不了**；重跑前先按 `docs/03` §6 把素材下载到 `_pkg/` |

## 三条速查

1. **要改 `Player.cs` 的任何一块**：先确认它属于 6 件事里的哪一件（移动/瞄准/射击/拾取/动画/死亡），只动那一件，其余保持不动。
2. **要改贴图/尺寸**：先看 `docs/03` 的尺寸表，再改 `CellPx`（3 处）或 2× 缩放（5 处）。
3. **要改数值**：改 `data/*.tres|json`，然后回填 `docs/02`，不要改代码里的常量。
