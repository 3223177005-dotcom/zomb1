#!/usr/bin/env python3
"""去掉武器贴图里**预先画好的手**（`side_*.png`）。

背景：`characters/weapon/*.png` 是"角色握着枪"的整套贴图，枪身上自带两只手
（后手握把 + 前手护木）。人物精灵本身也有手，于是屏幕上会出现"三只手"。

做法：把皮肤色像素用**最近的枪身像素**填掉（不是删成透明，避免枪上出现洞），
原图备份为 `*.withhands.bak`。

用法（项目根目录）：python tools/strip_weapon_hands.py
"""
import os
import shutil
from collections import deque

from PIL import Image

WEAPON_DIR = os.path.join("test_1", "新建游戏项目", "assets", "sprites", "characters", "weapon")
TARGETS = [
    "side_idle.png", "side_shoot.png", "side_reload.png", "side_death.png",
    "down_idle.png", "down_shoot.png", "down_reload.png",
    "up_idle.png", "up_shoot.png", "up_reload.png",
]

# 皮肤色（含阴影），取自贴图实际调色板
SKIN = {(230, 194, 176, 255), (186, 134, 143, 255)}


def is_skin(px):
    return px in SKIN


def inpaint(img):
    """把皮肤像素换成最近的非皮肤像素颜色。"""
    w, h = img.size
    px = img.load()
    skin = [(x, y) for y in range(h) for x in range(w) if is_skin(px[x, y])]
    if not skin:
        return 0

    # 多源 BFS：从每个皮肤像素出发，向外找第一个非皮肤像素
    filled = {}
    queue = deque()
    seen = set(skin)
    for (x, y) in skin:
        queue.append((x, y, 0))

    while queue:
        x, y, d = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not (0 <= nx < w and 0 <= ny < h):
                continue
            p = px[nx, ny]
            if p[3] == 0:
                continue
            if is_skin(p):
                if (nx, ny) not in seen:
                    seen.add((nx, ny))
                    queue.append((nx, ny, d + 1))
                continue
            # 找到外层枪身颜色，回填整条链上的皮肤像素
            filled[(x, y)] = p
            break

    for (x, y), color in filled.items():
        px[x, y] = color
    return len(filled)


def main():
    total = 0
    for name in TARGETS:
        path = os.path.join(WEAPON_DIR, name)
        if not os.path.exists(path):
            print(f"  跳过（不存在）{name}")
            continue

        bak = path + ".withhands.bak"
        if not os.path.exists(bak):
            shutil.copy2(path, bak)

        img = Image.open(bak).convert("RGBA")
        n = inpaint(img)
        img.save(path)
        total += n
        print(f"  {name:<16} 填掉 {n} 个皮肤像素")

    print(f"\n完成，共 {total} 像素；原图备份 *{'.withhands.bak'}")


if __name__ == "__main__":
    main()
