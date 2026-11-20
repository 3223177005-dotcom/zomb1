"""生成全套 UI 像素素材：九宫格面板 / 槽位 / 分节进度条 / 12x12 小图标 / HUD 角盒。

风格：**旧帆布 + 战地档案卡**（暖卡其帆布底 + 深褐铁框 + 古铜铆钉 + 铁锈红织带）。
浅色底的意义：物品图标本身是深黑铁色 + 1px 黑描边，放在浅米色格子上轮廓才跳得出来；
深色底会把枪械/钢盔糊成一团。

用法：python tools/gen_hud_icons.py
输出：assets/sprites/ui/*.png（RGBA 硬边，无抗锯齿，最近邻显示）
"""

import os

from PIL import Image

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "sprites", "ui")

# ---------------------------------------------------------------- 旧帆布调色板

C_OUTER = (38, 32, 24, 255)            # 深褐铁框外沿
C_PANEL_BG = (192, 180, 156, 250)      # 暖卡其帆布主底
C_PANEL_BG2 = (186, 174, 150, 250)     # 交替列微纹理
C_REGION_BG = (168, 156, 132, 248)     # 内嵌牛皮纸凹槽
C_REGION_BG2 = (164, 152, 128, 248)
C_BEVEL_LIGHT = (224, 214, 192, 255)   # 帆布亮边（上/左）
C_BEVEL_SHADE = (124, 112, 92, 255)    # 深卡其暗折边（下/右）
C_INNER_RING = (110, 98, 78, 255)      # 内圈缝线 / 铜框线
C_BRASS_RIVET = (92, 72, 42, 255)      # 深古铜铆钉
C_AMBER = (168, 76, 36, 255)           # 铁锈朱红（悬停描边 / 织带条）
C_AMBER_DIM = (122, 54, 26, 255)

# 状态色（浅底上要够深才看得清）
HP = {"main": (184, 52, 42, 255), "hi": (212, 76, 64, 255), "lo": (124, 32, 24, 255)}
OLIVE = {"main": (82, 118, 56, 255), "hi": (108, 146, 78, 255), "lo": (52, 78, 34, 255)}
AMBER = {"main": (188, 128, 42, 255), "hi": (216, 156, 64, 255), "lo": (132, 86, 24, 255)}
IRON = {"main": (92, 86, 76, 255), "hi": (124, 116, 104, 255), "lo": (58, 54, 46, 255)}

ICON_PALETTE = {
    ".": (0, 0, 0, 0),
    "K": C_OUTER,
    # 战地医疗深红
    "r": HP["lo"], "R": HP["main"], "H": HP["hi"],
    # 陆军野战绿
    "c": OLIVE["lo"], "C": OLIVE["main"], "E": OLIVE["hi"],
    # 古铜琥珀
    "a": AMBER["lo"], "A": AMBER["main"], "Y": AMBER["hi"],
    # 深铁铅灰
    "g": IRON["lo"], "G": IRON["main"], "W": IRON["hi"],
}

ICONS = {
    # 医疗十字（战地深红）
    "icon_hp": [
        "............",
        "....KKKK....",
        "...KHRRRK...",
        "...KHRRRK...",
        "KKKKHRRRKKKK",
        "KHHRRRRRRRRK",
        "KRRRRRRRRRRK",
        "KKKKRRRrKKKK",
        "...KRRRrK...",
        "...KRRRrK...",
        "....KKKK....",
        "............",
    ],
    # 耐力闪电（古铜琥珀）
    "icon_stamina": [
        ".....KKK....",
        "....KYAAK...",
        "...KYAAK....",
        "..KYAAK.....",
        ".KYYYaaKKK..",
        ".KKYYYaaK...",
        "...KYAAaK...",
        "..KYAAaK....",
        ".KYAAaK.....",
        ".KKKKK......",
        "............",
        "............",
    ],
    # 防弹衣（陆军野战绿）
    "icon_armor": [
        "............",
        "..KKKKKKKK..",
        "..KEEEEEEK..",
        ".KECCCCCCEK.",
        ".KECKKKKCEK.",
        ".KECKccKCEK.",
        ".KECKccKCEK.",
        ".KECCCCCCEK.",
        "..KCCCCCCK..",
        "..KCKCCKCK..",
        "..KKKKKKKK..",
        "............",
    ],
    # 头盔（陆军野战绿）
    "icon_helmet": [
        "............",
        "....KKKK....",
        "..KKEEEEKK..",
        ".KECCCCCCEK.",
        ".KECCCCCCEK.",
        ".KCCCCCCCCK.",
        ".KKKKKKKKKK.",
        ".KccccccccK.",
        "..KCCCCCCK..",
        "..KKKKKKKK..",
        "............",
        "............",
    ],
    # 负重砝码（深铁铅灰，浅底上更醒目）
    "icon_weight": [
        "............",
        "....KKKK....",
        "...KWGGGK...",
        "..KKGGGGKK..",
        ".KGGGGGGGGK.",
        ".KGWGGGGWGK.",
        ".KGGGGGGGGK.",
        ".KGWGGGGWGK.",
        "..KGGGGGGK..",
        "...KKKKKK...",
        "............",
        "............",
    ],
}


def _px(img):
    return img.load()


def write_icon(name, rows):
    """按字符图生成 12x12 RGBA 图标。"""
    size = 12
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = _px(img)
    for y, row in enumerate(rows[:size]):
        for x, ch in enumerate(row[:size]):
            color = ICON_PALETTE.get(ch)
            if color is None:
                raise ValueError("未知调色板字符 %r in %s" % (ch, name))
            px[x, y] = color

    path = os.path.join(OUT_DIR, name + ".png")
    img.save(path)
    return path


