using Godot;
using System.Collections.Generic;

/// <summary>
/// 手持武器 + 配件的视觉快照（含 ZS 原生素材对比）。窗口模式运行（headless 没渲染）：
///   Godot --path . res://tests/WeaponVisualShot.tscn
/// 产物：user://shot_mods.png（本项目素材）/ shot_zs.png（ZS 素材）/ shot_compare.png / shot_zs_guns.png
///
/// ZS 素材用 id 前缀 <c>zs:</c>（如 <c>zs:svd</c>），挂点取自 <see cref="ZsMountTable"/>。
/// </summary>
public partial class WeaponVisualShot : Node2D
{
    private const string ZsDir = "res://assets/sprites/weapons_cdda/zs/";

    private Camera2D _cam;

    public override async void _Ready()
    {
        var bg = new ColorRect { Name = "Bg", Color = new Color(0.12f, 0.12f, 0.14f), Size = new Vector2(960, 540) };
        AddChild(bg);

        var cam = new Camera2D { Name = "Cam", Position = new Vector2(480, 270), Zoom = new Vector2(5.0f, 5.0f) };
        AddChild(cam);
        _cam = cam;
        cam.MakeCurrent();

        await ShotTheirArt();
        await ShotZsArt();
        await ShotCompare();
        await ShotZsGuns();
        await ShotZsRaw();

        GD.Print("shots saved:", ProjectSettings.GlobalizePath("user://"));
        GetTree().Quit();
    }

    /// <summary>本项目原有素材（CDDA 枪 + 物品图标当配件）。</summary>
    private async System.Threading.Tasks.Task ShotTheirArt()
    {
        await Clear();
        var visual = Spawn(new Vector2(480, 270), "svd");
        await Frames(3);
        visual.AimAt(Vector2.Right);
        visual.Mods?.RefreshFrom(new List<ModMount>
        {
            new(SlotTypes.Muzzle, Fake("muzzle", "res://assets/sprites/items32/bandage.png"), SlotTypes.DefaultMount(SlotTypes.Muzzle)),
            new(SlotTypes.Scope, Fake("scope", "res://assets/sprites/items32/cash.png"), SlotTypes.DefaultMount(SlotTypes.Scope)),
            new(SlotTypes.ExtendedMag, Fake("mag", "res://assets/sprites/items32/ammo_762x54r.png"), SlotTypes.DefaultMount(SlotTypes.ExtendedMag)),
            new(SlotTypes.Grip, Fake("grip", "res://assets/sprites/items32/gold.png"), SlotTypes.DefaultMount(SlotTypes.Grip)),
        });
        await Frames(3);
        Save("user://shot_mods.png");
    }

