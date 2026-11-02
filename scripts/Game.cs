using Godot;
using System.Collections.Generic;

/// <summary>
/// 游戏场景：建立地图（TileMapLayer）、统一格子、寻路网格、地面物品。
///
/// 地图数据来自 <see cref="MapLayout"/>（纯数据），**渲染、碰撞、寻路三者同源** ——
/// 同一个 <c>Terrain[,]</c> 既铺 tile，又生成碰撞矩形，又标寻路实心格，不会漂移。
///
/// 贴图是程序生成的 16×16 像素地形图集（<c>assets/sprites/tiles/terrain_atlas.png</c>），
/// 将来换真美术只要**替换这个文件并保持 16×16 切分**，一行代码都不用改。
/// </summary>
public partial class Game : Node2D
{
    /// <summary>格子边长（像素）。世界坐标与 tile 尺寸必须一致。</summary>
    public const float CellSize = 32.0f;

    /// <summary>地形图集路径。</summary>
    private const string TerrainAtlasPath = "res://assets/sprites/tiles/terrain_atlas.png";

    /// <summary>地图区域（世界坐标）。以原点为中心，所以 (0,0) 正好是地图中央。</summary>
    public static readonly Rect2 WorldBounds = new Rect2(
        -MapLayout.Width * CellSize * 0.5f,
        -MapLayout.Height * CellSize * 0.5f,
        MapLayout.Width * CellSize,
        MapLayout.Height * CellSize);

    /// <summary>统一的地图格子（寻路与地面物品共用）。</summary>
    public MapGrid Grid { get; private set; }

    /// <summary>地面上的物品（含露天物品与战利品容器）。</summary>
    public WorldItems Items { get; private set; }

    /// <summary>地形层（渲染）。</summary>
    public TileMapLayer Terrain { get; private set; }

    /// <summary>地图数据（格子 → 地形）。</summary>
    public MapLayout.Terrain[,] Map { get; private set; }

    public override void _Ready()
    {
        AddToGroup("game");   // 面板等节点用它找到本场景（与 "player" 组的做法一致）

        Grid = new MapGrid(
            WorldBounds.Position,
            CellSize,
            new Vector2I(MapLayout.Width, MapLayout.Height));

        NavigationManager.Instance.Setup(Grid);
        Items = new WorldItems(Grid);

        Map = MapLayout.Build();

        BuildTerrain();
        BuildCollision();
        MarkWalkable();

        // ───────── 以下两块都是【临时测试内容】，正式生成系统做好后整块删掉 ─────────
        SpawnDebugLoot();
        SeedBackpack();
        SpawnChests();
        SetupWorldZoom();
        BuildExtraction();
    }

    /// <summary>撤离点（站在里面 5 秒成功）。</summary>
    public ExtractionZone Extraction { get; private set; }

    private ResultPanel _result;

    /// <summary>放撤离点 + 结算面板。位置选在地图下方那段竖直马路上（室外、好找）。</summary>
    private void BuildExtraction()
    {
        var corner = Grid.CellToWorld(new Vector2I(23, 29));

        Extraction = new ExtractionZone
        {
            Name = "ExtractionZone",
            Area = new Rect2(corner, new Vector2(CellSize * 2.0f, CellSize * 2.0f)),
            RequiredSeconds = 5.0f,
        };
        AddChild(Extraction);

        _result = new ResultPanel { Name = "ResultPanel", Visible = false };
        (_uiLayer as Node ?? this).AddChild(_result);
    }

    public override void _Process(double delta)
    {
        if (Extraction == null || Extraction.Completed || _result == null) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        bool justFinished = Extraction.Tick((float)delta, Extraction.Contains(player.GlobalPosition));
        if (justFinished) _result.Show(player);

        // 搜过的箱子 / 尸体贴图变暗
        foreach (var pair in _lootSprites)
        {
            var sprite = pair.Value;
            if (sprite == null || !GodotObject.IsInstanceValid(sprite)) continue;

            var spot = Items.GetLootSpot(pair.Key);
            bool searched = spot != null && spot.Searched;
            sprite.Modulate = searched ? new Color(0.45f, 0.45f, 0.50f) : Colors.White;
        }
    }

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

    /// <summary>用代码生成 TileSet，并把地图铺到 <see cref="TileMapLayer"/> 上。</summary>
    private void BuildTerrain()
    {
        Terrain = GetNode<TileMapLayer>("Terrain");

        var atlas = GD.Load<Texture2D>(TerrainAtlasPath);
        if (atlas == null)
        {
            GD.PushError($"Game: 找不到地形图集 {TerrainAtlasPath}");
            return;
        }

        var source = new TileSetAtlasSource
        {
            Texture = atlas,
            TextureRegionSize = new Vector2I((int)CellSize, (int)CellSize),
        };

        // 自动拼接图集：16 列（邻居掩码 0..15） x 8 行（地形 0..7）
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 16; col++)
                source.CreateTile(new Vector2I(col, row));

