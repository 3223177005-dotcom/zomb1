using Godot;

/// <summary>
/// 全屏面板背后的**暗色遮罩**：面板一开，把世界与四角 HUD 压暗。
///
/// 为什么单独抽出来：`EquipmentPanel.cs` 早已超过 400 行（AGENTS 规则 1），
/// 不允许再往里堆控件搭建代码；这块逻辑与面板业务无关，独立成静态工厂最省事。
/// </summary>
public static class UiDimLayer
{
    /// <summary>遮罩颜色（够暗但不至于看不见地形轮廓）。</summary>
    public static readonly Color DimColor = new(0.02f, 0.03f, 0.04f, 0.62f);

    /// <summary>
    /// 在 <paramref name="host"/>（通常是 CanvasLayer）下建一层铺满屏幕的遮罩，
    /// 并垫到最底层；初始隐藏，由调用方按面板开合切换 <c>Visible</c>。
    /// </summary>
    public static ColorRect Create(Node host)
    {
        var dim = new ColorRect
        {
            Name = "Dim",
            Color = DimColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };

        host.AddChild(dim);
        host.MoveChild(dim, 0);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return dim;
    }
}
