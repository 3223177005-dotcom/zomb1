using Godot;
using System.Collections.Generic;

/// <summary>
/// 临时自测：阶段 2（Inventory 改用 GridInventory）验收。
/// 跑法：Godot --headless --path . res://_dev_test/GridSelfTest.tscn
/// </summary>
public partial class GridSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override async void _Ready()
    {
        GD.Print("=== 阶段 2 自测开始 ===");

        var db = ItemDatabase.Instance;
        Check("items.json 载入 14 件物品", db != null && db.Count == 14, $"实际 {db?.Count}");

        var svd = db.GetItem("svd");
        var dagger = db.GetItem("dagger");
        var backpackData = db.GetItem("backpack");
        var bandage = db.GetItem("bandage");
        var ammo = db.GetItem("ammo_762x54r");
        var cash = db.GetItem("cash");
        var gold = db.GetItem("gold");

        // 1. 数据层
        Check("SVD 是 4×2（8 格）", svd.GridW == 4 && svd.GridH == 2);
        Check("背包内部 6×5（30 格）",
            backpackData.ContainerGridW == 6 && backpackData.ContainerGridH == 5
            && backpackData.ContainerCellCount == 30,
            $"{backpackData.ContainerGridW}×{backpackData.ContainerGridH}");

        // 2. 自动排布
        var pocket = new Inventory(float.MaxValue, 4, 3);
        var svdStack = new ItemStack(svd, 1);
        Check("4×3 口袋能自动放下 SVD", pocket.Add(svdStack) == null);
        Check("SVD 落在 (0,0) 且未旋转",
            pocket.Stacks.Count == 1 && pocket.Stacks[0].GridX == 0 && pocket.Stacks[0].GridY == 0
            && !pocket.Stacks[0].Rotated,
            $"x={pocket.Stacks[0].GridX} y={pocket.Stacks[0].GridY}");
        Check("SVD 占 8 格", pocket.UsedCells == 8, $"实际 {pocket.UsedCells}");
        Check("占位表自洽", pocket.Validate(out string r1), r1);

        // 3. 旋转：口袋只有 2 列，SVD 4×2 必须转 90°
        var tall = new Inventory(float.MaxValue, 2, 4);
        var tallOk = tall.Add(new ItemStack(svd, 1)) == null;
        Check("2×4 口袋靠旋转装下 SVD", tallOk && tall.Stacks.Count == 1 && tall.Stacks[0].Rotated);
        Check("旋转后仍占 8 格", tall.UsedCells == 8, $"实际 {tall.UsedCells}");

        // 4. 尺寸绝对判定：1×1 口袋放 4×2 → TooBig（不是 NoSpace）
        var tiny = new Inventory(float.MaxValue, 1, 1);
        var bigCode = tiny.CanHold(new ItemStack(svd, 1));
        Check("1×1 口袋判 TooBig", bigCode == ContainCode.TooBig, bigCode.ToString());

        // 5. 塞满 → NoSpace
        var small = new Inventory(float.MaxValue, 2, 2);
        for (int i = 0; i < 4; i++) small.Add(new ItemStack(cash, 1));
        var fullCode = small.CanHold(new ItemStack(cash, 1));
        Check("2×2 装满后判 NoSpace",
            small.UsedCells == 4 && fullCode == ContainCode.NoSpace,
            $"占格 {small.UsedCells} / {fullCode}");

        // 6. 堆叠合并（同一叠只占一格）
        var one = new Inventory(float.MaxValue, 1, 1);
        one.Add(new ItemStack(ammo, 30));
        one.Add(new ItemStack(ammo, 30));
        Check("两次 30 发合并成一叠 60 发",
            one.StackCount == 1 && one.CountOf("ammo_762x54r") == 60 && one.UsedCells == 1,
            $"{one.StackCount} 叠 / {one.CountOf("ammo_762x54r")} 发 / {one.UsedCells} 格");

        // 7. 满叠之后再放 → 需要新格子，1×1 已满 → NoSpace 且返回剩余
        var leftover = one.Add(new ItemStack(ammo, 1));
        Check("1×1 已满时多出的 1 发被退回",
            leftover != null && leftover.Count == 1 && one.CountOf("ammo_762x54r") == 60,
            $"剩余 {leftover?.Count}");

        // 8. 重量上限
        var light = new Inventory(0.5f, 10, 10);
        var heavyCode = light.CanHold(new ItemStack(svd, 1));
        Check("0.5kg 口袋放 SVD 判 TooHeavy", heavyCode == ContainCode.TooHeavy, heavyCode.ToString());

        var carry = new Inventory(5.0f, 10, 10);
        carry.Add(new ItemStack(svd, 1));                 // 4.3kg
        carry.Add(new ItemStack(dagger, 1));              // 0.4kg
        var overCode = carry.CanHold(new ItemStack(backpackData, 1));   // 1.2kg 放不下了
        Check("负重只剩 0.3kg 时背包判 CannotSupport",
            overCode == ContainCode.CannotSupport, overCode.ToString());

        // 9. 拆分：有空格能拆，没空格拆不动
        var splitPocket = new Inventory(float.MaxValue, 4, 3);
        splitPocket.Add(new ItemStack(ammo, 60));
        var split = splitPocket.SplitStack(splitPocket.Stacks[0], 30);
        Check("4×3 口袋能拆出一叠（占 2 格）",
            split != null && splitPocket.UsedCells == 2 && splitPocket.CountOf("ammo_762x54r") == 60,
            $"占格 {splitPocket.UsedCells}");

        var jam = new Inventory(float.MaxValue, 1, 1);
        jam.Add(new ItemStack(ammo, 60));
        var jamSplit = jam.SplitStack(jam.Stacks[0], 30);
        Check("1×1 口袋塞满时拆分被拒（原叠不变）",
            jamSplit == null && jam.Stacks[0].Count == 60 && jam.UsedCells == 1);

        // 10. 移除会清格子
        splitPocket.RemoveFrom(split, split.Count);
        Check("拿走拆出的那一叠后释放格子", splitPocket.UsedCells == 1, $"占格 {splitPocket.UsedCells}");

        // 11. 嵌套下探：口袋塞满 → 塞进里面的背包
        var outer = new Inventory(float.MaxValue, 4, 3);
        var bag = new ItemStack(backpackData, 1);
        bag.EnsureContents();                       // 先建内容物口袋（Add 是移动语义，会新建一叠）
        outer.Add(bag);

        var bagStored = outer.Stacks[0];            // 口袋里实际存的那一份
        var bagPocket = bagStored.Contents;
        Check("背包内容物是 6×5 口袋",
            bagStored.IsContainer && bagPocket != null && bagPocket.CellCount == 30,
            $"{bagPocket?.GridW}×{bagPocket?.GridH}");

        // 把 4×3 的最后一列（3 格）用 3 张现金填满
        for (int i = 0; i < 3; i++) outer.Add(new ItemStack(cash, 1));
        Check("4×3 口袋已塞满（12 格）", outer.UsedCells == 12 && outer.FreeCells == 0, $"占格 {outer.UsedCells}");

        var code = outer.TryInsert(new ItemStack(bandage, 1), out var stored);
        Check("口袋满时递归塞进背包",
            code == ContainCode.Success && stored != null
            && bagPocket.CountOf("bandage") == 1 && outer.CountOf("bandage") == 0,
            $"{code} / 背包绷带 {bagPocket.CountOf("bandage")}");
        bool outerOk = outer.Validate(out string r2);
        bool bagOk = bagPocket.Validate(out string r3);
        Check("嵌套后两层占位表都自洽", outerOk && bagOk, $"{r2} {r3}");

        // 12. 循环防护
        var cycleCode = bagPocket.TryInsert(bagStored, out _);
        Check("背包放进自己的内容物被判循环", cycleCode == ContainCode.WouldCycle, cycleCode.ToString());

        // 13. 递归重量：背包自重 + 内容物重量
        Check("背包的 TotalWeight 含内容物（1.2 + 0.05）",
            Mathf.IsEqualApprox(bagStored.TotalWeight, 1.25f), $"{bagStored.TotalWeight:0.###}");

        // 14. 指定坐标放置（阶段 3/4 拖拽要用）
        var manual = new Inventory(float.MaxValue, 4, 3);
        var watch = db.GetItem("watch");
        var placed = manual.Place(new ItemStack(watch, 1), 2, 1, false);
        bool manualOk = manual.Validate(out string r4);
        Check("能放到指定格 (2,1)",
            placed && manual.UsedCells == 1 && manual.At(2, 1) == manual.Stacks[0] && manualOk,
            r4);
        Check("重叠位置放置被拒", !manual.Place(new ItemStack(ammo, 1), 2, 1, false));

        // 15. 拖拽：按格坐标搬运（阶段 3.5 / 4）
        var bagA = new Inventory(float.MaxValue, 4, 3);
        var bagB = new Inventory(float.MaxValue, 4, 3);
        var gun = new ItemStack(svd, 1);
        bagA.Add(gun);
        var moved = bagA.Stacks[0];

        ItemDrag.Begin(bagA, new List<ItemStack> { moved }, moved.GridX, moved.GridY);
        var placeCode = ItemDrag.TryPlaceAt(bagB, 0, 1);
        Check("跨口袋按格坐标搬运",
            placeCode == ContainCode.Success && bagB.At(0, 1) == moved && bagA.IsEmpty
            && moved.GridX == 0 && moved.GridY == 1,
            $"{placeCode} / {moved.GridX},{moved.GridY}");
        ItemDrag.End();

        // 16. 旋转落点：2×4 的口袋只能转着放
        var tallBag = new Inventory(float.MaxValue, 2, 4);
        ItemDrag.Begin(bagB, new List<ItemStack> { moved });
        bool rotOk = ItemDrag.CanPlaceAt(tallBag, 0, 0, out bool needRot) == ContainCode.Success;
        Check("放不下时判定会自动旋转", rotOk && needRot, $"rotated={needRot}");
        var rotCode = ItemDrag.TryPlaceAt(tallBag, 0, 0);
        Check("旋转搬运成功且占 8 格",
            rotCode == ContainCode.Success && moved.Rotated && tallBag.UsedCells == 8 && bagB.IsEmpty,
            $"{rotCode} / 占格 {tallBag.UsedCells}");
        ItemDrag.End();

        // 17. 被占用 → 拒绝，且两边都不变
        var occupied = new Inventory(float.MaxValue, 4, 3);
        occupied.Add(new ItemStack(gold, 1));                 // 2×1 at (0,0)
        int beforeA = tallBag.StackCount;
        ItemDrag.Begin(tallBag, new List<ItemStack> { moved });
        var blocked = ItemDrag.CanPlaceAt(occupied, 0, 0, out _);
        var blocked2 = ItemDrag.TryPlaceAt(occupied, 0, 0);
        Check("格子被别的物品占着时拒绝搬运",
            blocked == ContainCode.NoSpace && blocked2 != ContainCode.Success
            && tallBag.StackCount == beforeA && occupied.CountOf("svd") == 0,
            $"{blocked} / {blocked2}");
        ItemDrag.End();

        // 18. 同类堆叠 → 合并
        var ammoBag = new Inventory(float.MaxValue, 2, 2);
        ammoBag.Add(new ItemStack(ammo, 30));
        var other = new Inventory(float.MaxValue, 2, 2);
        other.Add(new ItemStack(ammo, 30));
        var otherStack = other.Stacks[0];
        ItemDrag.Begin(other, new List<ItemStack> { otherStack });
        var mergeCode = ItemDrag.TryPlaceAt(ammoBag, 0, 0);
        Check("落在同类堆叠上合并成一叠 60 发",
            mergeCode == ContainCode.Success && ammoBag.CountOf("ammo_762x54r") == 60
            && ammoBag.StackCount == 1 && other.IsEmpty && ammoBag.UsedCells == 1,
            $"{mergeCode} / {ammoBag.CountOf("ammo_762x54r")} 发 / {ammoBag.StackCount} 叠");
        ItemDrag.End();

        // 19. 同口袋内换格
        var oneBag = new Inventory(float.MaxValue, 4, 3);
        oneBag.Add(new ItemStack(cash, 1));
        var cashStack = oneBag.Stacks[0];
        ItemDrag.Begin(oneBag, new List<ItemStack> { cashStack }, 0, 0);
        var shiftCode = ItemDrag.TryPlaceAt(oneBag, 3, 2);
        Check("同一口袋里换格子",
            shiftCode == ContainCode.Success && oneBag.UsedCells == 1
            && cashStack.GridX == 3 && cashStack.GridY == 2,
            $"{shiftCode} / {cashStack.GridX},{cashStack.GridY}");
        ItemDrag.End();

        // 20. 口袋根本装不下 → TooBig
        var micro = new Inventory(float.MaxValue, 1, 1);
        ItemDrag.Begin(tallBag, new List<ItemStack> { moved });
        var tooBig = ItemDrag.CanPlaceAt(micro, 0, 0, out _);
        Check("1×1 口袋收到 4×2 判 TooBig", tooBig == ContainCode.TooBig, tooBig.ToString());
        ItemDrag.End();

        // 21. GridPanel 的落点管线：算格子 → 搬运 → 通知刷新
        var gridSrc = new Inventory(float.MaxValue, 4, 3);
        var gridDst = new Inventory(float.MaxValue, 4, 3);
        gridSrc.Add(new ItemStack(svd, 1));
        var dragged = gridSrc.Stacks[0];

        int changed = 0;
        var panel = new GridPanel { Pocket = gridDst, CellPx = 34, Changed = () => changed++ };
        AddChild(panel);
        panel.Rebuild();

        Vector2 At(int cx, int cy) => new(GridPanel.Pad + cx * 34 + 6, GridPanel.Pad + cy * 34 + 6);

        ItemDrag.Begin(gridSrc, new List<ItemStack> { dragged }, dragged.GridX, dragged.GridY);
        bool canDrop = panel.CanDropAt(At(0, 1));
        panel.DropAt(At(0, 1));
        Check("GridPanel 按鼠标格搬东西并通知刷新",
            canDrop && changed == 1 && gridDst.At(0, 1) == dragged && gridSrc.IsEmpty,
            $"canDrop={canDrop} changed={changed} @{dragged.GridX},{dragged.GridY}");

        // 合并：同类型落到已有那叠上
        var gridDst2 = new Inventory(float.MaxValue, 2, 2);
        gridDst2.Add(new ItemStack(ammo, 30));
        var panel2 = new GridPanel { Pocket = gridDst2, CellPx = 34 };
        AddChild(panel2);
        panel2.Rebuild();

        var ammoSrc = new Inventory(float.MaxValue, 2, 2);
        ammoSrc.Add(new ItemStack(ammo, 20));
        var ammoStack = ammoSrc.Stacks[0];
        ItemDrag.Begin(ammoSrc, new List<ItemStack> { ammoStack });
        panel2.CanDropAt(At(0, 0));
        panel2.DropAt(At(0, 0));
        Check("GridPanel 落在同类堆叠上合并",
            gridDst2.CountOf("ammo_762x54r") == 50 && ammoSrc.IsEmpty && gridDst2.UsedCells == 1,
            $"{gridDst2.CountOf("ammo_762x54r")} 发");

        panel.QueueFree();
        panel2.QueueFree();

        // 22. 集成：真玩家（基础口袋 4×3 + 装备槽里的背包 6×5）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");
        var player = playerScene.Instantiate<Player>();
        AddChild(player);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Check("玩家基础口袋 4×3 = 12 格",
            player.Pocket.GridW == 4 && player.Pocket.GridH == 3 && player.Pocket.CellCount == 12,
            $"{player.Pocket.GridW}×{player.Pocket.GridH}");

        var equippedBag = player.Equipment.Get(Player.ContainerSlot)?.Contents;
        Check("初始背包落在容器槽、内容物 6×5 = 30 格",
            equippedBag != null && equippedBag.CellCount == 30, $"{equippedBag?.CellCount}");

        Check("玩家总格数 = 12 + 30 = 42", player.TotalCellCapacity == 42, $"{player.TotalCellCapacity}");

        var pick = player.TryPickUp(new ItemStack(svd, 1), out var picked);
        Check("拾取 SVD 自动排进基础口袋（占 8 格）",
            pick == ContainCode.Success && picked != null
            && player.Pocket.CountOf("svd") == 1 && player.Pocket.UsedCells == 8,
            $"{pick} / 占格 {player.Pocket.UsedCells}");

        // 基础口袋只剩最后一排 4 格，3×3 背包放不下 → 递归塞进身上的背包
        var bagPick = player.TryPickUp(new ItemStack(backpackData, 1), out _);
        Check("基础口袋放不下时背包进装备容器",
            bagPick == ContainCode.Success && equippedBag.CountOf("backpack") == 1
            && equippedBag.UsedCells == 9 && player.Pocket.CountOf("backpack") == 0,
            $"{bagPick} / 背包内占格 {equippedBag.UsedCells}");

        // 全局负重上限
        player.MaxCarryWeight = 6.0f;
        var overWeight = player.TryPickUp(new ItemStack(cash, 1), out _);
        Check("超过全局负重判 CannotSupport", overWeight == ContainCode.CannotSupport, overWeight.ToString());

        player.MaxCarryWeight = player.TotalWeight + 1.0f;
        var quick = player.TryTake(new ItemStack(bandage, 1), out _);
        Check("TryTake 放进空格子最多的口袋",
            quick == ContainCode.Success && player.CountItem("bandage") == 1, quick.ToString());
        bool pocketOk = player.Pocket.Validate(out string rp);
        bool bagOk2 = equippedBag.Validate(out string rb);
        Check("拾取后各口袋占位表仍自洽", pocketOk && bagOk2, $"{rp} {rb}");

        // 23. 切武器时"枪的贴图层"要跟着藏 / 显（回归：切成匕首后枪一直挂手上）
        var gunVisual = player.GetNodeOrNull<WeaponVisual>("WeaponVisual");
        player.TrySetActiveSlot(Player.Slot2);
        Check("切到匕首时枪的贴图层隐藏",
            gunVisual != null && !gunVisual.Visible && !player.IsRangedActive,
            $"visible={gunVisual?.Visible} ranged={player.IsRangedActive}");

        player.TrySetActiveSlot(Player.Slot1);
        Check("切回枪时贴图层恢复",
            gunVisual != null && gunVisual.Visible && player.IsRangedActive,
            $"visible={gunVisual?.Visible} ranged={player.IsRangedActive}");

        // 24. 开火动画只跟"真射出一发"走（回归：没子弹了还冒火焰）
        var weaponNode = player.GetNodeOrNull<Weapon>("Weapon");
        var magSetter = typeof(Weapon).GetProperty("Magazine")!.GetSetMethod(true);
        var cooldownField = typeof(Weapon).GetField("_cooldown",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var shootLeftField = typeof(WeaponVisual).GetField("_shootLeft",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        magSetter!.Invoke(weaponNode, new object[] { 1 });
        cooldownField!.SetValue(weaponNode, 0.0f);
        bool firedOnce = weaponNode!.TryFire(player.GlobalPosition, Vector2.Right, 0.0f);
        Check("射出一发才播后坐 / 枪焰",
            firedOnce && gunVisual!.Shooting, $"fired={firedOnce} shooting={gunVisual?.Shooting}");

        shootLeftField!.SetValue(gunVisual, 0.0f);       // 清掉上一发的后坐
        magSetter.Invoke(weaponNode, new object[] { 0 });
        cooldownField.SetValue(weaponNode, 0.0f);
        bool dryFire = weaponNode.TryFire(player.GlobalPosition, Vector2.Right, 0.0f);
        Check("空仓开火不播后坐 / 枪焰",
            !dryFire && !gunVisual.Shooting, $"dry={dryFire} shooting={gunVisual.Shooting}");

        // 25. 配件检索：改装面板清单的来源（直接放的 + 嵌套背包里的，非配件不出现）
        var probeBag = new Inventory(float.MaxValue, 6, 5);
        probeBag.Add(new ItemStack(db.GetItem("silencer"), 1));
        probeBag.Add(new ItemStack(cash, 1));                    // 非配件 → 不该出现

        var innerBag = new ItemStack(backpackData, 1);
        innerBag.EnsureContents();
        probeBag.Add(innerBag);
        var innerStored = probeBag.Stacks[probeBag.Stacks.Count - 1];
        innerStored.EnsureContents()?.Add(new ItemStack(db.GetItem("tacgrip"), 1));

        var refs = AttachmentFinder.Collect(probeBag, "基础口袋");
        bool nestedOk = refs.Exists(r => ReferenceEquals(r.Stack, innerStored.Contents?.Stacks[0]));
        Check("配件检索：嵌套容器里的配件也能找到",
            refs.Count == 2 && nestedOk, $"找到 {refs.Count} 件，嵌套={nestedOk}");
        Check("配件检索：非配件不出现在清单里",
            !refs.Exists(r => r.Stack.Data.Id == "cash"));
        Check("配件检索：嵌套的会标注「在 X 里」",
            refs.Exists(r => !string.IsNullOrEmpty(r.Note) && r.Where == "基础口袋"),
            string.Join(" / ", refs.ConvertAll(r => $"{r.Stack.Data.Name}@{r.Where}:{r.Note}")));

        GD.Print($"=== 通过 {_pass}，失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok)
        {
            _pass++;
            GD.Print($"  [OK]   {name}");
        }
        else
        {
            _fail++;
            GD.Print($"  [FAIL] {name}   {detail}");
        }
    }
}