    /// <summary>ZS 原生素材 + ZS 像素挂点（左对齐、缩放 1）。</summary>
    private async System.Threading.Tasks.Task ShotZsArt()
    {
        await Clear();
        // 走真实数据路径：ItemDatabase 的 svd + ZsArt 解析 + 出厂件补画
        var visual = Spawn(new Vector2(430, 258), "zs:svd");
        await Frames(3);
        visual.AimAt(Vector2.Right);
        var realSvd = ItemDatabase.Instance?.GetItem("svd");
        if (realSvd != null) visual.Mods?.Refresh(new ItemStack(realSvd, 1));
        // 标出子弹出膛点，便于肉眼核对
        var dot = new ColorRect
        {
            Name = "MuzzleDot",
            Color = new Color(1.0f, 0.2f, 0.2f),
            Size = new Vector2(3.0f, 3.0f),
            Position = visual.MuzzleGlobal - new Vector2(1.5f, 1.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(dot);

        await Frames(3);
        Save("user://shot_zs.png");

        var gun = visual.GetNodeOrNull<Sprite2D>("Gun");
        GD.Print("ZS gun tex=", gun?.Texture?.ResourcePath, " rect=", gun?.GetRect(), " global=", gun?.GlobalPosition);
        if (visual.Mods != null)
        {
            foreach (var child in visual.Mods.GetChildren())
            {
                if (child is Sprite2D s)
                    GD.Print("  zs mod ", s.Name, " local=", s.Position, " global=", s.GlobalPosition,
                        " gScale=", s.GlobalScale, " tex=", s.Texture?.GetSize());
            }
        }
    }

    /// <summary>并排对比：上 = 本项目素材，下 = ZS 素材。</summary>
    private async System.Threading.Tasks.Task ShotCompare()
    {
        await Clear();
        var a = Spawn(new Vector2(480, 215), "svd");
        await Frames(2);
        a.AimAt(Vector2.Right);
        a.Mods?.RefreshFrom(new List<ModMount>
        {
            new(SlotTypes.Muzzle, Fake("muzzle", "res://assets/sprites/items32/bandage.png"), SlotTypes.DefaultMount(SlotTypes.Muzzle)),
            new(SlotTypes.Scope, Fake("scope", "res://assets/sprites/items32/cash.png"), SlotTypes.DefaultMount(SlotTypes.Scope)),
            new(SlotTypes.ExtendedMag, Fake("mag", "res://assets/sprites/items32/ammo_762x54r.png"), SlotTypes.DefaultMount(SlotTypes.ExtendedMag)),
        });

        var b = Spawn(new Vector2(410, 300), "zs:svd");
        await Frames(2);
        b.AimAt(Vector2.Right);
        b.Mods?.RefreshFrom(ZsSvdMounts());
        await Frames(3);
        Save("user://shot_compare.png");
    }

    /// <summary>ZS 的 AK74 / M4：验证挂点表对不同枪都成立。</summary>
    private async System.Threading.Tasks.Task ShotZsGuns()
    {
        await Clear();
        _cam.Zoom = new Vector2(3.2f, 3.2f);
        var ak = Spawn(new Vector2(480, 230), "zs:ak_74");
        await Frames(2);
        ak.AimAt(Vector2.Right);
        ak.Mods?.RefreshFrom(ZsMounts("zs:ak_74",
            (SlotTypes.Body, "ak74_stock_s_3"), (SlotTypes.Barrel, "ak74_handguard_s_3"),
            (SlotTypes.ExtendedMag, "ak74_magazine_s_3"), (SlotTypes.Grip, "ak74_grip_s_3")));

        var m4 = Spawn(new Vector2(480, 310), "zs:m4a1");
        await Frames(2);
        m4.AimAt(Vector2.Right);
        m4.Mods?.RefreshFrom(ZsMounts("zs:m4a1",
            (SlotTypes.Barrel, "m4_handguard_1"), (SlotTypes.ExtendedMag, "m4_magazine_30")));
        await Frames(3);
        Save("user://shot_zs_guns.png");
    }

    /// <summary>逐像素对比用的原样渲染：相机与枪都在原点，zoom=6，底色纯黑。</summary>
    private async System.Threading.Tasks.Task ShotZsRaw()
    {
        await Clear();
        // 枪节点 _gun.Scale=2，所以相机 zoom 3 → 纹理像素在图上正好 6 倍
        _cam.Zoom = new Vector2(3.0f, 3.0f);
        _cam.Position = Vector2.Zero;
        var bgNode = GetNodeOrNull<ColorRect>("Bg");
        if (bgNode != null) bgNode.Color = Colors.Black;

        var visual = Spawn(Vector2.Zero, "zs:svd");
        await Frames(3);
        visual.AimAt(Vector2.Right);
        visual.Mods?.RefreshFrom(ZsSvdMounts());
        await Frames(4);
        Save("user://shot_zs_raw.png");

        _cam.Position = new Vector2(480, 270);
    }

    /// <summary>按项目里真实的物品 id 装（silencer / extmag），验证 ZsArt 的映射生效。</summary>
    private static List<ModMount> ZsSvdMounts() => ZsMounts("zs:svd",
        (SlotTypes.Body, "svd_stock_1", "res://assets/sprites/weapons_cdda/zs/mods/mod_svd_stock_1.png"), (SlotTypes.Barrel, "svd_handguard_1", "res://assets/sprites/weapons_cdda/zs/mods/mod_svd_handguard_1.png"),
        (SlotTypes.ExtendedMag, "extmag", "res://assets/sprites/items32/zs/mod/extmag.png"),
        (SlotTypes.Muzzle, "silencer", "res://assets/sprites/items32/zs/mod/silencer.png"),
        (SlotTypes.Muzzle, "silencer", "res://assets/sprites/items32/zs/mod/silencer.png"));

    private static List<ModMount> ZsMounts(string gunId, params (string Slot, string File)[] parts)
    {
        ZsMountTable.TryGet(gunId, out var entry);
        var list = new List<ModMount>();
        foreach (var p in parts)
        {
            ZsMountTable.TryMount(entry, p.Slot, out var mount);
            list.Add(new ModMount(p.Slot, ZsFake(p.File), Vector2.Zero, mount));
        }
        return list;
    }

    private static List<ModMount> ZsMounts(string gunId, params (string Slot, string Id, string Icon)[] parts)
    {
        ZsMountTable.TryGet(gunId, out var entry);
        var list = new List<ModMount>();
        foreach (var p in parts)
        {
            // ZS 素材只有「数据里有挂点」的槽才画：没有坐标就没有正确位置
            if (!ZsMountTable.TryMount(entry, p.Slot, out var mount)) continue;
            list.Add(new ModMount(p.Slot, Fake(p.Id, p.Icon), Vector2.Zero, mount));
        }
        return list;
    }



    private WeaponVisual Spawn(Vector2 at, string itemId)
    {
        var holder = new Node2D { Name = "Holder_" + itemId.Replace(':', '_'), Position = at };
        AddChild(holder);
        var visual = new WeaponVisual { Name = "WeaponVisual" };
        holder.AddChild(visual);
        visual.SetGunItem(itemId);
        return visual;
    }

    private async System.Threading.Tasks.Task Clear()
    {
        foreach (var child in GetChildren())
        {
            var name = child.Name.ToString();
            if (!name.StartsWith("Holder")) continue;
            RemoveChild(child);
            child.QueueFree();
        }
        await Frames(1);
    }

    private async System.Threading.Tasks.Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static ItemStack ZsFake(string file) => Fake(file, ZsDir + file + ".png");

    private static ItemStack Fake(string id, string iconPath)
    {
        var data = new ItemData { Id = id, Name = id, Icon = GD.Load<Texture2D>(iconPath) };
        return new ItemStack(data, 1);
    }

    private void Save(string path)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(path);
    }
}




















