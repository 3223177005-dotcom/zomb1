using Godot;

/// <summary>
/// 统一的地图格子坐标系 —— 世界坐标 ⇄ 格子坐标的**唯一转换点**。
///
/// 现在自己算（原点 + 格子边长）；将来接入 <c>TileMapLayer</c> 之后，
/// 只要把内部实现换成 <c>TileMapLayer.LocalToMap / MapToLocal</c>，
/// **所有调用方都不用改**。
///
/// 约定：
/// - 格子坐标用 <see cref="Vector2I"/>，原点格 (0,0) 的左上角位于 <see cref="Origin"/>;
/// - 对外**不 clamp** —— 区域外的坐标原样返回，由调用方用 <see cref="Contains"/> 判断。
///   （这一点很重要：clamp 会把区域外的格子全部塌陷到边缘。）
/// </summary>
public class MapGrid
{
    public MapGrid(Vector2 origin, float cellSize, Vector2I size)
    {
        Origin = origin;
        CellSize = Mathf.Max(1.0f, cellSize);
        Size = new Vector2I(Mathf.Max(1, size.X), Mathf.Max(1, size.Y));
    }

    /// <summary>格子边长（像素）。</summary>
    public float CellSize { get; }

    /// <summary>格子 (0,0) 左上角的世界坐标。</summary>
    public Vector2 Origin { get; }

    /// <summary>区域大小（格数）。</summary>
    public Vector2I Size { get; }

    /// <summary>区域的世界矩形。</summary>
    public Rect2 WorldBounds
        => new Rect2(Origin, new Vector2(Size.X * CellSize, Size.Y * CellSize));

    /// <summary>世界坐标 → 格子坐标。区域外也照算，**不 clamp**。</summary>
    public Vector2I WorldToCell(Vector2 world) => new Vector2I(
        Mathf.FloorToInt((world.X - Origin.X) / CellSize),
        Mathf.FloorToInt((world.Y - Origin.Y) / CellSize));

    /// <summary>格子坐标 → 该格**左上角**的世界坐标。</summary>
    public Vector2 CellToWorld(Vector2I cell) => new Vector2(
        Origin.X + cell.X * CellSize,
        Origin.Y + cell.Y * CellSize);

    /// <summary>格子坐标 → 该格**中心**的世界坐标（放东西 / 画东西用中心更自然）。</summary>
    public Vector2 CellCenter(Vector2I cell) => CellToWorld(cell) + new Vector2(CellSize, CellSize) * 0.5f;

    /// <summary>该格是否落在区域内。</summary>
    public bool Contains(Vector2I cell)
        => cell.X >= 0 && cell.Y >= 0 && cell.X < Size.X && cell.Y < Size.Y;

    /// <summary>把格子坐标夹到区域内。</summary>
    public Vector2I Clamp(Vector2I cell) => new Vector2I(
        Mathf.Clamp(cell.X, 0, Size.X - 1),
        Mathf.Clamp(cell.Y, 0, Size.Y - 1));

    public override string ToString()
        => $"格子 {CellSize:0.#}px  原点 {Origin}  区域 {Size.X}×{Size.Y}";
}
