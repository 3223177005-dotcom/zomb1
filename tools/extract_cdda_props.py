#!/usr/bin/env python3
"""从 CDDA/BN 的 MSX++UnDeadPeopleEdition 里提取**原生 32×32** 的容器 / 尸体贴图。

与 weapons 提取不同：这里**不裁 bbox**，保留 32×32 画布 —— 这样 1:1 显示时正好占一格。

用法（在项目根目录）：
    python tools/extract_cdda_props.py <msx目录>

输出：assets/sprites/objects32/<name>.png（32×32，RGBA）
"""
import json
import os
import sys

from PIL import Image

# 本项目文件 -> (CDDA id, 说明)
PROPS = {
    "chest_fridge":    ("f_fridge",            "冰箱"),
    "chest_trashcan":  ("f_trashcan",          "圆金属垃圾桶"),
    "chest_dumpster":  ("f_dumpster",          "方金属垃圾桶/垃圾箱"),
    "chest_crate":     ("f_crate_c",            "封闭木箱"),
    "chest_cardboard": ("f_cardboard_box",     "纸箱"),
    "chest_safe":      ("f_safe_c",            "保险柜"),
    "chest_locker":    ("f_locker",            "储物柜"),
    "corpse":          ("corpse_generic_male", "尸体（男性通用；CDDA 的 corpse 是血渍贴花，不是尸体）"),
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
        "test_1", "新建游戏项目", "assets", "sprites", "objects32")

    cfg = json.load(open(os.path.join(msx, "tile_config.json"), encoding="utf-8"))
    info = cfg.get("tile_info", [{}])[0]
    tw, th = int(info.get("width", TILE)), int(info.get("height", TILE))
    if tw != TILE or th != TILE:
        print(f"[!] 包内 tile_info 是 {tw}×{th}，不是 32×32 —— 停止")
        return

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
    ok = missing = 0
    for ours, (cdda_id, note) in PROPS.items():
        entry = index.get(cdda_id)
        if entry is None:
            print(f"  [!] {ours:<16} 找不到 {cdda_id}（{note}）")
            missing += 1
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
            print(f"  [!] {ours:<16} 索引 {gi} 越界")
            missing += 1
            continue

        # 合成 bg（有些家具带底色）
        bg = first(entry.get("bg"))
        if bg is not None:
            for name, a, b in ranges:
                if a <= bg < b:
                    im = sheets[name]
                    cols = im.width // TILE
                    l = bg - a
                    bgimg = im.crop(((l % cols) * TILE, (l // cols) * TILE,
                                     (l % cols) * TILE + TILE, (l // cols) * TILE + TILE))
                    canvas = bgimg.copy()
                    canvas.alpha_composite(img)
                    img = canvas
                    break

        img.save(os.path.join(out_dir, f"{ours}.png"))
        ok += 1
        print(f"  [ok] {ours:<16} <- {cdda_id:<18} 32×32  {note}")

    print(f"\n完成：{ok} 张，缺失 {missing} 张 -> {out_dir}")
    if missing:
        print("缺失项不要用近似图替代，需人工确认后补映射。")


if __name__ == "__main__":
    main()
