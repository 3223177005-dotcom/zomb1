using Godot;

/// <summary>
/// 护甲机制自测（纯数据层）：
///   Godot --headless --path . res://tests/ArmorSelfTest.tscn
///
/// 公式照 ZS <c>bullet_hit_player.gml</c>：
///   chance = 0.9 + (pen - 10*cls)/20 - log10(dur/100)*(pen&lt;=cls*10 ? 2 : 2*0.15)
///   打穿：伤害 ×(chance/2+0.5)，耐久 -dmg*0.08*relK
///   挡住：伤害 ×0.15，耐久 -dmg*0.10*relK
/// </summary>
public partial class ArmorSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override void _Ready()
    {
        GD.Print("=== 护甲机制自测开始 ===");
        TestPenetrateChance();
        TestBlock();
        TestPenetrate();
        TestBrokenArmor();
        TestDurabilityDecay();
        TestEquipRules();
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void TestPenetrateChance()
    {
        // 满耐久、0 穿透、IV 级 → 必定被挡（chance 0）
        float c0 = ArmorService.PenetrateChance(0.0f, 4, 100.0f);
        Check("IV 级满耐久挡 0 穿透", Mathf.IsZeroApprox(c0), $"chance={c0:F3}");

        // 60 穿透 vs II 级：远超 10*2 → 必穿
        float c1 = ArmorService.PenetrateChance(60.0f, 2, 100.0f);
        Check("60 穿透打穿 II 级", Mathf.IsEqualApprox(c1, 1.0f), $"chance={c1:F3}");

        // 耐久越低越容易穿
        float low = ArmorService.PenetrateChance(25.0f, 4, 20.0f);
        float high = ArmorService.PenetrateChance(25.0f, 4, 100.0f);
        Check("耐久低更容易被打穿", low > high, $"{low:F3} vs {high:F3}");
    }

    private void TestBlock()
    {
        float dur = 100.0f;
        var r = ArmorService.Absorb(40.0f, 0.0f, armorClass: 4, durDamage: 35.0f,
            ref dur, roll01: 0.99f);       // 0.99 > chance(0) → 挡住

        Check("挡住时只吃 15%", Mathf.IsEqualApprox(r.Damage, 6.0f), $"伤害={r.Damage:F2}");
        Check("挡住时耐久下降", dur < 100.0f, $"耐久={dur:F1}");
        Check("挡住标记正确", r.Blocked, "Blocked 应为 true");
    }

    private void TestPenetrate()
    {
        float dur = 100.0f;
        var r = ArmorService.Absorb(40.0f, 60.0f, armorClass: 2, durDamage: 35.0f,
            ref dur, roll01: 0.0f);        // 0 < chance(1) → 打穿

        Check("打穿时伤害几乎全吃", r.Damage > 30.0f, $"伤害={r.Damage:F2}");
        Check("打穿时耐久也下降", dur < 100.0f, $"耐久={dur:F1}");
        Check("打穿不算挡住", !r.Blocked, "Blocked 应为 false");
    }

    private void TestBrokenArmor()
    {
        float dur = 0.0f;
        var r = ArmorService.Absorb(40.0f, 0.0f, armorClass: 6, durDamage: 35.0f, ref dur, roll01: 0.5f);
        Check("破甲后全额吃伤害", Mathf.IsEqualApprox(r.Damage, 40.0f), $"伤害={r.Damage:F2}");
        Check("破甲不再掉耐久", Mathf.IsZeroApprox(dur), $"耐久={dur:F1}");
    }

    private void TestDurabilityDecay()
    {
        float soft = 100.0f;
        float tough = 100.0f;
        ArmorService.Absorb(50.0f, 0.0f, 4, 200.0f, ref soft, roll01: 0.99f);
        ArmorService.Absorb(50.0f, 0.0f, 4, 20.0f, ref tough, roll01: 0.99f);
        Check("dur_damage 高的掉得快", soft < tough, $"{soft:F1} vs {tough:F1}");
    }

    private void TestEquipRules()
    {
        var body = ArmorItem("armor_arman", cls: 4, durDamage: 35.0f, helmet: false);
        var head = ArmorItem("helmet_steel", cls: 3, durDamage: 45.0f, helmet: true);

        Check("身体甲能进护甲位", EquipRules.Accepts(EquipSlotKind.Armor, body), "应接受");
        Check("头盔不能进护甲位", !EquipRules.Accepts(EquipSlotKind.Armor, head), "应拒绝");
        Check("头盔能进头盔位", EquipRules.Accepts(EquipSlotKind.Helmet, head), "应接受");
        Check("身体甲不能进头盔位", !EquipRules.Accepts(EquipSlotKind.Helmet, body), "应拒绝");
        Check("普通物品进不了护甲位",
            !EquipRules.Accepts(EquipSlotKind.Armor, new ItemStack(new ItemData { Id = "x", Category = ItemCategory.Loot }, 1)),
            "应拒绝");
    }

    private static ItemStack ArmorItem(string id, int cls, float durDamage, bool helmet)
    {
        var data = new ItemData { Id = id, Name = id, Category = ItemCategory.Armor };
        data.Stats[ItemStats.MaxDurability] = 100.0f;
        data.Stats[ItemStats.ArmorClass] = cls;
        data.Stats[ItemStats.ArmorDurDamage] = durDamage;
        data.Stats[ItemStats.ArmorIsHelmet] = helmet ? 1 : 0;
        var stack = new ItemStack(data, 1);
        stack.Durability = 100.0f;
        return stack;
    }

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
