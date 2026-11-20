using Godot;
using System.Collections.Generic;

/// <summary>
/// 把装在手枪上的配件按**枪贴图的相对位置**叠到枪身上。
///
/// 移植自 ZS 的 <c>item_weapon_draw_modded.gml</c>（固定绘制顺序 + 槽位坐标），
/// 坐标用**贴图比例**而不是像素：枪贴图大小不同（SVD / 莫辛 / 变体），固定像素一定错位。
/// 比例来源优先级：武器数据 <see cref="WeaponSlotData.MountX"/>/<c>MountY</c> → <see cref="SlotTypes.DefaultMount"/>。
///
/// 坐标系：本节点是枪 Sprite2D 的子节点，所以
///   子节点局部坐标（贴图像素）= 比例 × 贴图尺寸 − 握把坐标，
/// 外层 <c>_gun.Scale = 2</c> 负责把「贴图像素」放大到世界像素。
/// </summary>
public partial class WeaponModOverlay : Node2D
{
    /// <summary>固定绘制顺序，靠前的先画（会被后面的盖住）。</summary>
    private static readonly string[] DrawOrder =
    {
        SlotTypes.Body, SlotTypes.Barrel, SlotTypes.Grip, SlotTypes.ExtendedMag,
        SlotTypes.Muzzle, SlotTypes.Scope, SlotTypes.Laser1, SlotTypes.Laser2,
    };

    /// <summary>
    /// 配件大小**相对枪贴图高度**计算（而不是固定缩放）：枪贴图有 27×7 这种小图，
    /// 也有上百像素的大图，固定缩放一定会一小一大。
    /// 0.8 = 配件高度占枪身高度的 80%。
    /// </summary>
    private const float HeightRatio = 0.8f;

    /// <summary>贴图尺寸与握把位置（贴图像素），由 <see cref="WeaponVisual"/> 在换枪时告知。</summary>
    private Vector2 _texSize = new(1.0f, 1.0f);
    private Vector2 _grip = Vector2.Zero;
    private Vector2 _baseOrigin = Vector2.Zero;
    private bool _ready;

    private int _signature;

    /// <summary>换枪时调用：把这张枪贴图的尺寸、握把点、以及（ZS 素材的）原点传进来。</summary>
    public void Setup(Texture2D tex, Vector2 grip, Vector2 baseOrigin)
    {
        if (tex == null) return;
        _texSize = tex.GetSize();
        _grip = grip;
        _baseOrigin = baseOrigin;
        _ready = _texSize.X > 1.0f;
        _signature = 0;      // 强制下一帧重建
    }

    /// <summary>按当前手持枪的已装配件重建（自动跟随改枪面板的装/卸）。</summary>
    public override void _Process(double delta)
    {
        if (!_ready) return;
        var gun = WeaponMods.Active;
        int sig = Signature(gun);
        if (sig == _signature) return;
        _signature = sig;
        Refresh(gun);
    }

    /// <summary>用一把枪的槽位数据重建叠加层（ZS 素材取挂点表，其它取比例）。</summary>
    public void Refresh(ItemStack gun)
    {
        var list = new List<ModMount>();
        if (gun?.Data != null)
        {
            bool zs = ZsArt.TryEntry(gun.Data.Id, out var entry);

            // ZS 的枪出厂就带着枪托 / 护木 / 弹匣（weapon.json 的 default_id），
            // 我们自己的武器数据里没有这些槽，所以按 ZS 数据补画，否则手上只剩一根枪管。
            if (zs) AddFactoryParts(entry, list);

            foreach (var slot in WeaponMods.Slots(gun))
            {
                // 空槽不画，也不覆盖出厂件（ZS 的枪出厂就带枪托/护木/弹匣）
                if (slot.IsEmpty) continue;

                // 同一槽装了玩家自己的件就顶掉出厂件
                list.RemoveAll(m => m.SlotType == slot.SlotType);

                ZsMountTable.Mount mount;
                if (zs && ZsMountTable.TryMount(entry, slot.SlotType, out mount))
                    list.Add(new ModMount(slot.SlotType, slot.Installed, slot.MountFraction, mount));
                else
                    list.Add(new ModMount(slot.SlotType, slot.Installed, slot.MountFraction));
            }
        }
        RefreshFrom(list);
    }

    /// <summary>把 ZS 数据里的出厂配件（槽 → 配件 id）加进绘制列表。</summary>
    private static void AddFactoryParts(ZsMountTable.Entry entry, List<ModMount> list)
    {
        foreach (var pair in entry.Defaults)
        {
            if (!entry.Slots.TryGetValue(pair.Key, out var mount)) continue;
            var tex = GD.Load<Texture2D>(ZsMountTable.ModArtPath(pair.Value));
            if (tex == null) continue;
            list.Add(new ModMount(ZsMountTable.ToSlotType(pair.Key), pair.Value, mount, tex));
        }
    }

    /// <summary>直接给「槽类型 + 配件 + 挂点」列表重建（也方便自测塞假数据）。</summary>
    public void RefreshFrom(IReadOnlyList<ModMount> installed)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        if (!_ready || installed == null) return;

