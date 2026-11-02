using Godot;

/// <summary>准星：跟随鼠标，间隙随玩家散布平滑变化。</summary>
public partial class Crosshair : Control
{
    [Export] public float BaseGap { get; set; } = 6.0f;        // 零散布时的基础间隙（像素）
    [Export] public float GapPerDegree { get; set; } = 2.4f;   // 每度散布对应的间隙像素
    [Export] public float LineLength { get; set; } = 14.0f;
    [Export] public float LineThickness { get; set; } = 3.0f;

    private Player _player = null;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(128, 128);
    }

    public override void _Process(double delta)
    {
        if (_player == null)
            _player = GetTree().GetFirstNodeInGroup("player") as Player;

        // 跟随鼠标（居中）
        var mouse = GetViewport().GetMousePosition();
        Position = mouse - Size * 0.5f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = new Color(1, 1, 1, 0.95f);
        var center = Size * 0.5f;
        float gap = BaseGap;
        if (_player != null)
            gap = BaseGap + _player.CurrentSpread * GapPerDegree;

        // 上/下/左/右四段线
        DrawLine(center + new Vector2(gap, 0), center + new Vector2(gap + LineLength, 0), c, LineThickness);
        DrawLine(center - new Vector2(gap, 0), center - new Vector2(gap + LineLength, 0), c, LineThickness);
        DrawLine(center + new Vector2(0, gap), center + new Vector2(0, gap + LineLength), c, LineThickness);
        DrawLine(center - new Vector2(0, gap), center - new Vector2(0, gap + LineLength), c, LineThickness);
        // 中心点
        DrawCircle(center, 2.4f, c);
    }
}
