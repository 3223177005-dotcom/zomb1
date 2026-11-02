using Godot;

/// <summary>
/// 网格寻路管理器（AStarGrid2D）。
/// 每个地图加载时调用 Setup() 配置网格并标记障碍物；敌人调用 FindPath() 获取路径。
///
/// 格子坐标**统一由 <see cref="MapGrid"/> 提供** —— 寻路与地面物品共用同一套，
/// 避免"两套坐标系错位"。
/// </summary>
public partial class NavigationManager : Node
{
    public static NavigationManager Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    private AStarGrid2D _grid;

    /// <summary>统一的地图格子（寻路与地面物品共用同一套）。</summary>
    public MapGrid Grid { get; private set; }

    /// <summary>配置寻路网格。</summary>
    public void Setup(MapGrid grid)
    {
        Grid = grid;

        _grid = new AStarGrid2D
        {
            CellSize = new Vector2(grid.CellSize, grid.CellSize),
            Offset = grid.Origin,
            Region = new Rect2I(0, 0, grid.Size.X, grid.Size.Y),
            DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles
        };
        _grid.Update();
    }

    /// <summary>把一块世界矩形区域标记为不可走（障碍物）。区域外的部分自动忽略。</summary>
    public void MarkRectSolid(Rect2 rect)
    {
        if (_grid == null || Grid == null) return;

        var start = WorldToCell(rect.Position);
        var end = WorldToCell(rect.End);

        for (int x = start.X; x <= end.X; x++)
        {
            for (int y = start.Y; y <= end.Y; y++)
            {
                var cell = new Vector2I(x, y);
                if (Grid.Contains(cell))
                    _grid.SetPointSolid(cell, true);
            }
        }
    }

    /// <summary>
    /// 返回从 from 到 to 的世界坐标路径（无路径时返回空数组）。
    ///
    /// 起点 / 终点若落在障碍格内（典型情况：敌人被物理挤到紧贴墙壁，
    /// 而墙壁所在格子已被标为障碍），A* 会直接失败。这时就近取一个可通行格，
    /// 否则敌人会退化成「直线撞墙」，永远绕不过障碍物。
    /// </summary>
    public Vector2[] FindPath(Vector2 from, Vector2 to)
    {
        if (_grid == null || Grid == null) return System.Array.Empty<Vector2>();

        // 区域外的坐标先夹回区域内，再就近找可通行格
        var startCell = FindNearestWalkable(Grid.Clamp(WorldToCell(from)));
        var endCell = FindNearestWalkable(Grid.Clamp(WorldToCell(to)));
        if (startCell.X < 0 || endCell.X < 0 || startCell == endCell)
            return System.Array.Empty<Vector2>();

        var idPath = _grid.GetIdPath(startCell, endCell);
        var path = new Vector2[idPath.Count];
        for (int i = 0; i < idPath.Count; i++)
            path[i] = _grid.GetPointPosition(idPath[i]);
        return path;
    }

    /// <summary>从给定格子向外逐圈查找最近的可通行格子；找不到返回 (-1,-1)。</summary>
    private Vector2I FindNearestWalkable(Vector2I cell)
    {
        if (IsWalkable(cell)) return cell;

        const int maxRadius = 8;   // 最多向外找 8 格（= 128px）
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;   // 只看当前这一圈

                    var candidate = new Vector2I(cell.X + dx, cell.Y + dy);
                    if (IsWalkable(candidate)) return candidate;
                }
            }
        }
        return new Vector2I(-1, -1);
    }

    private bool IsWalkable(Vector2I cell)
    {
        if (_grid == null || Grid == null) return false;
        if (!Grid.Contains(cell)) return false;
        return !_grid.IsPointSolid(cell);
    }

    public bool IsReady() => _grid != null;

    /// <summary>
    /// 把**一格**标记为不可走。地图按 tile 生成时用这个 ——
    /// 比 <see cref="MarkRectSolid"/> 精确（那个按世界矩形算，边界容易多标一圈）。
    /// </summary>
    public void MarkCellSolid(Vector2I cell)
    {
        if (_grid == null || Grid == null) return;
        if (Grid.Contains(cell)) _grid.SetPointSolid(cell, true);
    }

    /// <summary>世界坐标 → 格子坐标（与地面物品共用同一套格子）。</summary>
    public Vector2I WorldToCell(Vector2 pos)
        => Grid != null ? Grid.WorldToCell(pos) : Vector2I.Zero;
}
