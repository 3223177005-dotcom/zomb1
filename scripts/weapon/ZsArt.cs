using Godot;

/// <summary>
/// 「本项目物品 id」→「ZS 素材」的桥。
///
/// 目的：不动 <c>data/items.json</c>（避免规则 13 的数据回填与 id 变更风险），
/// 只在**运行时**把能对上的物品换成 ZS 的贴图 / 图标：
///   · 枪：id 与 ZS 同名（如 <c>svd</c>）且 <c>weapons_cdda/zs/&lt;id&gt;_base.png</c> 存在
///         → 手持用 <c>zs:&lt;id&gt;</c>（ZS 底图 + ZS 挂点），图标用 <c>items32/zs/gun/&lt;id&gt;.png</c>。
///   · 配件：按 <see cref="ModArt"/> 的显式表映射（本项目 id 与 ZS id 不同名）。
///
/// 素材来路见 <c>ZS_assets/tools/zs_import.py</c>；全部是 ZS 的美术，仅供本地研究。
/// </summary>
public static class ZsArt
{
    private const string GunArtDir = "res://assets/sprites/weapons_cdda/zs/";
    private const string GunIconDir = "res://assets/sprites/items32/zs/gun/";
    private const string ModIconDir = "res://assets/sprites/items32/zs/mod/";
    private const string ModArtDir = "res://assets/sprites/weapons_cdda/zs/mods/";

    /// <summary>本项目的配件 id → ZS 的配件贴图文件名（不含 .png，位于 <c>zs/mods/</c>）。</summary>
    private static readonly (string Id, string Art)[] ModArtTable =
    {
        ("silencer", "silencer_762x54"),      // SVD 口径消音器
        ("extmag", "svd_magazine_2"),         // SVD 扩容弹匣
        ("tacgrip", "foregrip_wood"),         // 木质前握把
    };

    /// <summary>枪：能对上 ZS 素材就返回 <c>zs:&lt;id&gt;</c>，否则返回 null。</summary>
    public static string GunId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemId.StartsWith(ZsMountTable.Prefix)) return null;
        string path = GunArtDir + itemId + "_base.png";
        return ResourceLoader.Exists(path) ? ZsMountTable.Prefix + itemId : null;
    }

    /// <summary>物品 id → ZS 挂点表条目（自动加 <c>zs:</c> 前缀；对不上返回 false）。</summary>
    public static bool TryEntry(string itemId, out ZsMountTable.Entry entry)
    {
        entry = null;
        string id = GunId(itemId) ?? itemId;
        return ZsMountTable.TryGet(id, out entry);
    }

    /// <summary>枪的物品图标路径（没有 ZS 图标返回 null）。</summary>
    public static string GunIcon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        string path = GunIconDir + itemId + ".png";
        return ResourceLoader.Exists(path) ? path : null;
    }

    /// <summary>配件的游戏内贴图路径（没有映射返回 null）。</summary>
    public static string ModArt(string itemId)
    {
        foreach (var (id, art) in ModArtTable)
        {
            if (id != itemId) continue;
            string path = ModArtDir + art + ".png";
            return ResourceLoader.Exists(path) ? path : null;
        }
        return null;
    }

    /// <summary>配件的物品图标路径（没有 ZS 图标返回 null）。</summary>
    public static string ModIcon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        string path = ModIconDir + itemId + ".png";
        return ResourceLoader.Exists(path) ? path : null;
    }

    /// <summary>给物品数据库用：ZS 有图标就换成 ZS 的，没有返回 null（保持原样）。</summary>
    public static string IconOverride(string itemId, bool isAttachment)
        => isAttachment ? ModIcon(itemId) : GunIcon(itemId);
}

