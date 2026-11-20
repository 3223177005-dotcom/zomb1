using Godot;

/// <summary>
/// 移动 / 后坐模型自测（纯计算，不依赖场景）：
/// Godot --headless --path . res://tests/MoveRecoilSelfTest.tscn
///
/// 覆盖 ZS 移植的三条硬规则：
///   1) 负重超上限 → 不能跑且走速减半；超上限 +5 → 不能走
///   2) 耐力低于 5 → 禁止起跑；跑动掉耐力、静止回耐力
///   3) 后坐两池：开火累积（上限 = recoil*2）、静止衰减、移动累积受 ergo 影响
/// </summary>
public partial class MoveRecoilSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override void _Ready()
    {
        GD.Print("=== 移动 / 后坐自测开始 ===");
        TestMoveNormal();
        TestMoveOverweight();
        TestMoveBlocked();
        TestStaminaGate();
        TestAimSlowdown();
        TestRecoilShotCap();
        TestRecoilDecay();
        TestRecoilErgoDifference();
        TestPoseReloadAndSwitch();
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void TestPoseReloadAndSwitch()
    {
        var pose = new WeaponPoseModel();
        pose.StartReload(1.0f);
        pose.Update(0.25f);                                  // 换弹进行到 25% → 抬起段
        Check("换弹会抬枪", pose.AngleOffsetDeg > 0.0f, $"角度 {pose.AngleOffsetDeg}");
        pose.Update(0.5f);                                   // 75% → 放回段
        Check("换弹结束前回正", pose.AngleOffsetDeg < CombatTuning.ReloadTiltDeg, $"角度 {pose.AngleOffsetDeg}");

        var sw = new WeaponPoseModel();
        sw.StartSwitch(0.5f);
        sw.Update(0.25f);
        Check("切枪会甩枪", sw.AngleOffsetDeg > 0.0f && sw.Switching, $"角度 {sw.AngleOffsetDeg}");
        sw.Update(0.5f);
        Check("切枪结束归零", Mathf.IsZeroApprox(sw.AngleOffsetDeg), $"角度 {sw.AngleOffsetDeg}");
    }

    private void TestMoveNormal()
    {
        var r = Move(weight: 20.0f, stamina: 100.0f, sprint: false, aim: false);
        Check("正常负重走路 = 96", Mathf.IsEqualApprox(r.Speed, CombatTuning.ZsWalkSpeed), $"实际 {r.Speed}");
        var s = Move(weight: 20.0f, stamina: 100.0f, sprint: true, aim: false);
        Check("正常负重跑步 = 152", Mathf.IsEqualApprox(s.Speed, CombatTuning.ZsSprintSpeed), $"实际 {s.Speed}");
    }

    private void TestMoveOverweight()
    {
        var r = Move(weight: 55.0f, stamina: 100.0f, sprint: true, aim: false);
        Check("超上限不能跑", !r.CanSprint, "CanSprint 应为 false");
        Check("超上限走速减半", Mathf.IsEqualApprox(r.Speed, CombatTuning.ZsWalkSpeed * 0.5f), $"实际 {r.Speed}");
    }

    private void TestMoveBlocked()
    {
        var r = Move(weight: 60.0f, stamina: 100.0f, sprint: false, aim: false);
        Check("超上限 +5 不能走", !r.CanWalk && Mathf.IsEqualApprox(r.Speed, 0.0f), $"实际 {r.Speed}");
    }

    private void TestStaminaGate()
    {
        var r = Move(weight: 20.0f, stamina: 3.0f, sprint: true, aim: false);
        Check("耐力 < 5 禁止起跑", !r.CanSprint, "CanSprint 应为 false");
        var drain = Move(weight: 20.0f, stamina: 100.0f, sprint: true, aim: false);
        Check("跑步掉耐力", drain.StaminaDelta < 0.0f, $"delta {drain.StaminaDelta}");
        var regen = Move(weight: 20.0f, stamina: 50.0f, sprint: false, aim: false);
        Check("不跑回耐力", regen.StaminaDelta > 0.0f, $"delta {regen.StaminaDelta}");
    }

    private void TestAimSlowdown()
    {
        var r = Move(weight: 20.0f, stamina: 100.0f, sprint: false, aim: true);
        Check("瞄准移速 ×0.5", Mathf.IsEqualApprox(r.Speed, CombatTuning.ZsWalkSpeed * 0.5f), $"实际 {r.Speed}");
    }

    private void TestRecoilShotCap()
    {
        var m = new RecoilModel();
        float recoil = 114.0f;
        for (int i = 0; i < 20; i++) m.OnShot(recoil);
        Check("开火池上限 = recoil*2", Mathf.IsEqualApprox(m.FromShooting, recoil * 2.0f), $"实际 {m.FromShooting}");
    }

    private void TestRecoilDecay()
    {
        var m = new RecoilModel();
        m.OnShot(114.0f);
        float before = m.FromShooting;
        m.Tick(0.1f, ergo: 35.0f, weaponWeight: 3.3f, moving: false, mouseDelta: 0.0f);
        Check("静止时开火池衰减", m.FromShooting < before, $"{before} -> {m.FromShooting}");
    }

    private void TestRecoilErgoDifference()
    {
        var low = new RecoilModel();
        var high = new RecoilModel();
        for (int i = 0; i < 10; i++)
        {
            low.Tick(1.0f / 60.0f, ergo: 10.0f, weaponWeight: 4.0f, moving: true, mouseDelta: 0.0f);
            high.Tick(1.0f / 60.0f, ergo: 90.0f, weaponWeight: 4.0f, moving: true, mouseDelta: 0.0f);
        }
        Check("低 ergo 晃动更大", low.FromMovement > high.FromMovement,
            $"低 {low.FromMovement:F1} / 高 {high.FromMovement:F1}");
    }

    private static MoveResult Move(float weight, float stamina, bool sprint, bool aim)
    {
        return MoveModel.Compute(new MoveInputs
        {
            WalkSpeed = CombatTuning.ZsWalkSpeed,
            SprintSpeed = CombatTuning.ZsSprintSpeed,
            Weight = weight,
            MaxWeight = 50.0f,
            Stamina = stamina,
            SprintHeld = sprint,
            Aiming = aim,
            MoveHeld = true,
            InHub = false,
        }, 1.0f / 60.0f, 0.5f);
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
