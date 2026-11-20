using Godot;
using System.Collections.Generic;

/// <summary>
/// 野外地图（模仿 ZS 的露天场景）：大片草地 + 土路 + 几间房子 + 树。
///
/// 分工：
///   · 这里只决定**每格是什么地形**，以及**树长在哪几格**（<see cref="Trees"/>）；
///   · 地形贴图由 <c>Game</c> 按图集铺；树由 <c>WorldProps</c> 变成实体（带碰撞）。
///
/// 尺寸 60×40 格 × 32px = 1920×1280 世界单位，原点居中，与 <c>Game.WorldBounds</c> 一致。
/// </summary>
public static class MapLayout
{
    /// <summary>地图宽（格）。</summary>
    public const int Width = 60;

    /// <summary>地图高（格）。</summary>
    public const int Height = 40;

    /// <summary>地形种类。顺序与图集格子的顺序一致（见 <see cref="AtlasIndex"/>）。</summary>
    public enum Terrain
    {
        Street = 0,        // 沥青路
        Dirt = 1,          // 泥土
        Grass = 2,         // 草地（野外主色）
        WoodFloor = 3,     // 木地板
        TiledFloor = 4,    // 瓷砖地面
        BrickWall = 5,     // 砖墙（实心）
        ConcreteWall = 6,  // 混凝土墙（实心）
        Rubble = 7,        // 瓦砾
    }

    /// <summary>图集里第几格（行）。</summary>
    public static int AtlasIndex(Terrain terrain) => (int)terrain;

    /// <summary>是不是实心（挡人、挡子弹、挡寻路）。</summary>
    public static bool IsSolid(Terrain terrain)
        => terrain is Terrain.BrickWall or Terrain.ConcreteWall;

    /// <summary>树的位置（格坐标）。由 <c>WorldProps</c> 换成实体。</summary>
    public static readonly List<Vector2I> Trees = new();

    /// <summary>生成整张地图。</summary>
    public static Terrain[,] Build()
    {
        var map = new Terrain[Width, Height];

        // 野外底：全是草地
        Fill(map, 0, 0, Width, Height, Terrain.Grass);

        // 泥土小径（把三间房和主干连起来）
        Fill(map, 0, 18, Width, 3, Terrain.Dirt);      // 横向主路
        Fill(map, 28, 0, 3, Height, Terrain.Dirt);     // 纵向主路
        Fill(map, 12, 21, 3, 12, Terrain.Dirt);        // 通往南边房子
        Fill(map, 40, 8, 3, 12, Terrain.Dirt);         // 通往北边房子

        // 三间房（ZS 的野外也有零散民房）
        BuildRoom(map, 6, 5, 12, 8, Terrain.WoodFloor, doorOnTop: false);
        BuildRoom(map, 41, 6, 13, 9, Terrain.TiledFloor, doorOnTop: false);
        BuildRoom(map, 34, 24, 12, 8, Terrain.WoodFloor, doorOnTop: true);

        // 零散瓦砾（废墟感）
        Fill(map, 20, 6, 3, 3, Terrain.Rubble);
        Fill(map, 47, 28, 4, 3, Terrain.Rubble);
        Fill(map, 8, 30, 3, 4, Terrain.Rubble);

        // 一圈矮墙当地图边界
        Frame(map, 1, Terrain.ConcreteWall);

        BuildTrees(map);
        return map;
    }

    /// <summary>
    /// 撒树：只长在草地上、周围一圈都是草地、彼此不挨着；固定种子 → 每次进游戏一致。
    /// 位置同时记进 <see cref="Trees"/> 给 <c>WorldProps</c> 建实体。
    /// </summary>
    private static void BuildTrees(Terrain[,] map)
    {
        Trees.Clear();
        var rng = new RandomNumberGenerator { Seed = 20260115 };

        for (int y = 2; y < Height - 2; y++)
        {
            for (int x = 2; x < Width - 2; x++)
            {
                if (map[x, y] != Terrain.Grass) continue;
                if (!AllGrassAround(map, x, y)) continue;
                if (rng.Randf() > 0.10f) continue;
                if (TooCloseToTree(x, y)) continue;

                Trees.Add(new Vector2I(x, y));
            }
        }
    }

    private static bool AllGrassAround(Terrain[,] map, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (map[x + dx, y + dy] != Terrain.Grass) return false;
        return true;
    }

    private static bool TooCloseToTree(int x, int y)
    {
        foreach (var t in Trees)
            if (Mathf.Abs(t.X - x) <= 1 && Mathf.Abs(t.Y - y) <= 1) return true;
        return false;
    }

    private static void Fill(Terrain[,] map, int x, int y, int w, int h, Terrain terrain)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            if (yy < 0 || yy >= Height) continue;
            for (int xx = x; xx < x + w; xx++)
            {
                if (xx < 0 || xx >= Width) continue;
                map[xx, yy] = terrain;
            }
        }
    }

    private static void Frame(Terrain[,] map, int thickness, Terrain terrain)
    {
        Fill(map, 0, 0, Width, thickness, terrain);
        Fill(map, 0, Height - thickness, Width, thickness, terrain);
        Fill(map, 0, 0, thickness, Height, terrain);
        Fill(map, Width - thickness, 0, thickness, Height, terrain);
    }

    /// <summary>画一间房：铺地板 → 四周砌墙 → 在朝路那侧开一个两格宽的门。</summary>
    private static void BuildRoom(Terrain[,] map, int x, int y, int w, int h, Terrain floor, bool doorOnTop)
    {
        Fill(map, x, y, w, h, floor);

        Fill(map, x, y, w, 1, Terrain.BrickWall);
        Fill(map, x, y + h - 1, w, 1, Terrain.BrickWall);
        Fill(map, x, y, 1, h, Terrain.BrickWall);
        Fill(map, x + w - 1, y, 1, h, Terrain.BrickWall);

        int doorX = x + w / 2 - 1;
        Fill(map, doorX, doorOnTop ? y : y + h - 1, 2, 1, floor);
    }

    /// <summary>
    /// 4 位邻居掩码：上 1 右 2 下 4 左 8。**同种地形**（或同为实心墙）才算连通。
    /// </summary>
    public static int MaskAt(Terrain[,] map, int x, int y)
    {
        if (map == null) return 0;

        var self = map[x, y];
        int mask = 0;

        if (SameAs(map, x, y - 1, self)) mask |= 1;
        if (SameAs(map, x + 1, y, self)) mask |= 2;
        if (SameAs(map, x, y + 1, self)) mask |= 4;
        if (SameAs(map, x - 1, y, self)) mask |= 8;
        return mask;
    }

    private static bool SameAs(Terrain[,] map, int x, int y, Terrain self)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;

        var other = map[x, y];
        if (other == self) return true;
        return IsSolid(other) && IsSolid(self);
    }

    public static Vector2I AtlasCoords(Terrain[,] map, int x, int y)
        => new Vector2I(MaskAt(map, x, y), (int)map[x, y]);
}