def write_panel(name, size, bg, bg2):
    """九宫格面板：深褐铁框 + 双层折边 + 内圈缝线 + 四角古铜铆钉。"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = _px(img)

    for y in range(size):
        for x in range(size):
            edge_x = x in (0, size - 1)
            edge_y = y in (0, size - 1)

            # 四角切 1px（斜切角，像素机械感）
            if edge_x and edge_y:
                continue

            if x == 0 or y == 0 or x == size - 1 or y == size - 1:
                px[x, y] = C_OUTER
            elif x == 1 or y == 1:
                px[x, y] = C_BEVEL_LIGHT
            elif x == size - 2 or y == size - 2:
                px[x, y] = C_BEVEL_SHADE
            elif x == 2 or y == 2 or x == size - 3 or y == size - 3:
                px[x, y] = C_INNER_RING
            else:
                px[x, y] = bg if x % 2 == 0 else bg2

    # 四角铆钉（落在 6px 固定边角内，不会被九宫格拉伸）
    for rx, ry in ((3, 3), (size - 4, 3), (3, size - 4), (size - 4, size - 4)):
        px[rx, ry] = C_BRASS_RIVET

    path = os.path.join(OUT_DIR, name + ".png")
    img.save(path)
    return path


def write_slot(name, fill, accent=None):
    """20x20 槽位：铁框 + 帆布亮/暗折边 + 填充；accent 画一圈内描边（悬停/按下）。"""
    size = 20
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = _px(img)

    for y in range(size):
        for x in range(size):
            if (x in (0, size - 1)) and (y in (0, size - 1)):
                continue
            if x == 0 or y == 0 or x == size - 1 or y == size - 1:
                px[x, y] = C_OUTER
            elif x == 1 or y == 1:
                px[x, y] = C_BEVEL_LIGHT
            elif x == size - 2 or y == size - 2:
                px[x, y] = C_BEVEL_SHADE
            else:
                px[x, y] = fill

    if accent is not None:
        for i in range(2, size - 2):
            px[i, 2] = accent
            px[i, size - 3] = accent
            px[2, i] = accent
            px[size - 3, i] = accent

    path = os.path.join(OUT_DIR, name + ".png")
    img.save(path)
    return path


def write_bar(name, colors=None):
    """18x8 分节进度条：深卡其凹槽 / 着色填充 + 每 3px 一条分节线。"""
    w, h = 18, 8
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = _px(img)

    for y in range(h):
        for x in range(w):
            if x == 0 or y == 0 or x == w - 1 or y == h - 1:
                px[x, y] = C_OUTER
            elif colors is None:
                # 凹槽：深卡其底 + 上沿更暗的一线，形成凹陷感
                if y == 1:
                    px[x, y] = (96, 86, 70, 255)
                elif y == h - 2:
                    px[x, y] = (134, 124, 106, 255)
                else:
                    px[x, y] = (118, 108, 90, 245)
            else:
                # 分节线：每 3 列一条暗线
                if (x - 2) % 3 == 0:
                    px[x, y] = colors["lo"]
                elif y == 1 or x == 1:
                    px[x, y] = colors["hi"]
                elif y == h - 2:
                    px[x, y] = colors["lo"]
                else:
                    px[x, y] = colors["main"]

    path = os.path.join(OUT_DIR, name + ".png")
    img.save(path)
    return path


def write_hud_box():
    """16x16 九宫格角盒：半透羊皮卡其，左缘 2px 铁锈红战地织带条。"""
    size = 16
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = _px(img)

    bg = (196, 184, 160, 236)
    bg_scan = (191, 179, 155, 236)

    for y in range(size):
        for x in range(size):
            if (x in (0, size - 1)) and (y in (0, size - 1)):
                continue
            if x == 0 or y == 0 or x == size - 1 or y == size - 1:
                px[x, y] = C_OUTER
            elif x == 1 or y == 1:
                px[x, y] = C_BEVEL_LIGHT
            elif x == size - 2 or y == size - 2:
                px[x, y] = C_BEVEL_SHADE
            else:
                px[x, y] = bg if y % 2 == 0 else bg_scan

    # 左缘 2px 铁锈红织带条（顶/底各留 1px 暗角过渡）
    for y in range(2, size - 2):
        px[1, y] = C_AMBER
        px[2, y] = C_AMBER
    px[1, 2] = C_AMBER_DIM
    px[2, 2] = C_AMBER_DIM
    px[1, size - 3] = C_AMBER_DIM
    px[2, size - 3] = C_AMBER_DIM

    path = os.path.join(OUT_DIR, "hud_box.png")
    img.save(path)
    return path


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    written = []
    # 面板 / 区域
    written.append(write_panel("panel_outer", 24, C_PANEL_BG, C_PANEL_BG2))
    written.append(write_panel("panel_region", 24, C_REGION_BG, C_REGION_BG2))

    # 槽位三态（悬停/按下带铁锈红描边）
    written.append(write_slot("slot_normal", (204, 194, 172, 255)))
    written.append(write_slot("slot_hover", (218, 208, 186, 255), accent=C_AMBER))
    written.append(write_slot("slot_pressed", (226, 214, 184, 255), accent=C_AMBER))

    # 进度条：凹槽 + 三种状态色
    written.append(write_bar("bar_bg"))
    written.append(write_bar("bar_hp", HP))
    written.append(write_bar("bar_armor", OLIVE))
    written.append(write_bar("bar_amber", AMBER))

    # HUD 角盒 + 小图标
    written.append(write_hud_box())
    for name, rows in ICONS.items():
        written.append(write_icon(name, rows))

    for path in written:
        print("wrote", path)


if __name__ == "__main__":
    main()
