#!/usr/bin/env python3
"""把 16px 地形图集放大成 32px（世界格升到 32 时的过渡方案）。

原图集 256×128 = 16 列（邻居掩码）× 8 行（地形）× 16×16
放大后 512×256 = 同样的 16×8 布局 × 32×32 —— MapLayout.AtlasCoords 不用改。

这是**过渡**：像素是 2× 复制，视觉与原来一致；
真正变清晰要等 32px 地形素材（MSX/UltiCa）按同一布局填进来。
"""
import os
import shutil
import sys

from PIL import Image

SRC = os.path.join("test_1", "新建游戏项目", "assets", "sprites", "tiles", "terrain_atlas.png")
BAK = SRC + ".16px.bak"
FACTOR = 2


def main():
    if not os.path.exists(BAK):
        shutil.copy2(SRC, BAK)
        print(f"备份原图集 -> {BAK}")

    src = Image.open(BAK).convert("RGBA")
    out = src.resize((src.width * FACTOR, src.height * FACTOR), Image.NEAREST)
    out.save(SRC)
    print(f"放大：{src.width}×{src.height} -> {out.width}×{out.height}（最近邻，保持像素硬边）")


if __name__ == "__main__":
    main()
