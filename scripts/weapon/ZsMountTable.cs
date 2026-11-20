using Godot;
using System.Collections.Generic;

/// <summary>
/// 从 ZS 反编译出来的**枪械挂点表**。
///
/// 数据来源（全部由 <c>ZS_assets/tools/zs_import.py</c> 生成）：
/// - 挂点像素：<c>weapon.json → weapon.mods[slot].{x,y}</c>
/// - 枪贴图原点 / 配件贴图原点：<c>sprite_origins.txt</c>（UndertaleModTool 导出）
///
/// ZS 的绘制规则（<c>item_weapon_draw_modded.gml</c>）：
///   底图用 <c>sprite_inv</c>、以底图原点为锚点；配件画在 <c>(x + slot.x, y + slot.y)</c>，
///   用**配件自身的原点**。所以本项目里：配件左上角 = 挂点 − 配件原点（底色原点靠 grip 对齐）。
///
/// 物品 id 写 <c>zs:ak74</c> 这种前缀即启用 ZS 素材（贴图 <c>weapons_cdda/zs/ak74_base.png</c>）。
/// 数据在生成文件 <c>ZsMountTable.Data.cs</c> 里（68 把枪），这里只负责解析和查询。
/// </summary>
public static partial class ZsMountTable
{
    public const string Prefix = "zs:";

    /// <summary>一个挂点：贴图像素偏移 + 配件贴图自身的原点。</summary>
    public readonly struct Mount
    {
        public readonly Vector2 Offset;
        public readonly Vector2 Origin;

        /// <summary>水平镜像绘制（当前数据未使用，留作以后处理朝后绘制的部件）。</summary>
        public readonly bool FlipX;

        public Mount(float x, float y, float ox, float oy, bool flipX = false)
        {
            Offset = new Vector2(x, y);
            Origin = new Vector2(ox, oy);
            FlipX = flipX;
        }

        /// <summary>同一挂点、改成水平镜像绘制。</summary>
        public Mount Mirrored() => new(Offset.X, Offset.Y, Origin.X, Origin.Y, true);
    }

    /// <summary>一把枪的 ZS 数据：贴图尺寸、原点、每个槽的挂点。</summary>
    public sealed class Entry
    {
        public Vector2 Size;
        /// <summary>sprite_inv 的原点（组合图的坐标原点，几乎都是 0,0）。</summary>
        public Vector2 Origin;
        /// <summary>sprite_ingame 的原点 —— ZS 手持时绕它旋转（class_player_weapon.func_draw）。</summary>
        public Vector2 Pivot;
        public readonly Dictionary<string, Mount> Slots = new();
        /// <summary>出厂自带的配件（槽 → ZS 配件 id）：枪不装也应画出这些件（ZS 的 default_id）。</summary>
        public readonly System.Collections.Generic.Dictionary<string, string> Defaults = new();
    }

    // 懒加载：数据在另一个 partial 文件里，静态字段初始化顺序不保证（否则 Raw 可能还是 null）
    private static Dictionary<string, Entry> _table;
    private static Dictionary<string, Entry> Table => _table ??= Parse();

    /// <summary>物品 id 是不是 ZS 素材（带 <c>zs:</c> 前缀）。</summary>
    public static bool IsZsId(string itemId)
        => !string.IsNullOrEmpty(itemId) && itemId.StartsWith(Prefix);

    /// <summary>取 ZS 素材的贴图路径（表里没有的枪返回 null）。</summary>
    public static string ArtPath(string itemId)
    {
        if (!IsZsId(itemId)) return null;
        string key = itemId.Substring(Prefix.Length);
        return (_table ??= Parse()).ContainsKey(key)
            ? $"res://assets/sprites/weapons_cdda/zs/{key}_base.png"
            : null;
    }

    /// <summary>取某把枪的 ZS 挂点数据。</summary>
    public static bool TryGet(string itemId, out Entry entry)
    {
        entry = null;
        if (!IsZsId(itemId)) return false;
        return (_table ??= Parse()).TryGetValue(itemId.Substring(Prefix.Length), out entry);
    }

    /// <summary>取某个槽的挂点（本项目的槽名，自动翻译成 ZS 槽名；找不到再退回 handguard）。</summary>
    public static bool TryMount(Entry entry, string slotType, out Mount mount)
    {
        mount = default;
        if (entry == null) return false;

        string zs = ToZsSlot(slotType);
        if (entry.Slots.TryGetValue(zs, out mount)) return true;

        // 本项目的「枪管」既可能是 ZS 的 barrel，也可能只有 handguard（护木）
        if (slotType == SlotTypes.Barrel)
            return entry.Slots.TryGetValue("handguard", out mount);
        return false;
    }

    /// <summary>ZS 配件 id → 已导入的游戏内贴图路径。</summary>
    public static string ModArtPath(string modId)
        => string.IsNullOrEmpty(modId) ? null : $"res://assets/sprites/weapons_cdda/zs/mods/{modId}.png";

    /// <summary>ZS 的槽名 → 本项目的槽名（默认件要按本项目的槽类型参与绘制顺序）。</summary>
    public static string ToSlotType(string zsSlot) => zsSlot switch
    {
        "brake" => SlotTypes.Muzzle,
        "magazine" => SlotTypes.ExtendedMag,
        "stock" => SlotTypes.Body,
        "handguard" => SlotTypes.Barrel,
        "att_1" => SlotTypes.Laser1,
        "att_2" => SlotTypes.Laser2,
        _ => zsSlot,
    };

    /// <summary>本项目的槽名 → ZS 的槽名。</summary>
    public static string ToZsSlot(string slotType) => slotType switch
    {
        SlotTypes.Muzzle => "brake",
        SlotTypes.Barrel => "barrel",
        SlotTypes.ExtendedMag => "magazine",
        SlotTypes.Body => "stock",
        SlotTypes.Laser1 => "att_1",
        SlotTypes.Laser2 => "att_2",
        _ => slotType,
    };

    /// <summary>解析生成文件里的 <c>Raw</c>：<c>枪id|宽|高|原点x|y|槽:x,y,原点x,y;...</c></summary>
    private static Dictionary<string, Entry> Parse()
    {
        var table = new Dictionary<string, Entry>(Raw.Length);
        foreach (var row in Raw)
        {
            var head = row.Split('|');
            if (head.Length < 9) continue;

            var entry = new Entry
            {
                Size = new Vector2(ParseFloat(head[1]), ParseFloat(head[2])),
                Origin = new Vector2(ParseFloat(head[3]), ParseFloat(head[4])),
                Pivot = new Vector2(ParseFloat(head[5]), ParseFloat(head[6])),
            };

            foreach (var slot in head[7].Split(';'))
            {
                if (string.IsNullOrWhiteSpace(slot)) continue;
                var kv = slot.Split(':');
                if (kv.Length < 2) continue;
                var nums = kv[1].Split(',');
                if (nums.Length < 4) continue;
                entry.Slots[kv[0]] = new Mount(
                    ParseFloat(nums[0]), ParseFloat(nums[1]), ParseFloat(nums[2]), ParseFloat(nums[3]));
            }

            foreach (var def in head[8].Split(';'))
            {
                if (string.IsNullOrWhiteSpace(def)) continue;
                var kv = def.Split(':');
                if (kv.Length == 2) entry.Defaults[kv[0]] = kv[1];
            }
            table[head[0]] = entry;
        }
        return table;
    }

    private static float ParseFloat(string s) => float.TryParse(s, out var v) ? v : 0.0f;
}




