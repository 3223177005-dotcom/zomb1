#!/usr/bin/env python3
"""把 MSX++UnDeadPeopleEdition 的 32px 地形填进本项目的 16 掩码地形图集。

输出：assets/sprites/tiles/terrain_atlas.png —— 512×256（16 列掩码 × 8 行地形 × 32px）

掩码定义（MapLayout.MaskAt）：上=1 右=2 下=4 左=8，同种地形或同为实心墙算连通。

★ CDDA 的 fg/bg 是**全局图块索引**（按 tile_config 里 tiles-new 的 sheet 顺序累加），
  不是单张图集内的行号。所以必须先按像素尺寸算出每张图的起始偏移，再换算成本地索引。

自动拼接：CDDA 的 multitile 变体（center / corner / t_connection / edge / end_piece /
unconnected）各有固定旋转顺序，下面的 *_ORDER 决定"第 0 张对应哪个朝向"。
朝向不对时只改这几行常量再跑一次（游戏代码不用动）。
"""
import json
import os
import sys

from PIL import Image

# ---- 掩码 → CDDA 变体 + 旋转序号 ----------------------------------------
END_ORDER = [1, 2, 4, 8]        # 单边相连：上 右 下 左
CORNER_ORDER = [3, 6, 12, 9]    # 两边相邻：上右 右下 下左 左上
TCONN_ORDER = [7, 14, 13, 11]   # 三边相连：上右下 右下左 下左上 左上右
EDGE_ORDER = [5, 10]            # 两边相对：上下(竖) 左右(横)

# ---- 墙用哪一套（按地形名指定）----------------------------------------
# "msx"   ：用 TERRAINS 里给的 MSX 地形（实心感强、灰石色）
# "own16" ：保留本项目自制的 16px 墙，2× 放大（真·实心块，颜色可区分）
WALL_SOURCE = {
    "BrickWall": "own16",    # 红砖：自制 16px 2×，房间一眼可辨
    "ConcreteWall": "msx",   # 混凝土外框：MSX 灰墙，和房间区分开
}

# ---- 我们的 8 种地形 → MSX 里的地形 id --------------------------------
# flat=True：不做自动拼接（所有掩码用同一张）
TERRAINS = [
    ("Street",       "t_pavement",        True),
    ("Dirt",         "t_dirt",            True),
    ("Grass",        "t_grass",           False),
    ("WoodFloor",    "t_floor",           True),
    ("TiledFloor",   "t_linoleum_white",  True),
    ("BrickWall",    "t_wall",            False),
    ("ConcreteWall", "t_wall",            False),
    ("Rubble",       "t_railroad_rubble", True),
]

TILE = 32
COLS, ROWS = 16, 8


def pick(value):
    """fg/bg 可能是 int、旋转组列表或权重列表，取第一张。"""
    if value is None:
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, list):
        if not value:
            return None
        v = value[0]
        return v.get("sprite") if isinstance(v, dict) else int(v)
    if isinstance(value, dict):
        return value.get("sprite")
    return None


