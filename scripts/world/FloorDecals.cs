using Godot;
using System.Collections.Generic;

/// <summary>
/// 地面贴花层 —— 按 ZS 的做法重建（<c>obj_map_generator_Draw_0.gml</c>）：
///
/// ZS：16px 网格上**每格** draw 一张 24×24 的 <c>s_floor_*</c> 圆形贴花，变体用 chance 选；
/// 因为 24 &gt; 16，相邻格互相压住，四角透明被盖掉 → 连续又斑驳的地面。
///
/// 本项目：格子 32px，所以贴花按 ×2 贴（48×48 &gt; 32）得到同样的重叠。
/// 所有贴花**合成到一张 ImageTexture**（只挂 1 个 Sprite2D），不是几千个节点。
///
/// 素材：<c>assets/sprites/decals/floor/&lt;地形&gt;__&lt;ZS 贴图名&gt;.png</c>（由 zs_floor_blobs.py 导出）。
/// </summary>
public partial class FloorDecals : Node2D
{
    /// <summary>贴花超出格子的大小（像素/边）：让相邻贴花重叠，盖住透明角。</summary>
    private const int Overlap = 8;

    [Export] public int Seed { get; set; } = 20260114;

    /// <summary>裂缝密度（在地面贴花之上再撒一层）。</summary>
    [Export] public float CrackDensity { get; set; } = 0.03f;

    private const string FloorDir = "res://assets/sprites/decals/floor/";

    /// <summary>
    /// 地形 → 贴花变体与**权重**。权重照抄 ZS 的 chance 分布
    /// （街道：concrete 50% / concrete2 25% / concrete3 22% / concrete4 3%），
    /// 均匀随机会让少数派配色（如偏棕的 concrete4）抢戏，整片地面变花。
    /// </summary>
    private static readonly Dictionary<string, (string Name, float Weight)[]> Blobs = new()
    {
        ["street"] = new[] { ("s_floor_concrete", 0.50f), ("s_floor_concrete2", 0.25f), ("s_floor_concrete3", 0.22f), ("s_floor_concrete4", 0.03f) },
        ["dirt"] = new[] { ("s_floor_dirt", 0.55f), ("s_floor_dirt2", 0.20f), ("s_floor_dirt_2", 0.20f), ("s_floor_dirt3", 0.05f) },
        ["grass"] = new[] { ("s_floor_grass2", 0.70f), ("s_floor_grass_forest_2", 0.20f), ("s_floor_foglie", 0.10f) },
        ["wood"] = new[] { ("s_floor_dirt_2", 0.60f), ("s_floor_dirt2", 0.40f) },
        ["tiled"] = new[] { ("s_floor_concrete2", 0.60f), ("s_floor_concrete3", 0.40f) },
        ["rubble"] = new[] { ("s_floor_dirt_rad", 0.50f), ("s_floor_dirt_swamp", 0.30f), ("s_floor_dirt3", 0.20f) },
    };

    /// <summary>按地图数据生成贴花图（由 <c>Game</c> 在铺完地形后调用）。</summary>
    public void Build(MapLayout.Terrain[,] map, Vector2 origin, float cell)
    {
        if (map == null) return;

        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int cs = Mathf.RoundToInt(cell);
        var canvas = Image.CreateEmpty(w * cs, h * cs, false, Image.Format.Rgba8);
        canvas.Fill(new Color(0.0f, 0.0f, 0.0f, 0.0f));

        var cache = new Dictionary<string, Image>();
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };

        int stamped = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                string key = TerrainKey(map[x, y]);
                if (key == null) continue;

                var blob = PickBlob(cache, key, rng);
                if (blob == null) continue;

                // 贴花 24px → ×2 = 48px，位置往外扩 Overlap，让相邻格互相压住
                var scaled = (Image)blob.Duplicate();
                scaled.Resize(blob.GetWidth() * 2, blob.GetHeight() * 2, Image.Interpolation.Nearest);
                canvas.BlendRect(scaled, new Rect2I(0, 0, scaled.GetWidth(), scaled.GetHeight()),
                    new Vector2I(x * cs - Overlap, y * cs - Overlap));
                stamped++;
            }
        }

        StampCracks(canvas, w, h, cs, rng);

        var sprite = new Sprite2D
        {
            Name = "FloorCanvas",
            Texture = ImageTexture.CreateFromImage(canvas),
            Centered = false,
            Position = origin,          // 与 TileMapLayer 用同一个世界原点
            TextureFilter = TextureFilterEnum.Nearest,
        };
        AddChild(sprite);
        GD.Print($"[FloorDecals] 地面贴花 {stamped} 格，画布 {canvas.GetWidth()}×{canvas.GetHeight()}");
    }

    /// <summary>地形 → 贴花前缀（墙返回 null：不铺）。</summary>
    private static string TerrainKey(MapLayout.Terrain t) => t switch
    {
        MapLayout.Terrain.Street => "street",
        MapLayout.Terrain.Dirt => "dirt",
        MapLayout.Terrain.Grass => "grass",
        MapLayout.Terrain.WoodFloor => "wood",
        MapLayout.Terrain.TiledFloor => "tiled",
        MapLayout.Terrain.Rubble => "rubble",
        _ => null,
    };

    private static Image PickBlob(Dictionary<string, Image> cache, string key, RandomNumberGenerator rng)
    {
        if (!Blobs.TryGetValue(key, out var names) || names.Length == 0) return null;

        float roll = rng.Randf();
        string name = names[names.Length - 1].Name;
        float acc = 0.0f;
        foreach (var (candidate, weight) in names)
        {
            acc += weight;
            if (roll <= acc) { name = candidate; break; }
        }
        string cacheKey = key + "/" + name;
        if (cache.TryGetValue(cacheKey, out var cached)) return cached;

        var tex = GD.Load<Texture2D>(FloorDir + key + "__" + name + ".png");
        var img = tex?.GetImage();
        if (img == null) return null;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        cache[cacheKey] = img;
        return img;
    }

    /// <summary>在贴花之上再撒一层裂缝（ZS 的 floor 层也有随机变体，这里用现成的裂缝图）。</summary>
    private void StampCracks(Image canvas, int w, int h, int cs, RandomNumberGenerator rng)
    {
        var crack = GD.Load<Texture2D>("res://assets/sprites/decals/crack_0.png")?.GetImage();
        if (crack == null) return;
        if (crack.IsCompressed()) crack.Decompress();
        crack.Convert(Image.Format.Rgba8);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (rng.Randf() > CrackDensity) continue;
                canvas.BlendRect(crack, new Rect2I(0, 0, crack.GetWidth(), crack.GetHeight()),
                    new Vector2I(x * cs + cs / 4, y * cs + cs / 4));
            }
        }
    }
}

