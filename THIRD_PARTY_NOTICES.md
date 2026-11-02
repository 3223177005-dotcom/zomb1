# 第三方素材与授权（THIRD-PARTY NOTICES）

本项目《余日无多》使用了以下**第三方美术素材**。这些素材的版权归原作者所有，
本项目只按各自的许可协议使用；**修改后的衍生图继续沿用同一许可**。

---

## 1. 物品图标（32×32）：MSX++UnDeadPeopleEdition

- **文件**：`assets/sprites/items32/*.png`（14 张：bandage / svd / dagger / backpack / cash / circuit /
  gold / intel / watch / sample / silencer / extmag / tacgrip / ammo_762x54r）
- **来源**：Cataclysm: Bright Nights 仓库
  `gfx/MSX++UnDeadPeopleEdition/`（`normal_items.png` + `tile_config.json`）
  <https://github.com/cataclysmbn/Cataclysm-BN/tree/main/gfx/MSX++UnDeadPeopleEdition>
- **上游**：MShockXotto+ / MSX 系图集（作者：mshock、Xotto、SomeDeadGuy 等社区贡献者），
  另含 Dungeon Crawl: Stone Soup 的 CC0 图块
- **许可**：**CC BY-SA 3.0**（<https://creativecommons.org/licenses/by-sa/3.0/>）
- **本项目做的修改**：从整张图集按 `tile_config.json` 的索引裁出 32×32，
  并按本项目的物品 id 重命名（例如 `bandages` → `bandage.png`、`mosin91_30` → `svd.png`）。
  **这些裁切/重命名后的图标按 CC BY-SA 3.0 分发。**
- **署名要求**：分发时必须保留本文件或等效的署名说明，并注明许可与修改。

## 2. 地形图集（32×32）：MSX++UnDeadPeopleEdition

- **文件**：`assets/sprites/tiles/terrain_atlas.png`（512×256 = 16 邻居掩码 × 8 地形 × 32px）
- **来源**：同一 BN 仓库 `gfx/MSX++UnDeadPeopleEdition/normal_terrain.png`
  （背景底色取自 `normal_stuff.png`）
- **用到的 CDDA 地形 id**：`t_pavement`（街道）、`t_dirt`（泥土）、`t_grass`（草地，自动拼接）、
  `t_floor`（木地板）、`t_linoleum_white`（瓷砖）、`t_wall`（混凝土外墙，自动拼接）、
  `t_railroad_rubble`（瓦砾）
- **例外（自制）**：**砖墙那一行是本项目自制的 16px 素材 2× 放大**，不是 CDDA 素材，
  见 `WALL_SOURCE`（`tools/build_terrain_atlas.py`）；备份在 `terrain_atlas.png.16px.bak`。
- **许可**：**CC BY-SA 3.0**（仅限 MSX 来源的图块；自制的砖墙行不受此协议约束）
- **本项目做的修改**：按 CDDA 的全局图块索引裁切 32×32，把 fg 合成到 bg 上保证不透明，
  再按本项目的 16 掩码 × 8 地形布局重排。**重排后的 MSX 来源部分按 CC BY-SA 3.0 分发。**
- **复现**：`tools/build_terrain_atlas.py`（`*_ORDER` 常量就是自动拼接朝向的校准开关）

## 3. 原始 16×16 物品图标

- **文件**：`assets/sprites/items/*.png`
- **来源**：CDDA/BN 系 16×16 图集（ChestHole16 系）裁切，具体图集与作者**待确认**
- **许可**：CC BY-SA 3.0（同上）
- **状态**：已被 `items32` 取代（`items.json` 现指向 32px 版本），保留作为回退

## 4. 手持武器贴图：MSX++UnDeadPeopleEdition

- **文件**：`assets/sprites/weapons_cdda/*.png`（`svd.png` 27×7、`svd_alt_sniper.png` 31×10、
  `svd_alt_mosin.png` 24×7）
- **来源**：同一 BN 仓库 `gfx/MSX++UnDeadPeopleEdition/normal_items.png`
- **用到的 CDDA id**：`overlay_wielded_rifle_308`、`overlay_wielded_rm11b_sniper_rifle`、
  `overlay_wielded_mosin91_30`
- **许可**：**CC BY-SA 3.0**
- **本项目做的修改**：按全局图块索引裁出 32×32 单元，再按 `bbox` 裁掉透明边；
  游戏内 2× 最近邻放大显示。**裁切后的贴图按 CC BY-SA 3.0 分发。**
- **复现**：`tools/extract_cdda_weapon.py`

## 5. 容器 / 尸体贴图（32×32）：MSX++UnDeadPeopleEdition

- **文件**：`assets/sprites/objects32/*.png`（8 张）
- **来源**：同一 BN 仓库 `gfx/MSX++UnDeadPeopleEdition/`
  （`f_fridge` 与 `f_*` 家具在 `normal_furniture.png`/`large_stuff.png`/`normal_offset.png`；`corpse_generic_male` 在 `normal_items.png`）
- **用到的 CDDA id**：`f_fridge`、`f_trashcan`、`f_dumpster`、`f_crate_c`、`f_cardboard_box`、
  `f_safe_c`、`f_locker`、`corpse_generic_male`
- **许可**：**CC BY-SA 3.0**
- **本项目做的修改**：按全局图块索引裁出 32×32 单元（不裁 bbox，保留原画布），把 fg 合成到 bg 上。
- **例外**：`f_fridge` / `f_fridge_o` 在包内图块为**空**（0 个非透明像素），冰箱仍用旧 16×16 自绘贴图（`objects/chest_fridge.png`，代码里按图高自动 ×2）。
- **复现**：`tools/extract_cdda_props.py`

## 6. 其它

- 游戏代码、场景、地形图集、角色/武器帧图：本项目自制（`assets/sprites/tiles/`、
  `characters/`，地形图集由 16px 版 2× 放大而来）。
- 字体：`assets/fonts/NotoSansSC.ttf`（**Noto Sans SC**，SIL Open Font License 1.1，可自由再分发）。

---

> 变更记录：2026-11 为「贴图清晰化（B2）」引入 MSX 32px 图标；同时把世界格从 16px 升到 32px（D2），
> 地形图集暂时是自制 16px 版本的 2× 放大（`tools/upscale_terrain_atlas.py`），不涉及第三方素材。
