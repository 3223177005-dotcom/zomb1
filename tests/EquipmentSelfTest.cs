using Godot;

/// <summary>
/// 装备栏自测（纯数据层，不需要场景）：
///   Godot --headless --path . res://tests/EquipmentSelfTest.tscn
///
/// 覆盖 ZS 式装备机制的四条：
///   1) 武器只能进武器位、背包只能进背包位
///   2) 装上：从来源扣 1 件
///   3) 卸下：放回容器，放不下则保持装备状态
///   4) 换装：旧件回收到来源，不会凭空消失
/// </summary>
public partial class EquipmentSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override void _Ready()
    {
        GD.Print("=== 装备栏自测开始 ===");
        TestEquipWeaponAndUnequip();
        TestKindRules();
        TestUnequipFailsWhenFull();
        TestSwapReturnsPrevious();
        TestContainerRoundTrip();
        TestDurabilitySurvivesReequip();
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void TestEquipWeaponAndUnequip()
    {
        var slots = NewSlots();
        var bag = NewBag();
        var gun = MakeWeapon("svd");
        bag.TryInsert(gun, out _);

        bool ok = EquipService.TryEquip(slots, 0, gun, bag, bag, out _, out string reason);
        Check("武器装上武器位", ok && slots.Get(0)?.Data?.Id == "svd", reason);
        Check("装上后从来源扣掉", bag.CountOf("svd") == 0, $"背包还有 {bag.CountOf("svd")}");

        bool back = EquipService.TryUnequip(slots, 0, bag, out _, out string r2);
        Check("卸下放回容器", back && slots.Get(0) == null && bag.CountOf("svd") == 1, r2);
    }

    private void TestKindRules()
    {
        var slots = NewSlots();
        var gun = MakeWeapon("svd");
        var pack = MakeContainer("backpack");

        Check("武器不能进背包位", !EquipService.CanEquip(slots, 2, gun, out _), "应被拒绝");
        Check("背包不能进武器位", !EquipService.CanEquip(slots, 0, pack, out _), "应被拒绝");
        Check("背包能进背包位", EquipService.CanEquip(slots, 2, pack, out _), "应被接受");
    }

    private void TestUnequipFailsWhenFull()
    {
        var slots = NewSlots();
        var bag = NewBag();
        var gun = MakeWeapon("svd");
        bag.TryInsert(gun, out _);
        EquipService.TryEquip(slots, 0, gun, bag, bag, out _, out _);

        // 把背包填满：再塞 11 件（4×3 = 12 格）
        for (int i = 0; i < 12; i++)
            bag.TryInsert(MakeLoot("junk" + i), out _);

        bool ok = EquipService.TryUnequip(slots, 0, bag, out _, out string reason);
        Check("背包满时卸不下来", !ok && slots.Get(0)?.Data?.Id == "svd", reason);
    }

    private void TestSwapReturnsPrevious()
    {
        var slots = NewSlots();
        var bag = NewBag();
        var a = MakeWeapon("svd");
        var b = MakeWeapon("dagger");
        bag.TryInsert(a, out _);
        bag.TryInsert(b, out _);

        EquipService.TryEquip(slots, 0, a, bag, bag, out _, out _);
        bool ok = EquipService.TryEquip(slots, 0, b, bag, bag, out var replaced, out string reason);

        Check("换装成功且装上新的", ok && slots.Get(0)?.Data?.Id == "dagger", reason);
        Check("旧件回到背包", bag.CountOf("svd") == 1, $"svd 数量 {bag.CountOf("svd")}");
        Check("换下的正是旧件", replaced == null || replaced.Data?.Id == "svd",
            "replaced 不符合预期");
    }

    /// <summary>背包：装上 → 卸下 → 再装回去（用户报的"脱下来拖不回去"）。</summary>
    private void TestContainerRoundTrip()
    {
        var slots = NewSlots();
        var bag = NewBag();
        var pack = MakeContainer("backpack");
        bag.TryInsert(pack, out _);

        Check("背包位类型正确", slots.KindOf(2) == EquipSlotKind.Container, $"实际 {slots.KindOf(2)}");

        bool once = EquipService.TryEquip(slots, 2, pack, bag, bag, out _, out string r1);
        Check("背包装上背包位", once && slots.Get(2)?.Data?.Id == "backpack", r1);

        bool off = EquipService.TryUnequip(slots, 2, bag, out _, out string r2);
        Check("背包卸回背包格", off && slots.Get(2) == null, r2);

        bool again = EquipService.TryEquip(slots, 2, pack, bag, bag, out _, out string r3);
        Check("背包能再装回去", again && slots.Get(2)?.Data?.Id == "backpack", r3);
    }

    /// <summary>护甲：脱下再穿不能把耐久刷满（换装复制必须继承实例状态）。</summary>
    private void TestDurabilitySurvivesReequip()
    {
        var slots = NewSlots();
        var bag = NewBag();
        var armor = MakeArmor("armor_arman");
        armor.Durability = 37.0f;
        bag.TryInsert(armor, out _);

        bool on = EquipService.TryEquip(slots, 3, armor, bag, bag, out _, out string r1);
        Check("护甲装上后耐久保留", on && Mathf.IsEqualApprox(slots.Get(3)?.Durability ?? -1.0f, 37.0f),
            $"{r1} 耐久={slots.Get(3)?.Durability}");

        EquipService.TryUnequip(slots, 3, bag, out var moved, out _);
        bool again = EquipService.TryEquip(slots, 3, moved, bag, bag, out _, out string r2);
        Check("脱下再穿不免费修满", again && Mathf.IsEqualApprox(slots.Get(3)?.Durability ?? -1.0f, 37.0f),
            $"{r2} 耐久={slots.Get(3)?.Durability}");
    }

    private static ItemStack MakeArmor(string id)
    {
        var data = new ItemData { Id = id, Name = id, Category = ItemCategory.Armor };
        data.Stats[ItemStats.MaxDurability] = 100.0f;
        data.Stats[ItemStats.ArmorClass] = 4;
        data.Stats[ItemStats.ArmorDurDamage] = 35.0f;
        data.Stats[ItemStats.ArmorIsHelmet] = 0;
        return new ItemStack(data, 1);      // 构造函数会把耐久初始化成 100
    }

    private static EquipmentSlots NewSlots()
        => new(EquipSlotKind.Weapon, EquipSlotKind.Weapon, EquipSlotKind.Container,
            EquipSlotKind.Armor, EquipSlotKind.Helmet);

    private static Inventory NewBag() => new(float.MaxValue, 4, 3);

    private static ItemStack MakeWeapon(string id)
    {
        var data = new ItemData
        {
            Id = id,
            Name = id,
            Category = ItemCategory.Weapon,
            CategoryData = new RangedWeaponData { GunStats = new GunData() },
        };
        return new ItemStack(data, 1);
    }

    private static ItemStack MakeContainer(string id)
    {
        var data = new ItemData
        {
            Id = id,
            Name = id,
            Category = ItemCategory.Container,
            CategoryData = new ContainerData { GridW = 4, GridH = 3 },
        };
        return new ItemStack(data, 1);
    }

    private static ItemStack MakeLoot(string id)
        => new(new ItemData { Id = id, Name = id, Category = ItemCategory.Loot }, 1);

    private void Check(string what, bool ok, string detail)
    {
        if (ok)
        {
            _pass++;
            GD.Print($"  [PASS] {what}");
            return;
        }
        _fail++;
        GD.PrintErr($"  [FAIL] {what} —— {detail}");
    }
}




