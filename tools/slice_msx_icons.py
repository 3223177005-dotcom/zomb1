#!/usr/bin/env python3
"""从 CDDA/BN 的 MSX++UnDeadPeopleEdition 里切出本项目的 32×32 物品图标。

- 按**全局图块索引**（各 sheet 按像素尺寸累加）定位，不按单张图集行号
- 输出 32×32 画布；`CENTER_IDS` 里的 id 先裁 bbox 再居中（横向枪只占画布一条，不居中在 UI 里会偏下）

用法（项目根目录）：
    python tools/slice_msx_icons.py <msx目录> <输出目录>
"""
import json
import os
import sys

from PIL import Image

# 我们的 id -> MSX(CDDA) id
MAPPING = {
    "bandage": "bandages",
    "svd": "overlay_wielded_rifle_308",   # 与手持武器同一张图（extract_cdda_weapon.py）
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

# 源图是横向枪、只占画布一条 → 切出来要居中
CENTER_IDS = {"svd"}


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
    out_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        "test_1", "新建游戏项目", "assets", "sprites", "items32")

    cfg = json.load(open(os.path.join(msx_dir, "tile_config.json"), encoding="utf-8"))
    info = cfg["tile_info"][0]
    tw, th = int(info["width"]), int(info["height"])

    # 全局索引范围：按 tile_config 顺序累加每张 sheet 的实际图块数
    sheets, ranges, off = {}, [], 0
    for s in cfg.get("tiles-new", []):
        name = s.get("file")
        path = os.path.join(msx_dir, name)
        if not os.path.exists(path):
            continue
        im = Image.open(path).convert("RGBA")
        sheets[name] = im
        n = (im.width // tw) * (im.height // th)
        ranges.append((name, off, off + n))
        off += n

    index = {}
    for s in cfg.get("tiles-new", []):
        for t in s.get("tiles", []):
            ids = t.get("id")
            if ids is None:
                continue
            if isinstance(ids, str):
                ids = [ids]
            for i in ids:
                index.setdefault(i, t)

    def sprite(global_index):
        for name, a, b in ranges:
            if a <= global_index < b:
                im = sheets[name]
                cols = im.width // tw
                l = global_index - a
                return im.crop(((l % cols) * tw, (l // cols) * th,
                                (l % cols) * tw + tw, (l // cols) * th + th))
        return None

    os.makedirs(out_dir, exist_ok=True)
    ok = missing = 0

    for ours, theirs in MAPPING.items():
        entry = index.get(theirs)
        if entry is None:
            print(f"  [!] {ours:<14} <- {theirs:<26} 在 tile_config 里找不到")
            missing += 1
            continue

        gi = first_index(entry.get("fg"))
        if gi is None:
            gi = first_index(entry.get("bg"))
        if gi is None:
            print(f"  [!] {ours:<14} <- {theirs:<26} 没有 fg/bg 索引")
            missing += 1
            continue

        icon = sprite(gi)
        if icon is None:
            print(f"  [!] {ours:<14} <- {theirs:<26} 全局索引 {gi} 落在未下载的 sheet 上")
            missing += 1
            continue

        if ours in CENTER_IDS:
            bb = icon.getbbox()
            if bb:
                body = icon.crop(bb)
                canvas = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
                canvas.alpha_composite(body, ((tw - body.width) // 2, (th - body.height) // 2))
                icon = canvas

        icon.save(os.path.join(out_dir, f"{ours}.png"))
        ok += 1
        print(f"  [ok] {ours:<14} <- {theirs:<26} #{gi}")

    print(f"\n完成：{ok} 张，缺失 {missing} 张 -> {out_dir}")


if __name__ == "__main__":
    main()
