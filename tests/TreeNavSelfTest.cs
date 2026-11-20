using Godot;

/// <summary>
/// 树干格的**寻路**回归测试：AI 不许朝树里直线撞。
///
/// 直接跑**真实 `game.tscn`**（不复刻接线），这样「`Game` 忘了把树干格交给寻路」也会被抓到。
///
/// 规则：树干那格标成 AStarGrid2D 实心，但**地表 tile 与物理碰撞都不动** ——
/// 玩家照样能贴着树干走（物理挡的是 14×10 的树干碰撞体，不是整格）。
///
/// 断言：
/// 1. 每棵树的树干格都是寻路实心；
/// 2. 只多标了树干格（总数 = 地形实心格 + 不在实心地形上的树干格）；
/// 3. 一块普通草地格**没有**被误标；
/// 4. 跨过树的寻路会**绕开**树干格。
///
/// Godot --headless --path . res://tests/TreeNavSelfTest.tscn
/// </summary>
public partial class TreeNavSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override async void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://scenes/game.tscn");
        if (scene == null)
        {
            GD.PrintErr("加载 game.tscn 失败");
            GetTree().Quit(1);
            return;
        }

        var root = scene.Instantiate();
        AddChild(root);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (root is not Game game || game.Map == null || game.Grid == null)
        {
            GD.PrintErr("game.tscn 没搭好地图");
            GetTree().Quit(1);
            return;
        }

        var props = FindProps(root);
        if (props == null || props.TrunkCells.Count == 0)
        {
            Check("种出了树", false, props == null ? "场景里没有 WorldProps" : "TrunkCells 为空");
            Finish();
            return;
        }

        var map = game.Map;
        var grid = game.Grid;
        Check("种出了树", true, $"{props.TrunkCells.Count} 棵");

        // ① 树干格全标实心
        int blockedTrunks = 0;
        foreach (var cell in props.TrunkCells)
            if (NavigationManager.Instance.IsCellBlocked(cell)) blockedTrunks++;
        Check("树干格都标成寻路实心", blockedTrunks == props.TrunkCells.Count,
            $"{blockedTrunks}/{props.TrunkCells.Count}");

        // ② 只多标了树干格（没有连整片草地一起标掉）
        int solidTerrain = 0;
        for (int y = 0; y < MapLayout.Height; y++)
            for (int x = 0; x < MapLayout.Width; x++)
                if (MapLayout.IsSolid(map[x, y])) solidTerrain++;

        int treeExtra = 0;
        foreach (var cell in props.TrunkCells)
            if (!MapLayout.IsSolid(map[cell.X, cell.Y])) treeExtra++;

        int totalBlocked = 0;
        for (int y = 0; y < MapLayout.Height; y++)
            for (int x = 0; x < MapLayout.Width; x++)
                if (NavigationManager.Instance.IsCellBlocked(new Vector2I(x, y))) totalBlocked++;

        int expected = solidTerrain + treeExtra;
        Check("只多标了树干格", totalBlocked == expected,
            $"实标 {totalBlocked} ≠ 期望 {expected}（地形 {solidTerrain} + 树干 {treeExtra}）");

        // ③ 普通草地格没被误标
        var grass = FindFreeCell(map);
        Check("普通草地格仍可走", grass.HasValue && !NavigationManager.Instance.IsCellBlocked(grass.Value),
            grass.HasValue ? $"格 {grass.Value} 被误标" : "找不到草地格");

        // ④ 跨树的路径必须绕开树干格
        CheckPathDetours(props, grid);

        Finish();
    }

    private static WorldProps FindProps(Node root)
    {
        if (root is WorldProps props) return props;
        foreach (var child in root.GetChildren())
        {
            var found = FindProps(child);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>找一块非实心地形、又不是树干的格子。</summary>
    private static Vector2I? FindFreeCell(MapLayout.Terrain[,] map)
    {
        for (int y = 2; y < MapLayout.Height - 2; y++)
            for (int x = 2; x < MapLayout.Width - 2; x++)
            {
                if (MapLayout.IsSolid(map[x, y])) continue;
                var cell = new Vector2I(x, y);
                if (MapLayout.Trees.Contains(cell)) continue;
                return cell;
            }
        return null;
    }

    /// <summary>从树左边 3 格走到右边 3 格，路径里不许出现树干格。</summary>
    private void CheckPathDetours(WorldProps props, MapGrid grid)
    {
        foreach (var trunk in props.TrunkCells)
        {
            var from = trunk + new Vector2I(-3, 0);
            var to = trunk + new Vector2I(3, 0);

            if (!grid.Contains(from) || !grid.Contains(to)) continue;
            if (NavigationManager.Instance.IsCellBlocked(from)) continue;
            if (NavigationManager.Instance.IsCellBlocked(to)) continue;

            var path = NavigationManager.Instance.FindPath(grid.CellCenter(from), grid.CellCenter(to));
            if (path.Length == 0) continue;

            bool crosses = false;
            foreach (var point in path)
                if (grid.WorldToCell(point) == trunk) { crosses = true; break; }

            Check($"寻路绕开树干 {trunk}", !crosses, "路径穿过了树干格");
            return;   // 测通一棵就够，不必遍历 76 棵
        }

        Check("找到可测的树", false, "没有左右各 3 格都空着的树");
    }

    private void Finish()
    {
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void Check(string name, bool ok, string detail)
    {
        if (ok) _pass++;
        else _fail++;
        GD.Print(ok ? $"  [PASS] {name}" : $"  [FAIL] {name}  （{detail}）");
    }
}
