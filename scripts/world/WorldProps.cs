using Godot;
using System.Collections.Generic;

/// <summary>
/// 地图上的实体装饰：树（ZS 的 <c>s_tree*</c> 贴图）+ 碰撞。
///
/// 为什么单独一层：地形是 TileMapLayer，树是**会挡路的物件**（有碰撞、要压住玩家），
/// 塞进图集就只能当贴图、挡不住人。
///
/// 层级：排在 <c>Terrain</c> 之后、玩家之前（同级 ZIndex 下按树顺序绘制）。
///
/// ★ 尺寸约定：`assets/sprites/objects32/zs/s_tree*.png` 是 **32×80 原生**素材，
///   直接 1:1 显示（AGENTS 规则 7 的 ×2 只针对 16px 素材，这里不适用）。
/// ★ 碰撞约定：树干碰撞体挂在**本层**（不继承任何精灵缩放），
///   落在「格子底部中心、树干那么宽」的位置 —— 树冠不挡路，树干才挡。
/// </summary>
public partial class WorldProps : Node2D
{
    /// <summary>树贴图（ZS 原图，32×80 原生 → 1:1）。</summary>
    private static readonly string[] TreeArt =
    {
        "s_tree", "s_tree", "s_tree_dead", "s_tree_pine_dead",   // 活树权重高一些，死树点缀
    };

    private const string TreeDir = "res://assets/sprites/objects32/zs/";

    /// <summary>
    /// 树干碰撞宽度（像素）。按贴图实测取 12 —— `s_tree*.png` 底部 14 行的不透明像素是
    /// x∈[10,21]（宽 11~12），再宽就是空气墙。
    /// </summary>
    private const float TrunkWidth = 12.0f;

    /// <summary>树干碰撞高度（像素）。只挡根部，树冠是"能走过去"的。</summary>
    private const float TrunkHeight = 10.0f;

    private readonly List<Vector2I> _trunkCells = new();

    /// <summary>
    /// 树干占的格子（每棵一个，与碰撞体一一对应）。
    /// 由 <c>Game</c> 在种完树后交给 <see cref="NavigationManager"/> 标成寻路实心 ——
    /// 只影响 AI 寻路，**不影响地表渲染与玩家物理**（玩家仍能贴着树干走）。
    /// </summary>
    public IReadOnlyList<Vector2I> TrunkCells => _trunkCells;

    /// <summary>按 <see cref="MapLayout.Trees"/> 建树（由 <c>Game</c> 在地形铺好后调用）。</summary>
    public void Build(Vector2 origin, float cell, int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        int placed = 0;

        foreach (var grid in MapLayout.Trees)
        {
            string name = TreeArt[rng.RandiRange(0, TreeArt.Length - 1)];
            var tex = GD.Load<Texture2D>(TreeDir + name + ".png");
            if (tex == null) continue;

            var cellTopLeft = origin + new Vector2(grid.X * cell, grid.Y * cell);
            var size = tex.GetSize();

            // 贴图：底边贴格子底边、水平居中于格子（Centered=false 时 Position 就是左上角）
            var sprite = new Sprite2D
            {
                Name = $"Tree_{grid.X}_{grid.Y}",
                Texture = tex,
                Centered = false,
                Position = cellTopLeft + new Vector2((cell - size.X) * 0.5f, cell - size.Y),
            };
            sprite.FlipH = rng.Randf() < 0.5f;
            AddChild(sprite);

            AddTrunk(cellTopLeft, cell, grid);
            _trunkCells.Add(grid);
            placed++;
        }

        GD.Print($"[WorldProps] 种树 {placed} 棵");
    }

    /// <summary>
    /// 树干碰撞：挂在**道具层**而不是精灵下面 ——
    /// 挂在精灵下会连带吃它的缩放，半径与圆心一起被放大、位置偏出贴图。
    /// </summary>
    private void AddTrunk(Vector2 cellTopLeft, float cell, Vector2I grid)
    {
        var body = new StaticBody2D
        {
            Name = $"Trunk_{grid.X}_{grid.Y}",
            CollisionLayer = 1,
            CollisionMask = 0,
            Position = cellTopLeft + new Vector2(cell * 0.5f, cell - TrunkHeight * 0.5f),
        };

        body.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(TrunkWidth, TrunkHeight) },
        });

        AddChild(body);
    }
}
