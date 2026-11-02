# AGENTS.md — 改这个仓库前必读

唯一目标：**半年后仍改得动**。验收标准只有一条：新人（或 AI）30 分钟内能安全改掉一个需求而不弄坏别的。
本文件只写"防止腐烂"的规则，不写风格。每条都必须能检查。

| # | 规则 | 针对的问题 | 怎么检查 | 违反时怎么办 |
|---|---|---|---|---|
| 1 | 单文件 **≤ 400 行**（现状超限：`Player.cs` 1005、`EquipmentPanel.cs` 798） | 一个文件承担移动/瞄准/拾取/UI 多职责 | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1` | 先按职责拆，再动功能；不允许"先加后拆" |
| 2 | 单方法 **≤ 60 行**，参数 **≤ 5 个** | 布尔参数堆叠、方法内分叉过多 | 同上脚本 | 拆私有方法，或把参数收进结构体 |
| 3 | 依赖方向单向：`autoload ← world/item/weapon/enemy ← ui ← Game/Player`，**下层禁止引用上层** | 下层被迫知道 UI/玩家细节 | 现状 **7 处**（`Weapon.cs:176/178/181`、`Enemy.cs:44/205`、`GroundItemsLayer.cs:35/40`），**禁止新增**：`check-hygiene.ps1` 的 R3 计数不得上升 | 用接口或事件回调反向注册，别直接 `GetNode` |
| 4 | 禁止新增**全局可变状态**；已有 5 处冻结：`EventBus/ItemDatabase/NavigationManager/SceneManager.Instance` + `WeaponMods.Active` | 隐式契约："不先设 Active 就不生效" | `grep -rn "public static .*{ get; set; }" scripts/` | 走显式参数或事件；确需新增要先在本表补一行并说明理由 |
| 5 | 禁止用**字符串 group 名**跨层取节点（现存 `GroundItemsLayer` 的 `"player"`/`"game"` 是遗留） | 改名后静默变 null，运行时才炸 | 现状 **21 处**（冻结）：`check-hygiene.ps1` 的 R5 计数不得上升 | 新代码用构造注入或导出引用；旧代码改动时顺带注入 |
| 6 | **禁止吞异常**；catch 必须带上下文，错误信息格式：做了什么 + 输入 + 期望 | 出错无声，半年后无法定位 | `grep -rn "catch\s*{" --include=*.cs scripts/` | 要么不捕获，要么包装后重抛 |
| 7 | 16px 素材进 32px 世界**必须显式 ×2**，且只允许在 4 个位置（角色/敌人/武器/物件） | 漏缩放 → 贴图半格大 | `grep -rn "Scale = new Vector2(2" scripts/` 应为 5 行以内 | 新增世界贴图先查 `docs/03` §3 |
| 8 | 新增**第三方依赖**前，必须在 `docs/04` 的依赖表补一行：为什么现有依赖做不到、版本号 | 依赖膨胀、版本漂移 | 看 `docs/04` diff 是否含该行 | 未写说明的 PR 直接退回 |
| 9 | 修 bug **必须附一条能复现的测试**（`tests/GridSelfTest.cs` 或新测试场景） | 同一个 bug 修三次 | `git show --stat` 里修复与测试是否同提交 | 没有测试的修复不算完成 |
| 10 | **禁止越界改动**：顺手重构 / 顺带格式化 / 改无关文案 | 一次改动变成一次事故，review 失效 | `git diff --stat` 是否只碰任务相关文件 | 越界内容先说明并等确认，或拆成单独提交 |
| 11 | 魔法数字集中：`CellPx=40`、字号 `22`、逻辑分辨率 `960×540`、撤离 `5s`、世界格 `32` | 同一个 40 在 3 个面板各写一份 | `grep -rn "CellPx = 40\|= 22;" scripts/` | 提到 `Config`/常量文件，改一处生效 |
| 12 | 源码里禁止出现字面量 `\n`（PowerShell 替换事故高发） | 整行代码被注释掉，编译还过 | `grep -rn '\\\\n' --include=*.cs scripts/` | 用编辑器或 `edit` 工具改，不用 `-replace` 拼多行 |
| 13 | 改数据（`data/*.json`、`*.tres`）必须回填 `docs/02` 的对应表格 | 文档与数值分叉 | 对照 `docs/02` §4 数值表 | 同一次提交里改文档 |

## 定期自查（可直接粘贴）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-hygiene.ps1
```

脚本做四件事：超大文件、方法过长、跨层引用、禁词（`catch {}` / 字符串 group / 字面 `\n` / 新增 static setter）。
**当前基线 44 行**（R1×6 / R2×8 / R3×7 / R5×21 / R11×2）。只允许持平或下降；上升必须在提交说明里写理由。

## 违反时的总则

1. **先停手**：不要"顺手修好"，那属于规则 10；
2. 在本文件表格里补一条"现状例外"（谁、为什么、何时还）；
3. 例外超过 2 个 → 说明规则定错了，改规则而不是攒例外。
