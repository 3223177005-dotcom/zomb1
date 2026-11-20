using Godot;

/// <summary>
/// 用手上的**真实物品数据**走一遍手持渲染，检查配件是否齐全。
/// Godot --headless --path . res://tests/HeldWeaponProbe.tscn
/// </summary>
public partial class HeldWeaponProbe : Node
{
    public override void _Ready()
    {
        var item = ItemDatabase.Instance?.GetItem("svd");
        if (item == null)
        {
            GD.PrintErr("拿不到 svd 物品数据");
            GetTree().Quit(1);
            return;
        }

        var stack = new ItemStack(item, 1);
        GD.Print("item id=", item.Id, " 武器槽=", SlotNames(stack));
        GD.Print("ZsArt.GunId=", ZsArt.GunId(item.Id), " TryEntry=", ZsArt.TryEntry(item.Id, out var entry));

        var visual = new WeaponVisual { Name = "Probe" };
        AddChild(visual);
        visual.SetGunItem(ZsArt.GunId(item.Id) ?? item.Id);
        visual.AimAt(Vector2.Right);
        visual.Mods?.Refresh(stack);

        int count = 0;
        if (visual.Mods != null)
        {
            foreach (var child in visual.Mods.GetChildren())
            {
                if (child is Sprite2D s)
                {
                    count++;
                    GD.Print("  配件 ", s.Name, " tex=", s.Texture?.ResourcePath, " pos=", s.Position);
                }
            }
        }
        GD.Print("配件数量 = ", count, "（期望 >= 3：枪托/护木/弹匣随枪出厂）");
        GD.Print("出厂件表 = ", entry == null ? "(无)" : string.Join(",", entry.Defaults.Keys));
        GetTree().Quit(count >= 3 ? 0 : 1);
    }

    private static string SlotNames(ItemStack stack)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (var slot in WeaponMods.Slots(stack))
            names.Add(slot.SlotType + (slot.IsEmpty ? "(空)" : "=" + slot.Installed.Data?.Id));
        return string.Join(",", names);
    }
}