        var tileSet = new TileSet
        {
            TileSize = new Vector2I((int)CellSize, (int)CellSize),
        };
        tileSet.AddSource(source, 0);

        Terrain.TileSet = tileSet;
        Terrain.Position = WorldBounds.Position;   // 让 tile 坐标与世界格子对齐

        for (int y = 0; y < MapLayout.Height; y++)
            for (int x = 0; x < MapLayout.Width; x++)
                Terrain.SetCell(new Vector2I(x, y), 0, MapLayout.AtlasCoords(Map, x, y));
    }

    /// <summary>
    /// 按地图数据生成墙体碰撞：**同一行连续的实心格合并成一个矩形**，
    /// 避免几百个 CollisionShape2D。
    /// </summary>
    private void BuildCollision()
    {
        var body = new StaticBody2D { Name = "MapCollision" };
        body.CollisionLayer = 1;
        body.CollisionMask = 6;
        AddChild(body);

        for (int y = 0; y < MapLayout.Height; y++)
        {
            int x = 0;
            while (x < MapLayout.Width)
            {
                if (!MapLayout.IsSolid(Map[x, y])) { x++; continue; }

                int start = x;
                while (x < MapLayout.Width && MapLayout.IsSolid(Map[x, y])) x++;
                int runWidth = x - start;

                var centre = new Vector2(
                    WorldBounds.Position.X + (start + runWidth * 0.5f) * CellSize,
                    WorldBounds.Position.Y + (y + 0.5f) * CellSize);

                var shape = new CollisionShape2D
                {
                    Shape = new RectangleShape2D { Size = new Vector2(runWidth * CellSize, CellSize) },
                    Position = centre,
                };
                body.AddChild(shape);
            }
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

    /// <summary>某个世界坐标是不是实心（挡人 / 挡子弹）。</summary>
    public bool IsSolidAt(Vector2 world)
    {
        if (Map == null) return false;

        var cell = Grid.WorldToCell(world);
        if (!Grid.Contains(cell)) return true;   // 场外当实心
        return MapLayout.IsSolid(Map[cell.X, cell.Y]);
    }

    /// <summary>
    /// 【临时】出生点四周撒一点测试物资，用来试「按 F 拾取」和「中键查看」。
    ///
    /// - 右边一格：**5 个绷带**（**普通 白**，药品可堆叠 → 合并成 1 叠）→ 按 F **直接捡**
    /// - 左边一格：1 把匕首（**普通 白**）        → 按 F **直接捡**
    /// - 下边一格：1 把 SVD（**稀有 蓝**）        → 中键看名字是蓝色
    /// - 上边一格：**一个装着 1 个绷带的背包**    → 按 F 开**物品树**（容器一定开面板）
    /// </summary>
    private void SpawnDebugLoot()
    {
        var db = ItemDatabase.Instance;
        if (db == null) return;

        var bandage = db.GetItem("bandage");
        var dagger = db.GetItem("dagger");
        var svd = db.GetItem("svd");
        var backpack = db.GetItem("backpack");

        if (bandage != null) Items.Drop(new Vector2(32, 0), new ItemStack(bandage, 5));
        if (dagger != null) Items.Drop(new Vector2(-32, 0), new ItemStack(dagger, 1));
        if (svd != null) Items.Drop(new Vector2(0, 32), new ItemStack(svd, 1));

        // 「装着一个绷带的背包」：地面格 → 背包 → 绷带，两层嵌套
        if (backpack != null && bandage != null)
        {
            var bag = new ItemStack(backpack, 1);
            bag.EnsureContents().Add(new ItemStack(bandage, 1));
            Items.Drop(new Vector2(0, -32), bag);
        }

        // 【临时】三件枪械配件撒在出生点周围，方便验收改枪
        DropAttachment("silencer", new Vector2(32, 0));
        DropAttachment("extmag", new Vector2(-32, 0));
        DropAttachment("tacgrip", new Vector2(0, 32));

        // 【临时】SVD 口径的弹药
        var ammo = db.GetItem("ammo_762x54r");
        if (ammo != null) Items.Drop(new Vector2(64, 32), new ItemStack(ammo, 30));
    }

    /// <summary>【临时】把一个配件丢到地上。</summary>
    private void DropAttachment(string id, Vector2 position)
    {
        var item = ItemDatabase.Instance?.GetItem(id);
        if (item != null) Items.Drop(position, new ItemStack(item, 1));
    }

    /// <summary>
    /// 【临时】给**装备的容器**里塞点东西，方便看 Tab 面板右侧的物品列表
    /// （也方便试「拖动换口袋 / 拖回去合并」「右键丢弃 / 拆分」）。
    /// </summary>
    private void SeedBackpack()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        var pocket = player?.Equipment.Get(Player.ContainerSlot)?.Contents;
        var db = ItemDatabase.Instance;
        if (pocket == null || db == null) return;

        var bandage = db.GetItem("bandage");
        var dagger = db.GetItem("dagger");
        var svd = db.GetItem("svd");

        // 绷带可堆叠（上限 60）→ 放 12 个就是一叠，方便看物品列表
        if (bandage != null) pocket.Add(new ItemStack(bandage, 12));
        if (dagger != null) pocket.Add(new ItemStack(dagger, 1));
        if (svd != null) pocket.Add(new ItemStack(svd, 1));
    }







    /// <summary>UI 层（CanvasLayer 不受 Camera2D 缩放影响）。</summary>
    private CanvasLayer _uiLayer;

    private Camera2D _camera;

    /// <summary>
    /// **世界视角放大 2 倍**（画面面积 4 倍），HUD 和面板保持原尺寸：
    /// <list type="bullet">
    /// <item>给玩家挂一个 Zoom=2 的 Camera2D（跟随玩家）</item>
    /// <item>把准星 / 装备面板 / 地面面板搬进独立的 CanvasLayer —— CanvasLayer 不受摄像机影响</item>
    /// <item>镜头限制在地图范围内，避免看到地图外的灰边</item>
    /// </list>
    /// </summary>
    private void SetupWorldZoom()
    {
        // ① UI 层
        _uiLayer = new CanvasLayer { Name = "UiLayer", Layer = 1 };
        AddChild(_uiLayer);

        foreach (var name in new[] { "Crosshair", "EquipmentPanel", "GroundPanel" })
        {
            var node = GetNodeOrNull(name);
            node?.Reparent(_uiLayer);
        }

        // ② 镜头
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return;

        _camera = new Camera2D { Name = "Camera", Zoom = new Vector2(1.0f, 1.0f) };
        player.AddChild(_camera);
        _camera.MakeCurrent();

        _camera.LimitLeft = (int)WorldBounds.Position.X;
        _camera.LimitTop = (int)WorldBounds.Position.Y;
        _camera.LimitRight = (int)WorldBounds.End.X;
        _camera.LimitBottom = (int)WorldBounds.End.Y;
    }














    /// <summary>战利品格子上的贴图（尸体 / 箱子）—— 用来同步"搜过变暗"。</summary>
    private readonly Dictionary<Vector2I, Sprite2D> _lootSprites = new();

    /// <summary>把某个格子的战利品贴图登记进来（尸体由 Enemy 创建后调用）。</summary>
    public void RegisterLootSprite(Vector2I cell, Sprite2D sprite)
    {
        if (sprite != null) _lootSprites[cell] = sprite;
    }

    /// <summary>房间箱子的贴图（4 种轮流用）。</summary>
    private static readonly string[] ChestSprites =
    {
        // 32×32 原生（MSX）：垃圾桶 / 垃圾箱 —— 1:1 显示，不再 ×2
        "res://assets/sprites/objects32/chest_trashcan.png",   // 圆金属垃圾桶
        "res://assets/sprites/objects32/chest_dumpster.png",   // 方金属垃圾桶
        // ⚠ 冰箱：MSX 的 f_fridge / f_fridge_o 图块为空（0 个非透明像素），无可用原生图，
        //   暂留旧 16×16 贴图（仍 ×2）。见 docs/03 §2 与 THIRD_PARTY_NOTICES。
        "res://assets/sprites/objects/chest_fridge.png",   // 冰箱（旧 16px）
        "res://assets/sprites/objects32/chest_locker.png", // 储物柜（32×32 原生，替代第二个冰箱位）
    };

    /// <summary>在某个格子上放一张箱子贴图（底部居中，占满一格）。</summary>
    private void AddChestSprite(Vector2I cell, int variant)
    {
        string path = ChestSprites[Mathf.Abs(variant) % ChestSprites.Length];
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return;

        var centre = Grid.CellToWorld(cell) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);

        // 32×32 原生贴图 1:1；仍是 16px 的（目前只有冰箱）才 ×2
        float chestScale = tex.GetHeight() >= 32 ? 1.0f : 2.0f;
        float drawHeight = tex.GetHeight() * chestScale;

        var sprite = new Sprite2D
        {
            Name = "Chest",
            Texture = tex,
            Centered = true,
            Scale = new Vector2(chestScale, chestScale),
            Position = centre + new Vector2(0, (CellSize - drawHeight) * 0.5f),
        };
        AddChild(sprite);
        RegisterLootSprite(cell, sprite);
    }

















}
