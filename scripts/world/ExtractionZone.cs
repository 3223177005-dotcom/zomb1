using Godot;

/// <summary>
/// **撤离点**：玩家站在区域里达到 <see cref="RequiredSeconds"/> 秒就算撤离成功。
///
/// 目前是最简框架：
/// - 一块矩形区域（世界坐标）
/// - 站在里面累积时间，离开则**清零**（要站着不动才算）
/// - 满了就把 <see cref="Completed"/> 置真，由 <see cref="Game"/> 弹结算面板
/// </summary>
public partial class ExtractionZone : Node2D
{
    /// <summary>区域（世界坐标）。</summary>
    public Rect2 Area { get; set; }

    /// <summary>需要站多久（秒）。</summary>
    public float RequiredSeconds { get; set; } = 5.0f;

    /// <summary>已经站了多久（秒）。</summary>
    public float Elapsed { get; private set; }

    /// <summary>是否已完成撤离。</summary>
    public bool Completed { get; private set; }

    /// <summary>进度 0~1。</summary>
    public float Progress => RequiredSeconds <= 0.0f ? 1.0f : Mathf.Clamp(Elapsed / RequiredSeconds, 0.0f, 1.0f);

    /// <summary>玩家是不是在区域里。</summary>
    public bool Contains(Vector2 worldPosition) => Area.HasPoint(worldPosition);

    /// <summary>重置（重新开始一局时用）。</summary>
    public void Reset()
    {
        Elapsed = 0.0f;
        Completed = false;
        QueueRedraw();
    }

    /// <summary>每帧推进。返回 true 表示**这一帧刚刚完成**撤离。</summary>
    public bool Tick(float delta, bool playerInside)
    {
        if (Completed) return false;

        if (playerInside)
        {
            Elapsed += delta;
            if (Elapsed >= RequiredSeconds)
            {
                Elapsed = RequiredSeconds;
                Completed = true;
                QueueRedraw();
                return true;
            }
        }
        else if (Elapsed > 0.0f)
        {
            Elapsed = 0.0f;      // 离开就清零 —— 得老老实实站着
        }

        QueueRedraw();
        return false;
    }

    public override void _Draw()
    {
        // 底色：越接近完成越绿
        var fill = new Color(0.25f, 0.85f, 0.45f, 0.10f + 0.22f * Progress);
        DrawRect(Area, fill);
        DrawRect(Area, new Color(0.35f, 0.95f, 0.55f, 0.85f), false, 2.0f);

        // 内部进度条（贴底）
        if (Progress > 0.0f)
        {
            var bar = new Rect2(Area.Position, new Vector2(Area.Size.X * Progress, 4.0f));
            DrawRect(bar, new Color(0.40f, 1.0f, 0.60f, 0.95f));
        }

        // 四角标记，远看也能找到撤离点
        const float arm = 10.0f;
        var c = new Color(0.45f, 1.0f, 0.65f, 1.0f);
        DrawLine(Area.Position, Area.Position + new Vector2(arm, 0), c, 3.0f);
        DrawLine(Area.Position, Area.Position + new Vector2(0, arm), c, 3.0f);
        DrawLine(Area.End, Area.End - new Vector2(arm, 0), c, 3.0f);
        DrawLine(Area.End, Area.End - new Vector2(0, arm), c, 3.0f);
    }
}
