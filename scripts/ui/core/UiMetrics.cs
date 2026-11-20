using Godot;

/// <summary>
/// **UI 设计剂量表**（AGENTS 规则 11：魔法数字只此一份）。
///
/// 基准分辨率 **1280×720**（`project.godot` 的 viewport），版式一律靠锚点 + 容器自适应，
/// 不写绝对坐标；尺寸 / 字号 / 间距全部从这里取，改一处全局生效。
///
/// 字号阶梯对应像素字体 Fusion Pixel 的 12px 设计网格：**12 的整数倍最锐利**
/// （12 / 24 / 36），其余档位仍是无抗锯齿的硬边，只是个别笔画会差 1px。
/// </summary>
public static class UiMetrics
{
    // ---------- 字号阶梯 ----------
    public const int FontTiny = 12;
    public const int FontSmall = 14;
    public const int FontBody = 16;
    public const int FontSubtitle = 20;
    public const int FontTitle = 24;
    public const int FontHero = 32;

    // ---------- 间距 ----------
    public const int GapXs = 2;
    public const int GapSm = 4;
    public const int GapMd = 8;
    public const int GapLg = 12;
    public const int GapXl = 16;

    // ---------- 内边距 ----------
    public const int PadPanel = 16;
    public const int PadRegion = 10;

    // ---------- 尺寸 ----------
    /// <summary>HUD 右侧竖排侧栏宽度（像素）。</summary>
    public const int SidebarWidth = 340;

    /// <summary>背包 / 容器格子边长（像素）。</summary>
    public const int CellPx = 48;

    /// <summary>物品行高度（像素）。</summary>
    public const int RowHeight = 44;

    /// <summary>装备行 / 快捷槽的最小高度（像素）。</summary>
    public const int SlotMinHeight = 76;

    /// <summary>槽内图标框边长（原生物品图标 32px，留框居中）。</summary>
    public const int IconBox = 44;

    /// <summary>物品图标原生边长（assets/sprites 里的 32px 图标）。</summary>
    public const int ItemIconPx = 32;

    // ---------- 进度条高度 ----------
    public const int BarHp = 16;
    public const int BarThin = 8;
    public const int BarDurability = 8;
    public const int BarMicro = 6;

    // ---------- HUD 四角战术布局 ----------

    /// <summary>角落块离屏幕边缘的距离（像素）。</summary>
    public const int HudCorner = 12;

    /// <summary>12×12 原生小图标边长。</summary>
    public const int HudIcon = 12;

    /// <summary>左上「生存状态盒」宽度 / 高度（血·耐力·护甲·头盔·负重，全图标化）。</summary>
    public const int HudVitalsWidth = 172;
    public const int HudVitalsHeight = 118;

    /// <summary>右下「战术武器盒」尺寸（只有武器图 + 弹药 + 模式，不印武器名）。</summary>
    public const int HudWeaponWidth = 184;
    public const int HudWeaponHeight = 58;

    /// <summary>武器图标框（物品图标 32px，等比居中）。</summary>
    public const int HudWeaponIconW = 56;
    public const int HudWeaponIconH = 32;

    /// <summary>底部快捷栏：纯图标小格。</summary>
    public const int HudQuickSlotWidth = 68;
    public const int HudQuickBarHeight = 44;

    /// <summary>右上「消息 / 查看」浮窗尺寸。</summary>
    public const int HudFeedWidth = 260;
    public const int HudFeedHeight = 132;

    // ---------- 面板版式（占屏幕的比例，容器化自适应用） ----------
    /// <summary>Tab 面板左边界（屏宽比例）。</summary>
    public const float PanelAnchorLeft = 0.14f;

    /// <summary>Tab 面板右边界（屏宽比例）——HUD 拆成四角后不再让出侧栏。</summary>
    public const float PanelAnchorRight = 0.86f;

    /// <summary>Tab 面板上边界。</summary>
    public const float PanelAnchorTop = 0.05f;

    /// <summary>Tab 面板下边界。</summary>
    public const float PanelAnchorBottom = 0.95f;
}
