using Godot;

/// <summary>
/// 背包「卸下 → 再装回」的全流程探针（含容器内容），把每一步的状态打出来。
/// Godot --headless --path . res://tests/EquipRoundTripProbe.tscn
/// </summary>
public partial class EquipRoundTripProbe : Node
{
    public override void _Ready()
    {
        var slots = new EquipmentSlots(EquipSlotKind.Weapon, EquipSlotKind.Weapon, EquipSlotKind.Container);
        var pocket = new Inventory(float.MaxValue, 4, 3);

        // 背包（容器）
        var packData = new ItemData
        {
            Id = "backpack",
            Name = "背包",
            Category = ItemCategory.Container,
            CategoryData = new ContainerData { GridW = 6, GridH = 5 },
        };
        var pack = new ItemStack(packData, 1);
        slots.Equip(2, pack);
        GD.Print("起始  槽2=", Dump(slots.Get(2)), " 口袋里背包数=", pocket.CountOf("backpack"));

        // 1) 拖出（卸下到口袋）
        bool off = EquipService.TryUnequip(slots, 2, pocket, out var moved, out string r1);
        GD.Print("卸下  ok=", off, " reason=", r1, " 槽2=", Dump(slots.Get(2)),
            " 口袋里背包数=", pocket.CountOf("backpack"), " 拖动件=", Dump(moved));

        // 2) 拖回（装回槽里），source 用口袋
        bool on = EquipService.TryEquip(slots, 2, moved, pocket, pocket, out var replaced, out string r2);
        GD.Print("装回  ok=", on, " reason=", r2, " 槽2=", Dump(slots.Get(2)),
            " 口袋里背包数=", pocket.CountOf("backpack"), " 换下=", Dump(replaced));

        bool ok = on && slots.Get(2) != null && pocket.CountOf("backpack") == 0;
        GD.Print(ok ? "PASS 往返正常" : "FAIL 往返丢东西");
        GetTree().Quit(ok ? 0 : 1);
    }

    private static string Dump(ItemStack s) => s == null ? "(空)" : $"{s.Data?.Id}×{s.Count}";
}
