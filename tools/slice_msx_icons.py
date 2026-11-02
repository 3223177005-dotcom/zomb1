#!/usr/bin/env python3
"""从 CDDA/BN 的 MSX++UnDeadPeopleEdition（32×32）里切出本项目的物品图标。

用法（在项目根目录）：
    python tools/slice_msx_icons.py <msx目录> <输出目录>

<msx目录> 需含 tile_config.json 与 normal_items.png。
输出：<输出目录>/<我们的id>.png，32×32，RGBA。
"""
import json
import os
import sys

from PIL import Image

# 我们的物品 id -> MSX(CDDA) 里的 id
MAPPING = {
    "bandage": "bandages",
    "svd": "mosin91_30",          # 无 svd；取外形最接近的栓动长步枪
    "dagger": "knife_combat",
    "backpack": "backpack",
    "cash": "money",
    "circuit": "circuit",
    "gold": "gold_small",
    "intel": "file",
    "watch": "gold_watch",
    "sample": "blood",
    "silencer": "suppressor",
    "extmag": "stanag30",         # 竖长弹匣，正好配 1×2
    "tacgrip": "grip",
    "ammo_762x54r": "762_54R",
}


def first_index(value):
    """fg/bg 可能是 int，也可能是 [{weight, sprite}] 列表。"""
    if value is None:
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, list):
        if not value:
            return None
        item = value[0]
        return item.get("sprite") if isinstance(item, dict) else int(item)
    if isinstance(value, dict):
        return value.get("sprite")
    return None


def main():
    msx_dir = sys.argv[1] if len(sys.argv) > 1 else "_pkg/msx"
    out_dir = sys.argv[2] if len(sys.argv) > 2 else "test_1/新建游戏项目/assets/sprites/items32"

    cfg = json.load(open(os.path.join(msx_dir, "tile_config.json"), encoding="utf-8"))
    info = cfg["tile_info"][0]
    tw, th = int(info["width"]), int(info["height"])

    # id -> (sheet 文件名, 条目)
    index = {}
    for sheet in cfg.get("tiles-new", []):
        for tile in sheet.get("tiles", []):
            ids = tile.get("id")
            if ids is None:
                continue
            if isinstance(ids, str):
                ids = [ids]
            for i in ids:
                index.setdefault(i, (sheet.get("file"), tile))

    sheets = {}
    os.makedirs(out_dir, exist_ok=True)
    ok = missing = 0

    for ours, theirs in MAPPING.items():
        entry = index.get(theirs)
        if entry is None:
            print(f"  [!] {ours:<14} <- {theirs:<16} 在 tile_config 里找不到")
            missing += 1
            continue

        sheet_name, tile = entry
        sprite = first_index(tile.get("fg"))
        if sprite is None:
            sprite = first_index(tile.get("bg"))
        if sprite is None:
            print(f"  [!] {ours:<14} <- {theirs:<16} 没有 fg/bg 索引")
            missing += 1
            continue

        if sheet_name not in sheets:
            path = os.path.join(msx_dir, sheet_name)
            if not os.path.exists(path):
                print(f"  [!] 缺图集文件 {path}")
                missing += 1
                continue
            sheets[sheet_name] = Image.open(path).convert("RGBA")
        sheet = sheets[sheet_name]

        cols = sheet.width // tw
        x = (sprite % cols) * tw
        y = (sprite // cols) * th
        if y + th > sheet.height:
            print(f"  [!] {ours}: 索引 {sprite} 越界（{sheet.size}）")
            missing += 1
            continue

        icon = sheet.crop((x, y, x + tw, y + th))
        icon.save(os.path.join(out_dir, f"{ours}.png"))
        ok += 1
        print(f"  [ok] {ours:<14} <- {theirs:<16} sheet={sheet_name} #{sprite}")

    print(f"\n完成：{ok} 张，缺失 {missing} 张 -> {out_dir}")


if __name__ == "__main__":
    main()
