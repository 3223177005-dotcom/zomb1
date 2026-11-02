#!/usr/bin/env python3
"""从 CDDA 素材包里切出**纯枪、不带手**的武器贴图，供 360° 旋转使用。

- 用 `overlay_wielded_<id>`：CDDA 里"握在手上"的图，**横向、朝右、没有手**
  （普通物品图标是 45° 斜放的，不适合旋转）
- 自动裁掉透明边，得到紧贴枪身的矩形，方便按 bbox 算握把/枪口
- 输出到 `assets/sprites/weapons_cdda/<本项目 id>.png`

用法：python tools/extract_cdda_weapon.py <msx目录>
"""
import json
import os
import sys

from PIL import Image

# 本项目武器 id -> (CDDA id, 说明)
WEAPONS = {
    "svd": ("overlay_wielded_rifle_308", "FAL 型战斗步枪（最接近 SVD 的定位）"),
    "svd_alt_sniper": ("overlay_wielded_rm11b_sniper_rifle", "栓动狙击枪（备选）"),
    "svd_alt_mosin": ("overlay_wielded_mosin91_30", "莫辛纳甘（备选）"),
}

TILE = 32


def first(v):
    if isinstance(v, int):
        return v
    if isinstance(v, list) and v:
        x = v[0]
        return x.get("sprite") if isinstance(x, dict) else int(x)
    if isinstance(v, dict):
        return v.get("sprite")
    return None


def main():
    msx = sys.argv[1] if len(sys.argv) > 1 else "_pkg/msx"
    out_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        "test_1", "新建游戏项目", "assets", "sprites", "weapons_cdda")

    cfg = json.load(open(os.path.join(msx, "tile_config.json"), encoding="utf-8"))

    sheets, ranges, off = {}, [], 0
    for s in cfg.get("tiles-new", []):
        name = s.get("file")
        path = os.path.join(msx, name)
        if not os.path.exists(path):
            continue
        im = Image.open(path).convert("RGBA")
        sheets[name] = im
        n = (im.width // TILE) * (im.height // TILE)
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

    os.makedirs(out_dir, exist_ok=True)
    for ours, (cdda_id, note) in WEAPONS.items():
        entry = index.get(cdda_id)
        if entry is None:
            print(f"  [!] {ours}: 找不到 {cdda_id}")
            continue
        gi = first(entry.get("fg"))
        img = None
        for name, a, b in ranges:
            if a <= gi < b:
                im = sheets[name]
                cols = im.width // TILE
                l = gi - a
                img = im.crop(((l % cols) * TILE, (l // cols) * TILE,
                               (l % cols) * TILE + TILE, (l // cols) * TILE + TILE))
                break
        if img is None:
            print(f"  [!] {ours}: 索引 {gi} 越界")
            continue
        bb = img.getbbox()
        if bb is None:
            print(f"  [!] {ours}: 全透明")
            continue
        img.crop(bb).save(os.path.join(out_dir, f"{ours}.png"))
        print(f"  [ok] {ours:<16} <- {cdda_id:<36} {bb[2]-bb[0]}×{bb[3]-bb[1]}  ({note})")

    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
