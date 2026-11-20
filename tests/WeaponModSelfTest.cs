using Godot;
using System.Collections.Generic;

/// <summary>
/// 改装系统自测（纯数据，不依赖场景）：
///   Godot --headless --path . res://tests/WeaponModSelfTest.tscn
///
/// 覆盖从 ZS 移植的四条规则：
///   1) 挂点比例：数据里有 MountX/MountY 用数据，没有用槽类型默认值
///   2) 必装槽（needed）：不能被拆空
///   3) 武器白名单（AllWeapons / WeaponIds）：不适配的枪装不上
///   4) 行动中改装限制（CanModInRaid）
/// </summary>
public partial class WeaponModSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override void _Ready()
    {
        GD.Print("=== 改装系统自测开始 ===");
        TestDefaultMount();
        TestDataMountOverride();
        TestNeededSlotBlocksUninstall();
        TestWeaponWhitelist();
        TestRaidRestriction();
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void TestDefaultMount()
    {
        var data = new WeaponSlotData { SlotType = SlotTypes.Scope };
        Check("默认挂点用槽类型比例", !data.HasMount
            && data.MountFraction.IsEqualApprox(SlotTypes.DefaultMount(SlotTypes.Scope)),
            $"实际 {data.MountFraction}");
    }

    private void TestDataMountOverride()
    {
        var data = new WeaponSlotData { SlotType = SlotTypes.Scope, MountX = 0.6f, MountY = -0.2f };
        Check("数据挂点覆盖默认值", data.HasMount
            && data.MountFraction.IsEqualApprox(new Vector2(0.6f, -0.2f)),
            $"实际 {data.MountFraction}");
    }

    private void TestNeededSlotBlocksUninstall()
    {
        var gun = MakeGun("gun_needed", out var slots, SlotTypes.Muzzle, needed: true);
        var att = MakeAttachment("att_muzzle", SlotTypes.Muzzle);
        var install = WeaponMods.TryInstall(gun, att, out _);
        Check("必装槽可以装入", install.Ok, install.Reason);

        var slot = WeaponMods.Slots(gun)[0];
        Check("必装槽禁止拆空", WeaponMods.TryUninstall(slot) == null, "TryUninstall 应返回 null");
    }

    private void TestWeaponWhitelist()
    {
        var gun = MakeGun("ak_test", out _, SlotTypes.Scope, needed: false);
        var generic = MakeAttachment("att_scope_all", SlotTypes.Scope);
        Check("通用件任何枪都能装", WeaponMods.CanInstall(gun, generic).Ok, "应为可装");

        var onlySvd = MakeAttachment("att_scope_svd", SlotTypes.Scope);
        onlySvd.Data.AttachmentStats.AllWeapons = false;
        onlySvd.Data.AttachmentStats.WeaponIds.Add("svd");
        Check("白名单外装不上", !WeaponMods.CanInstall(gun, onlySvd).Ok, "应被拒绝");
        Check("白名单内可装", onlySvd.Data.AttachmentStats.FitsWeapon("svd"), "FitsWeapon 应为真");
    }

    private void TestRaidRestriction()
    {
        var gun = MakeGun("gun_raid", out _, SlotTypes.Grip, needed: false);
        var bench = MakeAttachment("att_grip", SlotTypes.Grip);
        bench.Data.AttachmentStats.CanModInRaid = false;
        Check("行动中禁止现场改装", !WeaponMods.TryInstall(gun, bench, out _, inRaid: true).Ok, "应被拒绝");

        var gun2 = MakeGun("gun_bench", out _, SlotTypes.Grip, needed: false);
        Check("非行动状态可以改装", WeaponMods.TryInstall(gun2, bench, out _, inRaid: false).Ok, "应可装");
    }

    /// <summary>造一把带一个槽的假武器（只用数据层，不碰场景）。</summary>
    private static ItemStack MakeGun(string id, out List<WeaponSlotData> slots, string slotType, bool needed)
    {
        var slotData = new WeaponSlotData { SlotType = slotType, Needed = needed };
        slots = new List<WeaponSlotData> { slotData };

        var weapon = new WeaponData();
        weapon.Slots = new Godot.Collections.Array<WeaponSlotData> { slotData };
        var item = new ItemData { Id = id, Name = id, CategoryData = weapon };
        return new ItemStack(item, 1);
    }

    private static ItemStack MakeAttachment(string id, string slotType)
    {
        var att = new AttachmentData { SlotType = slotType };
        var item = new ItemData { Id = id, Name = id, CategoryData = att };
        return new ItemStack(item, 1);
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
