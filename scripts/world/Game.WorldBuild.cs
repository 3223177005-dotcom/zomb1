using Godot;

/// <summary>
/// <see cref="Game"/> 的世界搭建辅助（寻路标记、临时宝箱）。
/// 从 Game.cs 拆出来只为守住 400 行上限（AGENTS 规则 1），逻辑未改。
/// </summary>
public partial class Game
{

    /// <summary>
    /// 在**四栋房间里各放一个箱子**。箱子是战利品容器（和尸体同类），区别是：
    /// **地图加载时就生成好**（不靠谁死），而且只出**金 / 红**两档战利品。
    /// </summary>
    private void SpawnChests()
    {
        // 每个房间里挑一个角落格（避开门口和墙）
        var chestCells = new[]
        {
            new Vector2I(5, 5),     // 左上（木地板）
            new Vector2I(42, 5),    // 右上（瓷砖）
            new Vector2I(5, 26),    // 左下（木地板）
            new Vector2I(42, 26),   // 右下（瓷砖）
        };

        for (int i = 0; i < chestCells.Length; i++)
        {
            var cell = chestCells[i];
            if (!Grid.Contains(cell)) continue;

            var items = ZombieLoot.RollChest();
            if (items.Count == 0) continue;

            Items.SpawnLootSpot(cell, items);
            AddChestSprite(cell, i);
        }
    }

    /// <summary>把实心格标给寻路。</summary>
    private void MarkWalkable()
    {
        for (int y = 0; y < MapLayout.Height; y++)
            for (int x = 0; x < MapLayout.Width; x++)
                if (MapLayout.IsSolid(Map[x, y]))
                    NavigationManager.Instance.MarkCellSolid(new Vector2I(x, y));
    }

    /// <summary>用代码生成 TileSet，并把地图铺到 <see cref="TileMapLayer"/> 上。</summary>
    /// <summary>地图上的实体装饰（树）；插在地形之后、玩家之前。</summary>
    private void BuildProps()
    {
        var props = new WorldProps { Name = "WorldProps" };
        AddChild(props);
        MoveChild(props, Terrain.GetIndex() + 2);   // 地形 → (贴花) → 树 → 玩家
        props.Build(WorldBounds.Position, CellSize, seed: 20260115);

        // 树干格给**寻路**标实心：丧尸不再朝树里直线撞。
        // 只动 AStarGrid2D，地表 tile 与物理碰撞都不变（玩家照样能贴着树干走）。
        foreach (var cell in props.TrunkCells)
            NavigationManager.Instance.MarkCellSolid(cell);
    }

    /// <summary>地面裂缝贴花（单独一层，图集里做不了随机）。</summary>
    private void BuildFloorDecals()
    {
        var decals = new FloorDecals { Name = "FloorDecals" };
        AddChild(decals);
        // ★ 必须排在地形之后、角色之前：它是整张 1536×1024 的画布，
        //   放到末尾会把角色/僵尸/掉落全盖住（同级 ZIndex 下按树顺序绘制）。
        MoveChild(decals, Terrain.GetIndex() + 1);
        decals.Build(Map, WorldBounds.Position, CellSize);
    }
}
