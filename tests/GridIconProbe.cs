using Godot;

/// <summary>
/// 背包图标尺寸探针：把真实的 svd / bandage 物品做成格子块，打印实际图标框尺寸。
/// Godot --headless --path . res://tests/GridIconProbe.tscn
/// </summary>
public partial class GridIconProbe : Node
{
    public override void _Ready()
    {
        var svd = ItemDatabase.Instance?.GetItem("svd");
        var bandage = ItemDatabase.Instance?.GetItem("bandage");
        if (svd == null || bandage == null)
        {
            GD.PrintErr("物品数据没加载");
            GetTree().Quit(1);
            return;
        }

        bool ok = true;
        ok &= Measure("svd(4x2)", svd, 4, 2, minWidth: 60.0f);
        ok &= Measure("bandage(1x1)", bandage, 1, 1, minWidth: 30.0f);
        GetTree().Quit(ok ? 0 : 1);
    }

    private bool Measure(string label, ItemData data, int gw, int gh, float minWidth)
    {
        const float cell = 40.0f;
        var block = new GridItemBlock
        {
            Stack = new ItemStack(data, 1),
            Size = new Vector2(gw * cell, gh * cell),
        };
        AddChild(block);

        TextureRect icon = null;
        foreach (var child in block.GetChildren())
            if (child is TextureRect tr) icon = tr;

        if (icon == null)
        {
            GD.PrintErr(label, ": 找不到图标节点");
            return false;
        }

        var src = data.Icon?.GetSize() ?? Vector2.Zero;
        GD.Print($"{label} 源图={src} 图标框={icon.Size} 比例={(src.Y > 0 ? src.X / src.Y : 0):F2}");
        bool ok = icon.Size.X >= minWidth;
        if (!ok) GD.PrintErr($"{label}: 图标宽度 {icon.Size.X} 小于期望 {minWidth}");
        return ok;
    }
}
