# 余日无多（ZombieExtraction）

**搜打撤 + 刷宝构筑（BD）的像素 2D 硬核 roguelike RPG 小游戏。**
带一把枪进废城，在 100 血、50 kg、12+30 格的夹缝里搜刮、交火、改装，活着走到撤离点把东西带出来；死了这一局就白给。

- 引擎：**Godot 4.7.2（mono）**，语言 **C#**
- 平台：Windows（已导出）/ macOS（有导出说明，未实测）
- 单局时长：5–15 分钟

---

## 一、怎么跑起来

```powershell
# 1) 构建
dotnet build

# 2) 跑游戏
D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --path . res://scenes/game.tscn

# 3) 跑自测（50 项，必须全绿）
D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://tests/GridSelfTest.tscn

# 4) 新增/替换图片后必须先导入（否则贴图为空）
D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --import
```

> ⚠️ **构建失败时 Godot 仍会用旧 dll 启动**，表现为「改了没反应 / 脚本类找不到」。改完先确认 `0 错误`。

导出：`export_presets.cfg` 已有 Windows / macOS 两个预设；导出前用 `dotnet build -c ExportRelease`。

## 二、怎么玩

| 操作 | 键位 |
|---|---|
| 移动 | WASD / 方向键 |
| 冲刺 | Shift |
| 瞄准 | 鼠标（武器 360° 跟随） |
| 射击 / 近战 | 左键 |
| 交互 / 拾取 | F |
| 换弹 | R |
| 切换武器槽 | 1 / 2 |
| 开背包 | Tab |

一局流程：**搜**（尸体、屋内容器）→ **打**（丧尸）→ **撤**（地图下方 (23,29) 的 2×2 撤离点站满 5 秒）→ **结算**。
离开撤离区计时清零；人物血量归零则 3 秒后回主菜单，本局物品不保留。

完整玩家手册见 [`玩法介绍.md`](玩法介绍.md)。

## 三、代码在哪

```
scripts/            # 全部 C# 逻辑
├── Game.cs         # 场景总装：地图、撤离点、箱子、调试掉落
├── Player.cs       # 移动 / 瞄准 / 射击 / 拾取 / 死亡
├── autoload/       # EventBus、ItemDatabase、NavigationManager、SceneManager（在用）
│                   # GameManager、SaveManager、AudioManager（已建未接入）
├── enemy/          # 丧尸 AI、感知、尸体掉落
├── item/           # 背包（格子）、物品、附件、改装数值
├── ui/             # 装备/背包/改装/搜刮/结算 面板
├── weapon/         # 枪械、近战、武器外观（含 360° 旋转）
└── world/          # 地图布局、地面物品、搜刮点、撤离区
data/               # 物品/枪/近战/附件/容器/敌人（.json / .tres）
assets/sprites/     # 32px 图标、地形图集、角色/丧尸帧、CDDA 枪贴图
tests/              # GridSelfTest（50 项）
tools/              # 美术管线：切图、地形重排、去手、放大
docs/               # 项目文档集（策划 + 技术）
```

## 四、文档

| 文档 | 用途 |
|---|---|
| [docs/00-索引与术语.md](docs/00-索引与术语.md) | **先读这个**：文档清单、阅读顺序、术语表（搜打撤 / 生存 / BD / 硬核 roguelike / 像素 2D） |
| [docs/01-项目定位与卖点.md](docs/01-项目定位与卖点.md) | 做给谁、凭什么有人玩、明确不做什么 |
| [docs/02-核心玩法循环.md](docs/02-核心玩法循环.md) | 一局怎么开始/结束、数值、掉落权重、死亡规则 |
| [docs/03-美术与像素规格.md](docs/03-美术与像素规格.md) | 32px 世界格、图标/UI 尺寸、地形自动拼接、授权 |
| [docs/04-系统与代码结构.md](docs/04-系统与代码结构.md) | 代码分层、加物品/加枪的步骤、常量表、技术债 |
| [docs/05-开发路线与里程碑.md](docs/05-开发路线与里程碑.md) | 做到哪、下一步做什么 |
| [docs/06-已知限制与待决问题.md](docs/06-已知限制与待决问题.md) | 现状缺口、Q1–Q6 决策清单、待补充数据 |
| [docs/07-文档比对与合并建议.md](docs/07-文档比对与合并建议.md) | 旧文档的合并/更新/废弃处置 |
| [玩法介绍.md](玩法介绍.md) | 玩家向操作手册 |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | 第三方素材来源与 **CC BY-SA 3.0** 义务（发版必带） |

## 五、当前状态

| 项 | 状态 |
|---|---|
| 搜打撤闭环 | ✅ 搜刮 / 交战 / 撤离 / 结算 / 死亡 |
| 背包与改装 | ✅ 格子背包、附件、拖拽、拆分 |
| 像素规格 | ✅ 32px 世界格、32px 图标、40px UI 格 |
| 武器表现 | ✅ 360° 旋转 + CDDA 枪贴图（可回退三方向） |
| 自测 | ✅ 50 项全过，`dotnet build` 0 警告 0 错误 |
| 内容量 | 🟡 14 件物品 / 1 种敌人 / 1 张地图 |
| 存档与元进度 | ❌ 未做（`SaveManager` 未接入） |

## 六、授权

第三方素材（MSX++UnDeadPeopleEdition 的物品图标、地形、枪械贴图）为 **CC BY-SA 3.0**：
分发时必须保留 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) 与许可全文，且修改过的素材继续按同协议分发。
本项目自有代码与美术可单独授权。