class Atlas:
    """按 CDDA 的全局索引取图块。"""

    def __init__(self, msx_dir, cfg):
        self.dir = msx_dir
        self.sheets = {}      # file -> Image
        self.offsets = []     # (file, start, end)
        offset = 0
        for s in cfg.get("tiles-new", []):
            name = s.get("file")
            path = os.path.join(msx_dir, name)
            if not os.path.exists(path):
                # 缺图集就跳过；后面解析到的索引会落到"未知"区间并报警
                continue
            im = Image.open(path).convert("RGBA")
            count = (im.width // TILE) * (im.height // TILE)
            self.sheets[name] = im
            self.offsets.append((name, offset, offset + count))
            offset += count

    def sprite(self, index):
        if index is None:
            return None
        for name, start, end in self.offsets:
            if start <= index < end:
                im = self.sheets[name]
                cols = im.width // TILE
                local = index - start
                x = (local % cols) * TILE
                y = (local // cols) * TILE
                return im.crop((x, y, x + TILE, y + TILE))
        return None


def main():
    msx_dir = sys.argv[1] if len(sys.argv) > 1 else "_pkg/msx"
    out_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        "test_1", "新建游戏项目", "assets", "sprites", "tiles", "terrain_atlas.png")

    cfg = json.load(open(os.path.join(msx_dir, "tile_config.json"), encoding="utf-8"))
    atlas_src = Atlas(msx_dir, cfg)
    print("已加载图集：" + "、".join(f"{n}({b - a})" for n, a, b in atlas_src.offsets))

    index = {}
    for sheet in cfg.get("tiles-new", []):
        for tile in sheet.get("tiles", []):
            ids = tile.get("id")
            if ids is None:
                continue
            if isinstance(ids, str):
                ids = [ids]
            for i in ids:
                index.setdefault(i, tile)

    def render(tid, mask):
        entry = index.get(tid)
        if entry is None:
            return None

        fg = bg = None
        if not entry.get("multitile"):
            fg = atlas_src.sprite(pick(entry.get("fg")))
            bg = atlas_src.sprite(pick(entry.get("bg")))
        else:
            variants = {a.get("id"): a for a in entry.get("additional_tiles", [])}

            def variant_fg(name, order, mask_value):
                v = variants.get(name)
                if v is None:
                    return None
                group = v.get("fg")
                if not isinstance(group, list):
                    return atlas_src.sprite(pick(group))
                i = order.index(mask_value) if mask_value in order else 0
                return atlas_src.sprite(pick(group[i] if i < len(group) else group[0]))

            if mask == 15:
                v = variants.get("center")
                fg = atlas_src.sprite(pick(v.get("fg"))) if v else atlas_src.sprite(pick(entry.get("fg")))
            elif mask == 0:
                v = variants.get("unconnected")
                fg = atlas_src.sprite(pick(v.get("fg"))) if v else atlas_src.sprite(pick(entry.get("fg")))
            elif mask in END_ORDER:
                fg = variant_fg("end_piece", END_ORDER, mask)
            elif mask in CORNER_ORDER:
                fg = variant_fg("corner", CORNER_ORDER, mask)
            elif mask in TCONN_ORDER:
                fg = variant_fg("t_connection", TCONN_ORDER, mask)
            elif mask in EDGE_ORDER:
                fg = variant_fg("edge", EDGE_ORDER, mask)

            bg = atlas_src.sprite(pick(entry.get("bg")))

        if fg is None:
            return None
        if bg is None:
            return fg
        out = bg.copy()
        out.alpha_composite(fg)
        return out

    atlas = Image.new("RGBA", (COLS * TILE, ROWS * TILE), (0, 0, 0, 0))
    missing = []
    for row, (name, tid, flat) in enumerate(TERRAINS):
        for mask in range(COLS):
            img = render(tid, 15 if flat else mask)
            if img is None:
                missing.append(f"{name}/{mask}")
                continue
            atlas.paste(img, (mask * TILE, row * TILE))
        print(f"  {name:<13} <- {tid:<18} {'flat' if flat else 'autotile'}")

    os.makedirs(os.path.dirname(out_path), exist_ok=True)

    # ---- 按 WALL_SOURCE 覆盖个别墙行 ----
    own_rows = [i for i, (name, _, _) in enumerate(TERRAINS) if WALL_SOURCE.get(name) == "own16"]
    if own_rows:
        bak_path = out_path + ".16px.bak"
        if not os.path.exists(bak_path):
            raise SystemExit(f"缺少自制 16px 图集备份：{bak_path}")
        bak = Image.open(bak_path).convert("RGBA")
        for row in own_rows:
            strip = bak.crop((0, row * 16, COLS * 16, row * 16 + 16))
            strip = strip.resize((COLS * TILE, TILE), Image.NEAREST)
            atlas.paste(strip, (0, row * TILE))
            print(f"  {TERRAINS[row][0]:<13} <- 自制 16px 图集 2× 放大")

    atlas.save(out_path)
    print(f"\n写出 {out_path}（{atlas.width}×{atlas.height}）")
    if missing:
        print(f"[!] 缺失 {len(missing)} 格：{missing[:8]}…")


if __name__ == "__main__":
    main()
