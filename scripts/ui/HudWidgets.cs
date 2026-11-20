using Godot;

/// <summary>
/// HUD 的**静态控件工厂**：角落悬浮盒 / 12px 小图标 / 进度条 / 条内压字 / 文字。
///
/// 为什么单独一个文件：四角 HUD 到处在重复「盒子 + 图标 + 条 + 字」这四件事，
/// 散落在搭建代码里会让 <see cref="HudSidebar"/> 迅速超过 400 行（AGENTS 规则 1）。
/// 这里只造控件，不持有任何状态。
/// </summary>
public static class HudWidgets
{
    /// <summary>浅色面板上的正文色（深炭墨，像打字机印在档案卡上）。</summary>
    public static readonly Color BodyColor = new(0.15f, 0.13f, 0.10f);
    public static readonly Color HintColor = new(0.42f, 0.37f, 0.30f);
    public static readonly Color AmmoColor = new(0.15f, 0.13f, 0.10f);
    public static readonly Color ModeColor = new(0.48f, 0.18f, 0.12f);

    /// <summary>压在**深色进度条 / 世界地图**上的字（亮米白 + 深描边），浅底上用不着。</summary>
    public static readonly Color BarTextColor = new(0.96f, 0.93f, 0.86f);

    /// <summary>深色描边：只给"压在深色底上"的亮字用。</summary>
    public static readonly Color OutlineColor = new(0.08f, 0.06f, 0.05f, 0.92f);

    /// <summary>
    /// 贴到四角的悬浮盒（紧凑九宫格 <see cref="UiStyle.HudBox"/>）。
    /// 尺寸固定、偏移相对锚点算，所以换分辨率也不会跑位。
    /// </summary>
    public static PanelContainer Corner(Control.LayoutPreset preset, int width, int height, int margin)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiStyle.HudBox());
        panel.SetAnchorsPreset(preset);

        switch (preset)
        {
            case Control.LayoutPreset.TopRight:
                panel.OffsetLeft = -margin - width;
                panel.OffsetTop = margin;
                panel.OffsetRight = -margin;
                panel.OffsetBottom = margin + height;
                break;

            case Control.LayoutPreset.BottomRight:
                panel.OffsetLeft = -margin - width;
                panel.OffsetTop = -margin - height;
                panel.OffsetRight = -margin;
                panel.OffsetBottom = -margin;
                break;

            case Control.LayoutPreset.CenterBottom:
                panel.OffsetLeft = -width * 0.5f;
                panel.OffsetTop = -margin - height;
                panel.OffsetRight = width * 0.5f;
                panel.OffsetBottom = -margin;
                break;

            default:   // TopLeft
                panel.OffsetLeft = margin;
                panel.OffsetTop = margin;
                panel.OffsetRight = margin + width;
                panel.OffsetBottom = margin + height;
                break;
        }
        return panel;
    }

    /// <summary>12×12 原生小图标（`assets/sprites/ui/icon_*.png`，1:1 显示保持锐利）。</summary>
    public static TextureRect Icon(string name)
    {
        var size = new Vector2(UiMetrics.HudIcon, UiMetrics.HudIcon);
        return new TextureRect
        {
            Texture = UiStyle.LoadUiIcon(name),
            CustomMinimumSize = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    /// <summary>像素分节进度条。</summary>
    public static ProgressBar Bar(int height, string fill)
    {
        var bar = new ProgressBar
        {
            MaxValue = 100.0,
            Value = 100.0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0.0f, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        UiStyle.StylePixelBar(bar, fill);
        return bar;
    }

    /// <summary>把一行字**压在条的正中央**（带描边）—— 省掉单独一行数值。</summary>
    public static Label TextInside(ProgressBar bar, int fontSize, Color color)
    {
        var label = Text("", fontSize, color, outline: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return label;
    }

    public static Label Text(string text, int fontSize, Color color, bool outline = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);

        if (outline)
        {
            label.AddThemeColorOverride("font_outline_color", OutlineColor);
            label.AddThemeConstantOverride("outline_size", 3);
        }
        return label;
    }

    /// <summary>HUD 里的富文本（消息 / 查看）：BBCode 上色，不参与鼠标。</summary>
    public static RichTextLabel Rich(int fontSize, Color? color = null)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rich.AddThemeFontSizeOverride("normal_font_size", fontSize);
        rich.AddThemeColorOverride("default_color", color ?? BodyColor);
        return rich;
    }

    /// <summary>横向排列（带统一间距）。</summary>
    public static HBoxContainer Row(int separation)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    /// <summary>纵向排列（带统一间距）。</summary>
    public static VBoxContainer Column(int separation)
    {
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", separation);
        return col;
    }
}
