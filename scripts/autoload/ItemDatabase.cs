using Godot;
using System.Collections.Generic;

/// <summary>
/// 物品数据库单例：从 data/items.json 载入物品定义，并提供查询 / 校验 / 热重载。
///
/// 数据层职责边界：
/// - 只负责“物品定义”的载入与查询，不涉及背包容器、UI、拾取等运行时逻辑。
/// - 新增物品只改 items.json；新增分类只改 ItemCategory；
///   新增稀有度只改 ItemRarity；新增分类字段只加 Stats 键。
/// </summary>
public partial class ItemDatabase : Node
{
    public static ItemDatabase Instance { get; private set; }

    private const string ItemsPath = "res://data/items.json";

    private readonly Dictionary<string, ItemData> _items = new();
    private readonly Dictionary<ItemCategory, List<ItemData>> _byCategory = new();
    private readonly Dictionary<ItemRarity, List<ItemData>> _byRarity = new();

    /// <summary>已载入的物品数量。</summary>
    public int Count => _items.Count;

    public override void _EnterTree() => Instance = this;

    public override void _Ready() => Reload();

    /// <summary>重新载入全部物品定义（开发期可随时调用）。</summary>
    public void Reload()
    {
        _items.Clear();
        _byCategory.Clear();
        _byRarity.Clear();

        if (!FileAccess.FileExists(ItemsPath))
        {
            GD.PushError($"ItemDatabase: 找不到物品数据文件 {ItemsPath}");
            return;
        }

        var parsed = Json.ParseString(FileAccess.GetFileAsString(ItemsPath));
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("ItemDatabase: items.json 根节点必须是对象");
            return;
        }

        var root = parsed.AsGodotDictionary();
        if (!root.ContainsKey("items") || root["items"].VariantType != Variant.Type.Array)
        {
            GD.PushError("ItemDatabase: items.json 缺少 items 数组");
            return;
        }

        int invalid = 0, duplicates = 0;
        foreach (var entry in root["items"].AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary)
            {
                invalid++;
                continue;
            }

            var data = ParseItem(entry.AsGodotDictionary());
            if (data == null)
            {
                invalid++;
                continue;
            }

            if (_items.ContainsKey(data.Id))
            {
                GD.PushError($"ItemDatabase: 物品 Id 重复 '{data.Id}'，已跳过");
                duplicates++;
                continue;
            }

            _items[data.Id] = data;

            if (!_byCategory.TryGetValue(data.Category, out var list))
            {
                list = new List<ItemData>();
                _byCategory[data.Category] = list;
            }
            list.Add(data);

            if (!_byRarity.TryGetValue(data.Rarity, out var rarityList))
            {
                rarityList = new List<ItemData>();
                _byRarity[data.Rarity] = rarityList;
            }
            rarityList.Add(data);
        }

        GD.Print($"[ItemDatabase] 已载入 {_items.Count} 件物品（无效 {invalid}，重复 {duplicates}）");
        PrintRaritySummary();
    }

    /// <summary>打印各稀有度的物品数量（便于确认数据分布）。</summary>
    public void PrintRaritySummary()
    {
        var parts = new List<string>();
        foreach (var rarity in ItemRarityInfo.All)
        {
            int n = _byRarity.TryGetValue(rarity, out var list) ? list.Count : 0;
            parts.Add($"{ItemRarityInfo.GetDisplayName(rarity)}={n}");
        }
        GD.Print($"[ItemDatabase] 稀有度分布: {string.Join(", ", parts)}");
    }

    // ---------- 查询 ----------

    public ItemData GetItem(string id)
        => !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out var data) ? data : null;

    public bool HasItem(string id)
        => !string.IsNullOrEmpty(id) && _items.ContainsKey(id);

    public IReadOnlyCollection<ItemData> GetAllItems() => _items.Values;

    public IReadOnlyList<ItemData> GetByCategory(ItemCategory category)
        => _byCategory.TryGetValue(category, out var list)
            ? list
            : System.Array.Empty<ItemData>();

    public IReadOnlyList<ItemData> GetByRarity(ItemRarity rarity)
        => _byRarity.TryGetValue(rarity, out var list)
            ? list
            : System.Array.Empty<ItemData>();

    /// <summary>创建一个物品堆叠；物品不存在时返回 null。</summary>
    public ItemStack CreateStack(string id, int count = 1)
    {
        var data = GetItem(id);
        return data == null ? null : new ItemStack(data, Mathf.Max(1, count));
    }

    // ---------- JSON 解析 ----------

    private static ItemData ParseItem(Godot.Collections.Dictionary dict)
    {
        string id = GetString(dict, "Id");
        if (string.IsNullOrWhiteSpace(id))
        {
            GD.PushWarning("ItemDatabase: 跳过缺少 Id 的条目");
            return null;
        }

        var data = new ItemData
        {
            Id = id,
            Name = GetString(dict, "Name", id),
            Category = ParseCategory(GetString(dict, "Category", "Misc")),
            Rarity = ParseRarity(GetString(dict, "Rarity", "Common")),
            Description = GetString(dict, "Description"),
            IconPath = GetString(dict, "IconPath"),
            Weight = GetFloat(dict, "Weight", 0.1f),
            // 格子背包：占几列/几行（不写就是 1x1）
            GridW = Mathf.Max(1, (int)GetFloat(dict, "GridW", 1.0f)),
            GridH = Mathf.Max(1, (int)GetFloat(dict, "GridH", 1.0f)),
            StackLimit = GetInt(dict, "StackLimit", 1),
            Value = GetInt(dict, "Value", 0),
        };

        if (dict.ContainsKey("Stats") && dict["Stats"].VariantType == Variant.Type.Dictionary)
            data.Stats = dict["Stats"].AsGodotDictionary();

        // 分类专属资源（武器数据 / 配件数据等）按路径载入 —— JSON 里没法直接嵌 Godot 资源
        string categoryDataPath = GetString(dict, "CategoryDataPath");
        if (!string.IsNullOrEmpty(categoryDataPath))
        {
            if (ResourceLoader.Exists(categoryDataPath))
                data.CategoryData = GD.Load<Resource>(categoryDataPath);
            else
                GD.PushWarning($"ItemDatabase: 物品 '{id}' 的 CategoryDataPath 不存在：{categoryDataPath}");
        }

        if (!string.IsNullOrEmpty(data.IconPath) && ResourceLoader.Exists(data.IconPath))
            data.Icon = GD.Load<Texture2D>(data.IconPath);

        if (!data.IsValid(out string reason))
        {
            GD.PushWarning($"ItemDatabase: 物品 '{id}' 数据非法（{reason}），已跳过");
            return null;
        }

        return data;
    }

    private static string GetString(Godot.Collections.Dictionary d, string key, string fallback = "")
        => d.ContainsKey(key) ? d[key].AsString() : fallback;

    private static int GetInt(Godot.Collections.Dictionary d, string key, int fallback)
        => d.ContainsKey(key) ? d[key].AsInt32() : fallback;

    private static float GetFloat(Godot.Collections.Dictionary d, string key, float fallback)
        => d.ContainsKey(key) ? (float)d[key].AsDouble() : fallback;

    private static ItemCategory ParseCategory(string raw)
        => System.Enum.TryParse<ItemCategory>(raw, true, out var category)
            ? category
            : ItemCategory.Misc;

    private static ItemRarity ParseRarity(string raw)
        => System.Enum.TryParse<ItemRarity>(raw, true, out var rarity)
            ? rarity
            : ItemRarity.Common;
}
