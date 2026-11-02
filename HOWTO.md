# HOWTO — 四件事的操作步骤

命令都在项目根目录 `test_1/新建游戏项目` 执行。基线检查：`powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1`（当前 44 处，改动后不应增加）。

---

## 1. 加一个功能

以「加一把新枪」为例，其他功能按同样顺序替换文件即可。

**1-1 找入口**：先查 `MODULE_MAP.md` 确认改哪一层。数据类改动从 `data/` 进，逻辑改动从 `scripts/<层>/` 进。

**1-2 改这几处**（少一处就跑不起来）：

| 顺序 | 文件 | 改什么 |
|---|---|---|
| 1 | `data/guns/<id>.tres` | 复制 `svd.tres` 改数值（伤害/射速/弹匣/弹道/音噪/口径） |
| 2 | `assets/sprites/weapons_cdda/<id>.png` | 纯枪横向贴图（27×7 量级），否则手持空白 |
| 3 | `data/items.json` | 登记同 id 的物品，`IconPath` 指向 `items32/<id>.png` |
| 4 | `scripts/world/ZombieLoot.cs` | 加进 `Table` 权重表，否则永不掉落 |
| 5 | `docs/02` §4 | 数值表回填（AGENTS 规则 13） |

**1-3 必须补的测试**：在 `tests/GridSelfTest.cs` 加一条断言（新枪能被 `ItemDatabase` 按 id 取出、`WeaponMods` 能算弹匣容量）。

**1-4 跑**：

```powershell
D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --import   # 新增图片必须
dotnet build
D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://tests/GridSelfTest.tscn
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1
```

---

## 2. 修一个 bug

**2-1 定位（先定位到模块，别直接读文件）**：

```powershell
# 按现象搜关键词（中文注释也能搜）
grep -rn "枪焰" scripts/            # 表现类
grep -rn "WeaponMods.Active" scripts/   # 数值/改装类
grep -rn "CellPx" scripts/          # 布局类
grep -rn "GetFirstNodeInGroup" scripts/ # 空引用类（组名写错会静默失败）
```

日志/状态类问题先看 `EventBus.Instance` 的信号有没有人订阅：`grep -rn "EventBus.SignalName\.<信号名>" scripts/`。

**2-2 先写测试，再改代码**（AGENTS 规则 9）：在当前 50 项自测里加一条能被 bug 触发的断言，**先跑到它失败**，再改实现。

**2-3 只改一处**：修完 `git diff --stat` 检查是否只碰了相关文件——多出别的文件就是违反规则 10，回退。

**2-4 复跑**：`dotnet build` → 自测 → hygiene 脚本 → 手动截图确认。

---

## 3. 加一个依赖

**3-1 准入条件（三条全满足才允许）**：

1. 现有依赖确实做不到，并且能一句话说清（写进 `docs/04` 的依赖表）；
2. 许可证允许闭源商用（MIT/Apache-2.0/BSD；GPL 类需要单独确认）；
3. 有明确版本号，且不引入额外的原生库/运行时。

**3-2 加在哪**：C# 侧只走 `ZombieExtraction.csproj` 的 `PackageReference`，锁定精确版本（`Version="1.2.3"`，禁止 `*` 或浮动区间）。

**3-3 必须同时做**：
- `docs/04` 依赖表补一行（名称 / 版本 / 为什么现有做不到）；
- 跑一次 `dotnet build` + 自测，确认没有新增警告。

**3-4 禁止**：为了一件小事引入整套框架；把包塞进 `assets/` 当资源用。

---

## 4. 改不动了怎么办（允许重构的条件）

**4-1 什么情况允许重构**（满足其一）：

- 同一文件因为超 400 行已经连续 2 次阻碍改动（`check-hygiene.ps1` R1 命中且本次任务被迫继续加行）；
- 同一 bug 第 3 次复发；
- 一个方法超过 60 行且本次任务必须改它。

**4-2 重构前必须做的三件事**（顺序不能换）：

1. **补测试到当前行为**：先让现有 50 项 + 新增覆盖面跑绿，作为"改前快照"；
2. **单独开分支 + 单独提交**：`git switch -c refactor/player-split`，重构与功能改动**不同提交**；
3. **记录爆炸半径**：对照 `MODULE_MAP.md` 列出受影响的模块，逐个跑一遍。

**4-3 重构的边界**：一次只拆一个文件/一个方法，拆完立刻跑自测 + 手动过一遍关键路径（开枪、拾取、撤离、死亡）。任何"顺手"的格式化、改名、注释清理都不做——留给独立提交。

**4-4 拆 `Player.cs`（1005 行）的建议顺序**（按耦合从低到高）：

| 步 | 拆出什么 | 依赖 | 风险 |
|---|---|---|---|
| 1 | `PlayerAnimation`（`SetPlayerAnim` + 帧偏移取整） | 无 | 低 |
| 2 | `PlayerPickup`（`FindPickupTarget` / `TryPickUp`） | `WorldItems` | 中（交互半径 64、120° 弧） |
| 3 | `PlayerAim`（`GetAimDirection` / `HandleAimAndShoot`） | `Weapon` | 高（散布/后坐逻辑） |
| 4 | 保留 `Player` 做装配与状态 | 全部 | — |

---

## 5. 定期自查（可直接粘贴）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1
```

输出 0 行 = 合规。**当前基线 44 行**（R1×6 / R2×8 / R3×7 / R5×21 / R11×2），每改一次只允许持平或下降；上升必须在 PR 说明里写理由，否则视为引入新债。

快速版（不跑脚本，一行看最大文件）：

```powershell
Get-ChildItem scripts -Recurse -Filter *.cs | ForEach-Object { [pscustomobject]@{n=(Get-Content $_.FullName).Count; f=$_.Name} } | Sort-Object n -Descending | Select-Object -First 5
```
