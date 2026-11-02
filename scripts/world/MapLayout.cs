using Godot;
/// <summary>
/// Demo 地图的**布局定义**（纯数据，不碰节点）。
///
/// 48×32 格，每格 **32px**（D2：世界格从 16 升到 32）= **1536×1024 世界单位**，原点居中在 (-768, -512)。
/// 布局：一幅十字街道 + 四栋**可进入**的建筑（砖墙包围、木地板 / 瓷砖地面、各开一个门）。
///
/// 用矩形定义而不是 ASCII 字符画 —— 少打错、好调。
/// 将来要接真正的关卡编辑，把 <see cref="Build"/> 换成读文件即可。
/// </summary>
public static class MapLayout
{
    /// <summary>地图宽（格）。</summary>
    public const int Width = 48;

    /// <summary>地图高（格）。</summary>
    public const int Height = 32;

    /// <summary>地形种类。顺序与图集格子的顺序一致（见 <see cref="AtlasIndex"/>）。</summary>
    public enum Terrain
    {
        Street = 0,        // 柏油路
        Dirt = 1,          // 泥土
        Grass = 2,         // 草地
        WoodFloor = 3,     // 木地板
        TiledFloor = 4,    // 瓷砖地面
        BrickWall = 5,     // 砖墙（实心）
        ConcreteWall = 6,  // 混凝土墙（实心）
        Rubble = 7,        // 瓦砾
    }

    /// <summary>图集里第几格（横排，每格 16×16）。</summary>
    public static int AtlasIndex(Terrain terrain) => (int)terrain;

    /// <summary>是不是实心（挡人、挡子弹、挡寻路）。</summary>
    public static bool IsSolid(Terrain terrain)
        => terrain is Terrain.BrickWall or Terrain.ConcreteWall;

    /// <summary>生成整张地图。</summary>
    public static Terrain[,] Build()
    {
        var map = new Terrain[Width, Height];

        // 底色：泥土
        Fill(map, 0, 0, Width, Height, Terrain.Dirt);

        // 几片草地
        Fill(map, 1, 1, 18, 2, Terrain.Grass);
        Fill(map, 29, 1, 18, 2, Terrain.Grass);
        Fill(map, 1, 12, 18, 2, Terrain.Grass);
        Fill(map, 29, 12, 18, 2, Terrain.Grass);
        Fill(map, 1, 29, 46, 2, Terrain.Grass);

        // 十字街道（竖街 x=21..26，横街 y=14..17）
        Fill(map, 21, 0, 6, Height, Terrain.Street);
        Fill(map, 0, 14, Width, 4, Terrain.Street);

        // 瓦砾堆（掩体感）
        Fill(map, 18, 19, 2, 5, Terrain.Rubble);
        Fill(map, 28, 19, 2, 5, Terrain.Rubble);
        Fill(map, 18, 4, 3, 3, Terrain.Rubble);
        Fill(map, 28, 4, 3, 3, Terrain.Rubble);

        // 四栋可进入的建筑（第三个参数决定门开在上面还是下面）
        BuildRoom(map, 3, 3, 15, 9, Terrain.WoodFloor, doorOnTop: false);    // 左上：木地板
        BuildRoom(map, 30, 3, 15, 9, Terrain.TiledFloor, doorOnTop: false);  // 右上：瓷砖
        BuildRoom(map, 3, 20, 15, 9, Terrain.WoodFloor, doorOnTop: true);    // 左下：木地板
        BuildRoom(map, 30, 20, 15, 9, Terrain.TiledFloor, doorOnTop: true);  // 右下：瓷砖

        // 地图外框（混凝土墙，把玩家关在场内）
        Frame(map, 1, Terrain.ConcreteWall);

        return map;
    }

    // ---------- 绘制工具 ----------

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

    /// <summary>画一间房：铺地板 → 四周围砖墙 → 在朝街那侧开一个两格宽的门。</summary>
    private static void BuildRoom(Terrain[,] map, int x, int y, int w, int h, Terrain floor, bool doorOnTop)
    {
        Fill(map, x, y, w, h, floor);

        Fill(map, x, y, w, 1, Terrain.BrickWall);              // 上
        Fill(map, x, y + h - 1, w, 1, Terrain.BrickWall);      // 下
        Fill(map, x, y, 1, h, Terrain.BrickWall);              // 左
        Fill(map, x + w - 1, y, 1, h, Terrain.BrickWall);      // 右

        int doorX = x + w / 2 - 1;
        Fill(map, doorX, doorOnTop ? y : y + h - 1, 2, 1, floor);
    }

    /// <summary>
    /// 4 位邻居掩码：上=1 右=2 下=4 左=8。**同种地形**（或同为实心墙）才算连通。
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

        // 两种实心墙互相连通，避免砖墙/水泥墙交界多出暗边
        return IsSolid(self) && IsSolid(other);
    }

    /// <summary>
    /// 自动拼接图集坐标：<c>X = 邻居掩码 0..15</c>，<c>Y = 地形序号 0..7</c>。
    /// （图集见 <c>assets/sprites/tiles/terrain_atlas.png</c>，**512x256** = 16 掩码 × 8 地形 × 32px）
    /// </summary>
    public static Vector2I AtlasCoords(Terrain[,] map, int x, int y)
        => new Vector2I(MaskAt(map, x, y), (int)map[x, y]);

}
