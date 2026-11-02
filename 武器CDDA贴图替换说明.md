## 7. 枪械贴图换源：CDDA 素材（MSX++UnDeadPeopleEdition）

### 7.1 选型与图源

| 本项目 id | CDDA id | 源图 | 尺寸 | 说明 |
|---|---|---|---|---|
| `svd` | `overlay_wielded_rifle_308` | `normal_items.png` | 27×7 | FAL 型战斗步枪，横向、朝右、**不带手** |
| `svd_alt_sniper` | `overlay_wielded_rm11b_sniper_rifle` | `normal_items.png` | 31×10 | 备选：狙击枪 |
| `svd_alt_mosin` | `overlay_wielded_mosin91_30` | `normal_items.png` | 24×7 | 备选：莫辛纳甘 |

- 只替换 **360° 用的那一张**；旧 `characters/weapon/*.png` 保留做回退（`UseCddaGun = false`）。
- 为什么用 `overlay_wielded_*` 而不是普通物品图标：**普通物品图标在 CDDA 里是 45° 斜放的**
  （`rifle_308` 实测就是斜的），拿来旋转会歪；`overlay_wielded_*` 是正的横向图。
- 为什么不再需要 `strip_weapon_hands.py`：CDDA 的 wielded 图**本来就不带手**，
  所以 360° 模式下不依赖去手脚本（脚本保留，旧贴图仍在用）。

### 7.2 提取工具

`tools/extract_cdda_weapon.py`：

- 读 `tile_config.json`，按**全局图块索引**（各 sheet 按尺寸累加）定位，不按单张行号；
- 自动按 `bbox` 裁掉透明边 → 得到紧贴枪身的矩形（便于算握把/枪口）；
- 输出 `assets/sprites/weapons_cdda/<id>.png`。

### 7.3 规格适配（关键数字）

| 项 | 值 |
|---|---|
| 帧尺寸 | 单帧（不是序列帧），`Hframes = 1` |
| 缩放 | `_gun.Scale = 2`（与角色 2× 一致）→ `svd` 实际显示 54×14 世界像素 |
| 握把 | 长度 **35%** 处、垂直居中（枪托在左）：`grip = (w×0.35, h/2)` |
| `_gun.Offset` | `centre - grip`，让握把落在节点原点 → 旋转中心就是手 |
| `MuzzleLocal` | `(w - grip.x) × 2`，即枪身右端 × 2 倍缩放 |
| 枪焰 | `_flash.Offset = (6, 0)`（火根从帧中心左移 6 texel），再往枪管里压 3px |
| 后坐 | 没有 shoot 序列帧，用 `_recoil` 沿局部 -X 位移顶替（3px → 0，60px/s 回弹） |

### 7.4 踩过的坑

1. **`fg/bg` 是全局索引**：直接当单张图集行号会取到空图（MSX 有 7 张 sheet 累加）。
2. **普通图标是斜的**：`rifle_308` 这类物品图标 45° 斜放，只能用于 UI 图标，不能用于旋转。
3. **枪焰贴图方向**：旧代码按 `_dir` 取 `{dir}_fire.png`，CDDA 模式下 `_dir` 不是 `side`，
   会拿到竖向的 `down_fire`（16×17），贴在横向枪上严重错位 → `PlayFlash()` 里强制用 `side_fire`。
4. **火根不在贴图最左**：`side_fire` 单帧 20×11，火根在 texel 4，帧中心 10 → 必须 `Offset = 6`，
   否则火苗缩在枪管里。CDDA 枪管偏细，再往内压 3px 才贴住。
5. **构建失败会静默跑旧程序集**：`dotnet build` 报错时 Godot 仍能启动，但跑的是上一次的 dll，
   表现为"代码改了没反应 / 脚本类找不到"。改完先看 build 的 `0 错误` 再看运行结果。

### 7.5 回退

- `WeaponVisual.UseCddaGun = false` → 回到 `characters/weapon/*.png`（已去手）那套；
- `Player.WeaponFollowAim360 = false` → 回到 down/up/side 吸附模式（CDDA 贴图也支持，
  只是角度吸附到 0/±90/180）。

### 7.6 授权

CDDA 素材（MSX++UnDeadPeopleEdition）为 **CC BY-SA 3.0**，署名与来源见
`THIRD_PARTY_NOTICES.md` 第 5 节；裁切/缩放属于修改，衍生图继续同协议。
