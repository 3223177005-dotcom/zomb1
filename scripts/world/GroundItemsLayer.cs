using Godot;

/// <summary>
/// 地面战利品的可视化：把**有物品的格子**用另一种颜色标出来。
///
/// 现在物品还没有实体图标，所以先用「整格染色 + 描边」表示这里地上有东西；
/// 将来做了掉落物精灵后，这个节点可以退化成只画高亮框。
///
/// 当前「朝向优先」选中的那一格会有更亮的描边。
/// </summary>
public partial class GroundItemsLayer : Node2D
{
    /// <summary>有战利品的格子的填充色。</summary>
    [Export] public Color CellFill { get; set; } = new Color(0.95f, 0.78f, 0.30f, 0.16f);

    /// <summary>有战利品的格子的描边色。</summary>
    [Export] public Color CellBorder { get; set; } = new Color(0.95f, 0.78f, 0.30f, 0.55f);

    /// <summary>当前选中格的描边色。</summary>
    [Export] public Color TargetBorder { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.90f);

    /// <summary>**战利品容器（尸体）**的填充色 —— 与露天物品的橙黄色**明显不同**。</summary>
    [Export] public Color LootSpotFill { get; set; } = new Color(0.66f, 0.32f, 0.92f, 0.0f);

    /// <summary>战利品容器的描边色。</summary>
    [Export] public Color LootSpotBorder { get; set; } = new Color(0.80f, 0.46f, 1.0f, 0.0f);

    /// <summary>**已搜过**的战利品容器的填充色（暗灰，一眼看出"这具已经翻过了"）。</summary>
    [Export] public Color SearchedFill { get; set; } = new Color(0.42f, 0.42f, 0.46f, 0.0f);

    /// <summary>**已搜过**的战利品容器的描边色。</summary>
    [Export] public Color SearchedBorder { get; set; } = new Color(0.48f, 0.48f, 0.53f, 0.0f);

    private Game _game;
    private Player _player;

    public override void _Process(double delta)
    {
        if (_game == null) _game = GetTree().GetFirstNodeInGroup("game") as Game;
        if (_player == null) _player = GetTree().GetFirstNodeInGroup("player") as Player;

        QueueRedraw();   // 有物品的格子很少，每帧重画代价可忽略
    }

    public override void _Draw()
    {
        if (_game?.Items == null || _game.Grid == null) return;

        var grid = _game.Grid;
        var cellSize = new Vector2(grid.CellSize, grid.CellSize);
        var target = _player != null ? _player.FindPickupTargetCell(_game.Items) : new Vector2I(-1, -1);

        // ① 露天物品：橙黄
        foreach (var cell in _game.Items.OccupiedCells)
        {
            var rect = new Rect2(grid.CellToWorld(cell), cellSize);
            bool isTarget = cell == target;

            DrawRect(rect, CellFill);
            DrawRect(rect, isTarget ? TargetBorder : CellBorder, false, isTarget ? 2.0f : 1.0f);
        }

        // ② 战利品容器（尸体）：紫色 —— **和露天物品一眼区分开**；
        //    搜过之后整格转成暗灰，表示"这具已经翻过了"
        foreach (var cell in _game.Items.LootSpotCells)
        {
            var rect = new Rect2(grid.CellToWorld(cell), cellSize);
            bool isTarget = cell == target;
            var spot = _game.Items.GetLootSpot(cell);
            bool searched = spot != null && spot.Searched;

            var fill = searched ? SearchedFill : LootSpotFill;
            var border = searched ? SearchedBorder : LootSpotBorder;

            DrawRect(rect, fill);
            DrawRect(rect, isTarget ? TargetBorder : border, false, isTarget ? 2.0f : 1.0f);

            // 中间画一个小十字，看起来像"尸体标记"（搜过之后跟着变灰）
            var centre = rect.Position + cellSize * 0.5f;
            float arm = Mathf.Min(cellSize.X, cellSize.Y) * 0.22f;
            var mark = searched ? SearchedBorder : LootSpotBorder;

            // 十字已去掉（箱子 / 尸体都有贴图了，不再需要格子标记）
            _ = mark;
        }
    }
}