        foreach (var type in DrawOrder)
        {
            var mount = Find(installed, type);
            if (mount.Texture == null && mount.Item?.Data?.Icon == null) continue;
            AddChild(mount.Native ? BuildNativeSprite(type, mount) : BuildSprite(type, mount.Item, mount.Fraction));
        }
    }

    /// <summary>
    /// ZS 原生素材：ZS 的 <c>mods[slot].{x,y}</c> 用「贴图左上角」定位，
    /// 位置 = 挂点 − 握把点（把「贴图左上角」换算成节点坐标）。
    /// 枪托在 ZS 里是朝后画的，挂点带 <see cref="ZsMountTable.Mount.FlipX"/> 时水平镜像。
    /// </summary>
    private Sprite2D BuildNativeSprite(string type, ModMount mount) => new()
    {
        Name = "Mod_" + type,
        // 有 ZS 游戏内贴图就用它（物品图标是 32px 的库存图，贴到枪上比例不对）
        Texture = mount.Texture ?? GD.Load<Texture2D>(ZsArt.ModArt(mount.Item?.Data?.Id)) ?? mount.Item?.Data?.Icon,
        Centered = false,
        Position = (_baseOrigin - _grip) + mount.NativeOffset - mount.NativeOrigin,
        Offset = Vector2.Zero,
        Scale = Vector2.One,
        FlipH = mount.NativeFlipX,
        FlipV = type == SlotTypes.Laser2,
    };

    /// <summary>比例 → 枪局部坐标（贴图像素，相对握把点）。</summary>
    public Vector2 MountOffset(Vector2 fraction)
        => new Vector2(fraction.X * _texSize.X, fraction.Y * _texSize.Y) - _grip;

    private Sprite2D BuildSprite(string type, ItemStack item, Vector2 fraction)
    {
        var icon = item.Data.Icon;
        float iconH = icon.GetSize().Y;
        // 大小相对枪身：图标高度 → 枪身高度的 80% × 该槽的系数
        float scale = iconH > 1.0f ? HeightRatio * SlotScale(type) * _texSize.Y / iconH : HeightRatio;

        return new Sprite2D
        {
            Name = "Mod_" + type,
            Texture = icon,
            Centered = true,
            Position = MountOffset(fraction),
            Scale = new Vector2(scale, scale),
            FlipV = type == SlotTypes.Laser2,        // 对应 ZS att_4 的竖直翻转
        };
    }

    /// <summary>各槽的额外大小系数（瞄具/激光比枪口细，弹匣更胖）。</summary>
    private static float SlotScale(string type) => type switch
    {
        SlotTypes.Muzzle => 1.0f,
        SlotTypes.Barrel => 0.9f,
        SlotTypes.Scope => 0.85f,
        SlotTypes.Grip => 0.9f,
        SlotTypes.ExtendedMag => 1.1f,
        SlotTypes.Laser1 => 0.6f,
        SlotTypes.Laser2 => 0.6f,
        _ => 1.0f,
    };

    private static ModMount Find(IReadOnlyList<ModMount> list, string type)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry.SlotType != type) continue;
            if (entry.Texture != null) return entry;            // ZS 出厂件（只有贴图）
            if (entry.Item != null && !entry.Item.IsEmpty) return entry;
        }
        return new ModMount(type, null, SlotTypes.DefaultMount(type));
    }

    private static int Signature(ItemStack gun)
    {
        if (gun?.Data == null) return 0;

        int hash = 17;
        foreach (var slot in WeaponMods.Slots(gun))
        {
            hash = hash * 31 + slot.SlotType.GetHashCode();
            if (!slot.IsEmpty) hash = hash * 31 + (slot.Installed.Data?.Id ?? "").GetHashCode();
        }
        return hash;
    }
}

/// <summary>一个待画的配件：槽类型 + 配件 + 挂点比例；<see cref="Native"/> 为真时用 ZS 像素挂点。</summary>
public readonly struct ModMount
{
    public readonly string SlotType;
    public readonly ItemStack Item;
    /// <summary>直接给贴图（ZS 出厂件不是 ItemStack，只有一张游戏内贴图）。</summary>
    public readonly Texture2D Texture;
    public readonly Vector2 Fraction;
    public readonly Vector2 NativeOffset;
    public readonly Vector2 NativeOrigin;
    public readonly bool NativeFlipX;
    public readonly bool Native;

    /// <summary>本项目自己的素材：按比例挂、居中、按枪身高度缩放。</summary>
    public ModMount(string slotType, ItemStack item, Vector2 fraction)
    {
        SlotType = slotType;
        Item = item;
        Texture = null;
        Fraction = fraction;
        NativeOffset = Vector2.Zero;
        NativeOrigin = Vector2.Zero;
        NativeFlipX = false;
        Native = false;
    }

    /// <summary>ZS 原生素材：左对齐 + 缩放 1 + ZS 像素挂点 + 配件自身原点。</summary>
    public ModMount(string slotType, ItemStack item, Vector2 fraction, ZsMountTable.Mount mount)
    {
        SlotType = slotType;
        Item = item;
        Texture = null;
        Fraction = fraction;
        NativeOffset = mount.Offset;
        NativeOrigin = mount.Origin;
        NativeFlipX = mount.FlipX;
        Native = true;
    }

    /// <summary>ZS 出厂件：只有一张贴图（没有 ItemStack）。</summary>
    public ModMount(string slotType, string debugId, ZsMountTable.Mount mount, Texture2D texture)
    {
        SlotType = slotType;
        Item = null;
        Texture = texture;
        Fraction = Vector2.Zero;
        NativeOffset = mount.Offset;
        NativeOrigin = mount.Origin;
        NativeFlipX = mount.FlipX;
        Native = true;
    }
}







