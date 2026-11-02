using Godot;
using System.Collections.Generic;

/// <summary>
/// 玩家：八方向移动 + 鼠标瞄准 + 射击（按开火模式）/ 近战 + 散布系统 + 生命值。
/// 搜打撤：死亡不重生，本局结束（未来替换为：丢失装备 → 结算界面 → 回藏身处）。
/// </summary>
public partial class Player : CharacterBody2D, IDamageable
{
    [Export] public float WalkSpeed { get; set; } = 240.0f;
    [Export] public float SprintSpeed { get; set; } = 400.0f;
    [Export] public float AimSpeedMultiplier { get; set; } = 0.5f;   // 瞄准时移动减速倍率

    /// <summary>
    /// 武器是否跟着**鼠标瞄准方向** 360° 旋转（泰拉瑞亚式）。
    /// false = 回到旧的「down / up / side 三方向」表现（回退开关）。
    /// </summary>
    [Export] public bool WeaponFollowAim360 { get; set; } = true;
    [Export] public float Acceleration { get; set; } = 2800.0f;
    [Export] public float Friction { get; set; } = 2800.0f;
    /// <summary>【暂未使用】保留给将来的「连续射击散布累积」。运动状态切换已改为立刻生效。</summary>
    [Export] public float SpreadBloomRate { get; set; } = 40.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;

    /// <summary>初始装备：槽位里放什么（物品 Id）。
    /// 将来接存档 / 藏身处仓库时只改这里。</summary>
    [Export] public string StartingWeapon1Id { get; set; } = "svd";
    [Export] public string StartingWeapon2Id { get; set; } = "dagger";

    /// <summary>初始装备的容器（放在容器槽）。
    /// **暂时默认带背包**，方便测试携带与 Tab 面板的背包显示；将来接存档时改回空。</summary>
    [Export] public string StartingContainerId { get; set; } = "backpack";

    /// <summary>玩家**裸身自带**的基础口袋格子（列 × 行）。装备容器各自有独立格子，在此之外叠加。</summary>
    [Export] public int BasePocketGridW { get; set; } = 4;
    [Export] public int BasePocketGridH { get; set; } = 3;

    /// <summary>
    /// 玩家负重上限（kg）。**全局只有这一份** —— 装备容器不会提高它；
    /// 容器自身的重量也会占掉其中的一份。
    /// </summary>
    [Export] public float MaxCarryWeight { get; set; } = 50.0f;

    /// <summary>装备栏槽位数量：2 个武器槽 + 1 个容器槽。</summary>
    public const int SlotCount = 3;

    /// <summary>槽位索引（0 基）：按键 1 / 2 切到前两个武器槽。</summary>
    public const int Slot1 = 0;
    public const int Slot2 = 1;
    public const int ContainerSlot = 2;

    /// <summary>
    /// 装备栏：槽位**不因武器类型区分** —— 远程和近战占用同一种槽，放哪个由玩家决定。
    /// 第 3 槽用来放容器（背包）。武器行为由物品自身的 WeaponData.Kind 决定，与槽位编号无关。
    /// </summary>
    public EquipmentSlots Equipment { get; } = new(SlotCount);

    /// <summary>玩家基础口袋（容器树的根，Owner == null）。格子 = <see cref="BasePocketGridW"/> × <see cref="BasePocketGridH"/>。</summary>
    public Inventory Pocket { get; private set; }

    /// <summary>当前实际散布（度，准星半角）。准星 UI 读取此值。</summary>
    public float CurrentSpread { get; private set; } = 0.0f;

    /// <summary>瞄准进度 0~1（腰射 → 瞄准的过渡，时长由 GunData.AimTime 控制）。</summary>
    private float _aimProgress = 0.0f;

    /// <summary>当前移动附加散布（立刻切换，不做过渡）。</summary>
    private float _moveBonus = 0.0f;

    public float Health { get; private set; } = 100.0f;
    public bool IsDead { get; private set; } = false;

    private Node2D _visual;
    private Marker2D _muzzle;
    private Weapon _weapon;
    private MeleeWeapon _meleeWeapon;
    private int _activeSlot = Slot1;

    /// <summary>
    /// 当前打开的 UI 面板数量（>0 时屏蔽开火）。
    /// 用计数而不是布尔：装备面板和地面拾取面板可能先后开合，布尔会互相踩。
    /// </summary>
    private int _uiOpenCount;

    private bool _uiBlocking => _uiOpenCount > 0;

    /// <summary>当前槽位索引（0 基）。</summary>
    public int ActiveSlot => _activeSlot;

    /// <summary>当前槽里的武器物品（空槽为 null）。</summary>
    public ItemStack ActiveWeaponItem => Equipment.Get(_activeSlot);

    /// <summary>当前手持武器的数据（空槽为 null）。</summary>
    public WeaponData ActiveWeaponData => ActiveWeaponItem?.Data?.WeaponStats;

    /// <summary>当前是否手持近战武器 —— 由**物品类型**决定，与槽位编号无关。</summary>
    public bool IsMeleeActive => ActiveWeaponData?.Kind == WeaponKind.Melee;

    /// <summary>当前手持武器名（HUD 读取）。</summary>
    public string CurrentWeaponName => ActiveWeaponItem?.Data?.Name ?? "—";

    /// <summary>当前开火模式（近战 / 空槽返回 None）。</summary>
    public FireMode CurrentFireMode
        => ActiveWeaponItem == null || IsMeleeActive
            ? FireMode.None
            : (_weapon?.CurrentFireMode ?? FireMode.None);

    /// <summary>
    /// 当前手持的是否**远程武器**（近战 / 空槽为 false）。
    /// HUD 用它决定**是否显示开火模式**，以及**是否显示弹药** —— 两者是同一个条件。
    /// </summary>
    public bool IsRangedActive => ActiveWeaponItem != null && !IsMeleeActive;

    public override void _Ready()
    {
        AddToGroup("player");
        Health = MaxHealth;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        _visual = GetNode<Node2D>("Visual");
        _muzzle = GetNode<Marker2D>("Visual/Muzzle");
        BuildSprite();        _weapon = GetNode<Weapon>("Weapon");
        _meleeWeapon = GetNodeOrNull<MeleeWeapon>("MeleeWeapon");

        if (_weapon != null)
        {
            _weapon.FireModeChanged += OnFireModeChanged;
            _weapon.Fired += OnWeaponFired;            // ★ 真射出子弹才播后坐 / 枪焰
            _weapon.ReloadStarted += OnReloadStarted;  // ★ 真开始换弹才播换弹动作
        }

        EventBus.Instance.UiPanelToggled += OnUiPanelToggled;

        // 基础口袋：容器树的根。重量上限走玩家全局账，所以这里只限格子。
        Pocket = new Inventory(float.MaxValue, BasePocketGridW, BasePocketGridH);

        EquipStartingLoadout();

        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, Health, MaxHealth);
        EmitWeaponInfo();
    }

    /// <summary>
    /// 离开场景树时断开 EventBus 连接。
    /// 必须做：EventBus 是常驻 autoload，不断开的话下一局会调用到已释放对象的方法。
    /// </summary>
    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.UiPanelToggled -= OnUiPanelToggled;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;
        float dt = (float)delta;
        HandleMovement(dt);
        HandleAimAndShoot(dt);

        // 开发辅助：Esc 显示鼠标，方便退出测试
        if (Input.IsActionJustPressed("pause"))
            Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    // ---------- 装备 ----------

    /// <summary>
    /// 初始装备：把两把测试武器放进两个武器槽，并把**当前槽**的武器数据装到武器节点上。
    /// （物品来源目前是硬编码的物品 Id；将来接存档 / 藏身处仓库时只改这里。）
    /// </summary>
    private void EquipStartingLoadout()
    {
        var db = ItemDatabase.Instance;
        if (db == null) return;

        var first = db.CreateStack(StartingWeapon1Id);
        if (first != null) Equipment.Equip(Slot1, first);

        var second = db.CreateStack(StartingWeapon2Id);
        if (second != null) Equipment.Equip(Slot2, second);

        // 容器：必须先建出内容物口袋，才能提供格子
        if (!string.IsNullOrWhiteSpace(StartingContainerId))
        {
            var container = db.CreateStack(StartingContainerId);
            if (container != null)
            {
                container.EnsureContents();
                Equipment.Equip(ContainerSlot, container);
            }
        }

        ApplyActiveWeapon();
    }

    // ---------- 携带（格子 + 全局负重） ----------
    //
    // ★ 规则：**每个口袋各管自己的格子，负重全局只有一份。**
    //   - 负重：基础口袋 + 装备栏里每一样（武器自重、容器自重、容器内容物），全局一份上限；
    //   - 格子：基础口袋 4×3、每个装备容器各自一块（由 ContainerData.GridW/GridH 决定）。
    //     装备栏里**物品自身**（SVD、匕首、背包本体）不占玩家的格子 —— 它们挂在装备栏上。

    /// <summary>
    /// 玩家当前总负重（kg）：基础口袋 + **装备栏里的一切**
    /// （武器自重、容器自重、容器内容物），**递归求和**。
    ///
    /// 重量的上限只有 <see cref="MaxCarryWeight"/> 一份，装备容器不会提高它。
    /// </summary>
    public float TotalWeight
    {
        get
        {
            float w = Pocket?.UsedWeight ?? 0.0f;

            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var equipped = Equipment.Get(i);
                if (equipped != null) w += equipped.TotalWeight;
            }
            return w;
        }
    }

    /// <summary>剩余可承重（kg）。</summary>
    public float FreeWeight => Mathf.Max(0.0f, MaxCarryWeight - TotalWeight);

    /// <summary>
    /// 玩家可及的**总格数** = 基础口袋 + 所有装备容器的格子。
    ///
    /// 注意：**不含装备栏物品自身占的格**（武器 / 容器本体挂在装备栏上，不占口袋）。
    /// </summary>
    public int TotalCellCapacity
    {
        get
        {
            int n = Pocket?.CellCount ?? 0;

            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var pocket = Equipment.Get(i)?.Contents;
                if (pocket != null) n += pocket.CellCount;
            }
            return n;
        }
    }

    /// <summary>
    /// 玩家可及的**已占格数** = 基础口袋用量 + 各装备容器**内容物**的用量。
    /// </summary>
    public int TotalUsedCells
    {
        get
        {
            int n = Pocket?.UsedCells ?? 0;

            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var pocket = Equipment.Get(i)?.Contents;
                if (pocket != null) n += pocket.UsedCells;
            }
            return n;
        }
    }

    /// <summary>全局负重预检（玩家唯一一份重量上限，装备容器不扩容）。</summary>
    private ContainCode CheckGlobalWeight(ItemStack it)
    {
        if (it.TotalWeight > MaxCarryWeight) return ContainCode.TooHeavy;
        if (TotalWeight + it.TotalWeight > MaxCarryWeight) return ContainCode.CannotSupport;
        return ContainCode.Success;
    }

    /// <summary>找一个装得下这叠东西的口袋（基础口袋优先）；找不到返回 null。不修改任何状态。</summary>
    private Inventory FindPocketFor(ItemStack it)
    {
        if (Pocket != null && Pocket.CanHold(it) == ContainCode.Success) return Pocket;

        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            var pocket = Equipment.Get(i)?.Contents;
            if (pocket != null && pocket.CanHold(it) == ContainCode.Success) return pocket;
        }
        return null;
    }

    /// <summary>
    /// 哪里都装不下时，挑一个**最能说明问题**的原因。
    ///
    /// 规则：只要存在「清一清就能放」的口袋，就报剩余量原因（玩家有办法解决）；
    /// 否则才报「永远放不下」的绝对原因（得换容器）。
    /// 对应 CDDA <c>item_contents::can_contain</c> 里优先取"清空后能装下"那条消息的做法。
    /// </summary>
    private ContainCode ExplainNoPocket(ItemStack it)
    {
        var absolute = ContainCode.Success;
        var remaining = ContainCode.Success;

        void Consider(Inventory pocket)
        {
            if (pocket == null) return;

            var code = pocket.CanHold(it);
            if (ContainCodeText.IsAbsolute(code))
            {
                if (absolute == ContainCode.Success) absolute = code;
            }
            else if (code != ContainCode.Success)
            {
                if (remaining == ContainCode.Success) remaining = code;
            }
        }

        Consider(Pocket);
        for (int i = 0; i < Equipment.SlotCount; i++) Consider(Equipment.Get(i)?.Contents);

        if (remaining != ContainCode.Success) return remaining;
        if (absolute != ContainCode.Success) return absolute;
        return ContainCode.NotAContainer;
    }

    /// <summary>
    /// 预检：这叠东西**能不能捡起来**（不修改任何状态）。
    /// 返回 <see cref="ContainCode.Success"/> 表示可以。
    /// </summary>
    public ContainCode CanPickUp(ItemStack it)
    {
        if (it?.Data == null || it.IsEmpty) return ContainCode.Success;

        var weight = CheckGlobalWeight(it);
        if (weight != ContainCode.Success) return weight;

        return FindPocketFor(it) != null ? ContainCode.Success : ExplainNoPocket(it);
    }

    /// <summary>
    /// 拾取 / 放入物品。
    ///
    /// ① 先算**全局负重** —— 这是玩家唯一的一份重量上限（装备容器不扩容）；
    /// ② 再依次尝试**基础口袋 → 各装备容器**（每个口袋各有自己的格子）。
    ///
    /// 成功时 <paramref name="stored"/> 是**实际存放的那一份**。
    /// </summary>
    /// <summary>
    /// 找**空格子最多**、而且装得下这件物品的口袋（基础口袋 + 各装备容器）。
    /// 找不到返回 null。
    /// </summary>
    public Inventory FindFattestPocketFor(ItemStack it)
    {
        if (it?.Data == null) return null;

        Inventory best = null;
        int bestFree = -1;

        void Consider(Inventory pocket)
        {
            if (pocket == null) return;
            if (pocket.CanHold(it) != ContainCode.Success) return;
            if (pocket.FreeCells <= bestFree) return;

            best = pocket;
            bestFree = pocket.FreeCells;
        }

        Consider(Pocket);
        for (int i = 0; i < Equipment.SlotCount; i++)
            Consider(Equipment.Get(i)?.Contents);

        return best;
    }

    /// <summary>
    /// **快速拿走**一件（双击战利品用）：和 <see cref="TryPickUp"/> 的区别是
    /// **不按"基础口袋优先"，而是放进空格子最多的那个口袋**。
    /// </summary>
    public ContainCode TryTake(ItemStack it, out ItemStack stored)
    {
        stored = null;
        if (it?.Data == null || it.IsEmpty) return ContainCode.Success;

        var weight = CheckGlobalWeight(it);
        if (weight != ContainCode.Success) return weight;

        var target = FindFattestPocketFor(it);
        if (target == null) return ExplainNoPocket(it);

        var code = target.TryInsert(it, out stored);
        if (code == ContainCode.Success) NotifyInventoryChanged();
        return code;
    }

    /// <summary>身上（基础口袋 + 各装备容器）一共有多少发某种弹药物品。</summary>
    public int CountItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;

        int total = Pocket?.CountOf(itemId) ?? 0;

        for (int i = 0; i < Equipment.SlotCount; i++)
            total += Equipment.Get(i)?.Contents?.CountOf(itemId) ?? 0;

        return total;
    }

    /// <summary>
    /// 从身上**扣除**最多 <paramref name="maxCount"/> 发某种弹药，返回实际扣掉的数量。
    /// 顺序：基础口袋 → 各装备容器。
    /// </summary>
    public int ConsumeItem(string itemId, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || maxCount <= 0) return 0;

        int consumed = 0;

        void ConsumeFrom(Inventory pocket)
        {
            if (pocket == null || consumed >= maxCount) return;

            // 先拷一份 —— 扣除会改动列表
            var stacks = new List<ItemStack>(pocket.Stacks);

            foreach (var stack in stacks)
            {
                if (consumed >= maxCount) break;
                if (stack?.Data == null || stack.Data.Id != itemId) continue;

                int take = Mathf.Min(stack.Count, maxCount - consumed);
                if (take <= 0) continue;

                pocket.RemoveFrom(stack, take);
                consumed += take;
            }
        }

        ConsumeFrom(Pocket);
        for (int i = 0; i < Equipment.SlotCount && consumed < maxCount; i++)
            ConsumeFrom(Equipment.Get(i)?.Contents);

        return consumed;
    }

    public ContainCode TryPickUp(ItemStack it, out ItemStack stored)
    {
        stored = null;
        if (it?.Data == null || it.IsEmpty) return ContainCode.Success;

        // ① 全局负重
        var weight = CheckGlobalWeight(it);
        if (weight != ContainCode.Success) return weight;

        // ② 基础口袋优先
        if (Pocket != null)
        {
            var code = Pocket.TryInsert(it, out stored);
            if (code == ContainCode.Success) { NotifyInventoryChanged(); return code; }
        }

        // ③ 再试各装备容器（背包里的空间）
        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            var pocket = Equipment.Get(i)?.Contents;
            if (pocket == null) continue;

            var code = pocket.TryInsert(it, out stored);
            if (code == ContainCode.Success) { NotifyInventoryChanged(); return code; }
        }

        return ExplainNoPocket(it);
    }

    /// <summary>身上东西变了 → 通知 UI（Tab 面板据此刷新，但**不能每帧刷**）。</summary>
    private void NotifyInventoryChanged()
        => EventBus.Instance?.EmitSignal(EventBus.SignalName.InventoryChanged);

    // ---------- 地面拾取（朝向优先） ----------

    /// <summary>交互半径（像素）。</summary>
    [Export] public float InteractRange { get; set; } = 64.0f;

    /// <summary>「朝向优先」的视锥角度（度）。落在这个角度内会被优先选中。</summary>
    [Export] public float InteractArcDegrees { get; set; } = 120.0f;

    /// <summary>用**当前准星方向**找拾取目标。</summary>
    public GroundStack FindPickupTarget(WorldItems items)
        => FindPickupTarget(items, GetAimDirection());

    /// <summary>
    /// 找拾取目标：交互半径内**朝 <paramref name="aimDirection"/> 方向优先**；
    /// 朝向内没有，才退化为全范围最近。
    ///
    /// 注意：我们的角色是**连续移动**的（不在格子里），所以这里按**像素距离**筛，
    /// 而不是 CDDA 的「我脚下那一格」。物品位置取所在格的中心。
    /// </summary>
    public GroundStack FindPickupTarget(WorldItems items, Vector2 aimDirection)
    {
        if (items == null) return default;

        var candidates = items.CollectNear(GlobalPosition, InteractRange);
        if (candidates.Count == 0) return default;

        if (aimDirection.LengthSquared() < 0.0001f) aimDirection = Vector2.Right;
        aimDirection = aimDirection.Normalized();

        float halfArc = Mathf.DegToRad(InteractArcDegrees * 0.5f);

        var best = default(GroundStack);
        float bestDist = float.MaxValue;

        // ① 先只看**朝向锥内**，取最近的
        foreach (var c in candidates)
        {
            var toItem = c.Position - GlobalPosition;

            // 正好站在同一格中心时没有方向可言，直接算命中
            if (toItem.LengthSquared() > 0.0001f)
            {
                float angle = Mathf.Abs(aimDirection.AngleTo(toItem.Normalized()));
                if (angle > halfArc) continue;
            }

            if (c.Distance < bestDist) { bestDist = c.Distance; best = c; }
        }
        if (best.IsValid) return best;

        // ② 朝向内没有 → 退化为全范围内最近
        foreach (var c in candidates)
            if (c.Distance < bestDist) { bestDist = c.Distance; best = c; }

        return best;
    }

    /// <summary>
    /// 从地上捡起一叠：按「朝向优先」选目标 → 放进身上 → 从地面移除。
    ///
    /// 附近没有东西时返回 <see cref="ContainCode.Success"/> 且 <paramref name="stored"/> 为 null。
    /// </summary>
    public ContainCode TryPickupFromGround(WorldItems items, out ItemStack stored)
    {
        stored = null;

        var target = FindPickupTarget(items);
        if (!target.IsValid) return ContainCode.Success;

        var code = TryPickUp(target.Stack, out stored);
        if (code != ContainCode.Success || stored == null) return code;

        // 整叠都进了身上 → 从地面容器移除
        items.GetCell(target.Cell)?.RemoveFrom(target.Stack, target.Stack.Count);
        items.PruneEmpty();

        InterruptAim();   // ★ 捡东西打断瞄准
        return code;
    }

    /// <summary>找拾取目标所在的**格子**（朝向优先）；没找到返回 (-1,-1)。</summary>
    public Vector2I FindPickupTargetCell(WorldItems items, Vector2 aimDirection)
    {
        var target = FindPickupTarget(items, aimDirection);
        return target.IsValid ? target.Cell : new Vector2I(-1, -1);
    }

    /// <summary>用当前准星方向找拾取目标格。</summary>
    public Vector2I FindPickupTargetCell(WorldItems items)
        => FindPickupTargetCell(items, GetAimDirection());

    /// <summary>
    /// 把某格里**某一类**物品尽量捡起（一叠一叠地捡，直到放不下为止）。
    /// 返回实际捡起的件数。
    /// </summary>
    public int TryPickupAllFromCell(WorldItems items, Vector2I cell, string itemId)
    {
        if (items == null || string.IsNullOrEmpty(itemId)) return 0;

        var inv = items.GetCell(cell);
        if (inv == null) return 0;

        int taken = 0;

        // 从后往前遍历：移除元素不会影响前面还没处理的下标
        for (int i = inv.Stacks.Count - 1; i >= 0; i--)
        {
            var stack = inv.Stacks[i];
            if (stack?.Data?.Id != itemId) continue;

            int count = stack.Count;
            var code = TryPickUp(stack, out var stored);
            if (code != ContainCode.Success || stored == null) break;

            inv.RemoveFrom(stack, count);
            taken += count;
        }

        items.PruneEmpty();

        if (taken > 0) InterruptAim();   // ★ 捡到东西才打断瞄准
        return taken;
    }

    /// <summary>
    /// 按**物品树的一行**捡起：把这一行涉及的堆叠逐个放进身上，放不下就停。
    /// 返回实际捡起的件数。
    ///
    /// 这一行可以在**任意嵌套层级**（例如"背包里的绷带"），
    /// 因为 <see cref="GroundNode.Parent"/> 就是它所在的那个容器，直接从那里面移除即可。
    ///
    /// 需要 <paramref name="items"/> 是为了在拿空后**清掉空的地面格**
    /// （和 <see cref="TryPickupFromGround"/> / <see cref="TryPickupAllFromCell"/> 保持一致）。
    /// </summary>
    public int TryPickupNode(WorldItems items, GroundNode node)
    {
        if (node?.Parent == null || node.Stacks.Count == 0) return 0;

        int taken = 0;

        // 从后往前：从 Parent 里移除元素不影响前面还没处理的下标
        for (int i = node.Stacks.Count - 1; i >= 0; i--)
        {
            var stack = node.Stacks[i];
            int count = stack.Count;

            var code = TryPickUp(stack, out var stored);
            if (code != ContainCode.Success || stored == null) break;

            node.Parent.RemoveFrom(stack, count);
            taken += count;
        }

        if (taken > 0)
        {
            items?.PruneEmpty();   // ★ 可能把某一格拿空了
            InterruptAim();        // ★ 捡到东西才打断瞄准
        }
        return taken;
    }

    /// <summary>
    /// 把当前槽里的武器数据应用到武器节点。
    /// 用哪个节点由**物品类型**决定：远程 → Weapon 节点，近战 → MeleeWeapon 节点。
    /// </summary>
    private void ApplyActiveWeapon()
    {
        switch (ActiveWeaponData)
        {
            case RangedWeaponData ranged when ranged.GunStats != null:
                // 同一把枪不重复应用，否则每次切槽都会把弹匣填满
                if (_weapon != null && !ReferenceEquals(_weapon.GunData, ranged.GunStats))
                    _weapon.ApplyGunData(ranged.GunStats);

                // 让开火逻辑知道"现在拿的是哪一把枪的实例"，卸/装配件后才能算出有效数值
                WeaponMods.Active = ActiveWeaponItem;

                // 有口径的枪：备弹数就是背包里的实弹数（HUD 显示用）
                if (_weapon != null && !string.IsNullOrWhiteSpace(ranged.GunStats.Caliber))
                {
                    _weapon.SyncReserve(CountItem(ranged.GunStats.Caliber));
                }
                break;

            case MeleeWeaponData melee when melee.MeleeStats != null:
                if (_meleeWeapon != null)
                    _meleeWeapon.MeleeData = melee.MeleeStats;
                break;
        }

        // ★ 枪的贴图是独立的叠层（资源包里没有刀的贴图）：
        //   换成近战 / 空手时必须**把这一层藏起来**，否则上一把枪会一直挂在手上。
        if (_weaponVisual != null)
        {
            _weaponVisual.Visible = IsRangedActive;
            // CDDA 素材：按物品 id 换枪贴图（weapons_cdda/<id>.png，纯枪不带手）
            _weaponVisual.SetGunItem(IsRangedActive ? ActiveWeaponItem?.Data?.Id : null);
        }

        // 切武器的手感反馈：近战偏暖色（注意 _visual 是隐藏的色块节点，
        // 真正显示的是 _sprite，所以必须调 _sprite 的 Modulate）
        if (_sprite != null)
            _sprite.Modulate = IsMeleeActive ? new Color(1.0f, 0.92f, 0.72f) : Colors.White;
    }

    /// <summary>
    /// 装备栏变了（装上 / 卸下 / 丢地上）之后，把「手上那把」的状态重算一遍。
    ///
    /// 场景：把正在拿的枪丢在地上、或者往正在用的槽里换一把别的武器 ——
    /// 这时候槽位没变，但内容和类型可能全变了，必须重算数据和**枪的贴图层可见性**。
    /// </summary>
    public void RefreshActiveWeapon()
    {
        ApplyActiveWeapon();
        EmitWeaponInfo();
    }

    /// <summary>当前手持武器的移动速度倍率（没有武器数据时按 1 处理）。</summary>
    private float ActiveWeaponMoveMultiplier
    {
        get
        {
            var data = ActiveWeaponData;
            return data != null && data.MoveSpeedMultiplier > 0.0f ? data.MoveSpeedMultiplier : 1.0f;
        }
    }

    // ---------- 移动 ----------

    private void HandleMovement(float delta)
    {
        var inputDir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        bool isSprinting = Input.IsActionPressed("sprint") && inputDir != Vector2.Zero;
        bool isAiming = Input.IsActionPressed("aim");

        float speed = isSprinting ? SprintSpeed : WalkSpeed;
        if (isAiming) speed *= AimSpeedMultiplier;
        speed *= ActiveWeaponMoveMultiplier;   // 手持武器自身的移速倍率

        // ★ 近战出手期间**定住不动** —— 走 A 手感：打一下就停，打完才能继续走
        if (_punchLeft > 0.0f)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        var targetVelocity = inputDir * speed;
        if (inputDir != Vector2.Zero)
            Velocity = Velocity.MoveToward(targetVelocity, Acceleration * delta);
        else
            Velocity = Velocity.MoveToward(Vector2.Zero, Friction * delta);

        MoveAndSlide();
    }

    // ---------- 瞄准 / 开火 ----------

    private void HandleAimAndShoot(float delta)
    {
        var aimDir = GetAimDirection();
        _visual.Rotation = aimDir.Angle();

        HandleWeaponSwitch();

        if (IsMeleeActive)
            HandleMelee(aimDir, delta);
        else
            HandleRanged(aimDir, delta);
    }

    /// <summary>1 / 2 键切换武器槽。</summary>
    private void HandleWeaponSwitch()
    {
        if (Input.IsActionJustPressed("slot_1"))
            TrySetActiveSlot(Slot1);
        else if (Input.IsActionJustPressed("slot_2"))
            TrySetActiveSlot(Slot2);
    }

    /// <summary>
    /// 切换当前武器槽。键位 1 / 2 与面板里的鼠标点击都走这里。
    /// 空槽不能切过去；返回是否切换成功（已在目标槽也算成功）。
    /// </summary>
    public bool TrySetActiveSlot(int slot)
    {
        if (!Equipment.IsValidSlot(slot)) return false;
        if (Equipment.IsEmpty(slot)) return false;
        // 只有武器能手持 —— 容器槽里的背包切不过去
        if (Equipment.Get(slot)?.Data?.WeaponStats == null) return false;

        // ★ 即使槽位没变也要重算一次：往**正在用的槽**里换一把别的武器时，
        //   槽号一样但内容变了，不刷就会留着上一把的枪模型 / 开火数据。
        _activeSlot = slot;
        ApplyActiveWeapon();   // 里面已经包含"藏枪 / 近战染色"的视觉更新
        EmitWeaponInfo();
        return true;
    }

    private void OnUiPanelToggled(bool open)
        => _uiOpenCount = Mathf.Max(0, _uiOpenCount + (int)(open ? 1 : -1));

    // ---------- 角色精灵动画 ----------

    private Sprite2D _sprite;

    /// <summary>武器叠层（枪 + 枪口火焰）。</summary>
    private WeaponVisual _weaponVisual;
    private float _animTime;
    private string _animDir = "";
    private string _animName = "";
    private Vector2 _lastDir = Vector2.Down;

    /// <summary>攻击朝向锁定还剩多久（秒）—— 这段时间内移动不会覆盖朝向。</summary>
    private float _faceLock;

    /// <summary>是否正在近战出手（这段时间内无法移动）。</summary>
    public bool IsMeleeAttacking => _punchLeft > 0.0f;

    /// <summary>近战挥拳动画还要播多久（秒）。</summary>
    private float _punchLeft;
    private bool _oneShot;

    private static readonly System.Collections.Generic.Dictionary<string, int> PlayerAnimFrames =
        new() { { "idle", 6 }, { "run", 6 }, { "punch", 4 }, { "pick_up", 3 } };

    private const float PlayerAnimFps = 10.0f;

    /// <summary>建角色精灵，并把原来的色块（<c>Visual</c> 的绘制部分）藏起来。</summary>
    private void BuildSprite()
    {
        if (_visual != null) _visual.Visible = false;

        _sprite = new Sprite2D { Name = "Sprite", Centered = true };
        // 世界格已升到 32px：角色贴图仍是 16px 时代画的，先 2× 最近邻放大占位
        // （等重绘 2× 帧图后，把 Scale 改回 1）
        _sprite.Scale = new Vector2(2.0f, 2.0f);
        AddChild(_sprite);

        SetPlayerAnim("down", "idle");

        _weaponVisual = new WeaponVisual { Name = "WeaponVisual" };
        AddChild(_weaponVisual);
    }

    private void SetPlayerAnim(string direction, string action, bool oneShot = false)
    {
        if (_sprite == null) return;
        if (_animDir == direction && _animName == action && _oneShot == oneShot) return;

        int frames = PlayerAnimFrames.TryGetValue(action, out int n) ? n : 6;
        var tex = GD.Load<Texture2D>($"res://assets/sprites/characters/player/{direction}_{action}.png");

        if (tex == null)
        {
            tex = GD.Load<Texture2D>($"res://assets/sprites/characters/player/{direction}_idle.png");
            if (tex == null) return;
            frames = PlayerAnimFrames["idle"];
            action = "idle";
        }

        _animDir = direction;
        _animName = action;
        _animTime = 0.0f;
        _oneShot = oneShot;

        _sprite.Texture = tex;
        _sprite.Hframes = frames;
        _sprite.Frame = 0;
        _sprite.Offset = new Vector2(0, -tex.GetHeight() / (float)frames * 0.5f + 3.0f);
        // Offset 取整：半像素偏移会让最近邻采样出现大小不一的像素块
        _sprite.Offset = new Vector2(Mathf.Round(_sprite.Offset.X), Mathf.Round(_sprite.Offset.Y));
    }

    /// <summary>
    /// **立刻面朝某个方向**（攻击时用：角色转向攻击方向，而不是移动方向）。
    /// 只改朝向缓存，真正的贴图切换由 UpdatePlayerAnimation 下一帧完成。
    /// </summary>
    public void FaceAim(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f) return;
        _lastDir = direction;
        _faceLock = 0.25f;      // 0.25 秒内移动不会把朝向抢回去
    }

    /// <summary>近战攻击时播一次挥拳动画（4 帧 ÷ 10fps = 0.4 秒）。</summary>
    public void PlayPunch()
    {
        _punchLeft = 4.0f / PlayerAnimFps;

        // ★ 清掉动画缓存，强制下一次 UpdatePlayerAnimation 真的切到 punch。
        //   否则"同方向连续挥拳"会被 SetPlayerAnim 的"参数没变就不重来"吞掉。
        _animDir = "";
        _animName = "";
        _oneShot = false;
    }

    private void UpdatePlayerAnimation(float delta)
    {
        if (_sprite == null) return;

        var v = Velocity;
        bool moving = v.LengthSquared() > 4.0f;

        // ★ 攻击朝向上锁期间，移动不覆盖朝向
        if (_faceLock > 0.0f) _faceLock -= delta;
        else if (moving) _lastDir = v;

        bool flip = false;
        string direction;
        if (Mathf.Abs(_lastDir.X) >= Mathf.Abs(_lastDir.Y)) { direction = "side"; flip = _lastDir.X < 0.0f; }
        else direction = _lastDir.Y > 0.0f ? "down" : "up";

        // ★ 挥拳中：播一次性攻击动画
        if (_punchLeft > 0.0f)
        {
            _punchLeft -= delta;
            SetPlayerAnim(direction, "punch", true);
        }
        else
        {
            SetPlayerAnim(direction, moving ? "run" : "idle", false);
        }

        _sprite.FlipH = flip;

        if (_weaponVisual != null)
        {
            if (WeaponFollowAim360)
            {
                // 武器脱离「角色朝向」，改成绕握持点自由旋转（泰拉瑞亚式）
                _weaponVisual.Style = WeaponVisual.AimStyle.Rotate360;
                _weaponVisual.AimAt(GetAimDirection());
            }
            else
            {
                // 旧表现：按 down / up / side 三方向贴图 + 固定偏移
                _weaponVisual.Style = WeaponVisual.AimStyle.Directional;
                // ★ 角色精灵自己有 Offset（脚底对齐用的），武器层必须跟着它走，
                //   否则枪会以玩家原点为基准、和人物错位。
                //   角色精灵是 2× 缩放的（32px 世界），所以这个世界坐标也要 ×2。
                _weaponVisual.Position = _sprite.Offset * 2.0f;
                _weaponVisual.SetDirection(direction);
                _weaponVisual.SetFlip(flip);
            }
        }

        _animTime += delta;
        if (_sprite.Hframes > 0)
        {
            int raw = (int)(_animTime * PlayerAnimFps);
            _sprite.Frame = _oneShot ? Mathf.Min(raw, _sprite.Hframes - 1) : raw % _sprite.Hframes;
        }
    }

    public override void _Process(double delta)
    {
        UpdatePlayerAnimation((float)delta);
    }

    /// <summary>B 键循环切换开火模式（只有一种模式时不动作）。</summary>
    private void HandleFireModeSwitch()
    {
        if (Input.IsActionJustPressed("fire_mode"))
            _weapon?.CycleFireMode();
    }

    /// <summary>近战：左键挥出扇形（方向朝鼠标）。</summary>
    private void HandleMelee(Vector2 aimDir, float delta)
    {
        // 近战不受散布影响：直接清零并复位瞄准状态
        CurrentSpread = 0.0f;
        _aimProgress = 0.0f;
        _moveBonus = 0.0f;

        if (_meleeWeapon == null || _meleeWeapon.MeleeData == null) return;

        // ★ 挥拳动画只在**真正出手那一帧**播。
        //   放在按键判断外会每帧重置，导致 _animTime 永远归零、画面卡在第 0 帧。
        if (!_uiBlocking && Input.IsActionJustPressed("shoot"))
        {
            FaceAim(aimDir);                        // ★ 转向攻击方向
            PlayPunch();
            _meleeWeapon.TryAttack(GlobalPosition, aimDir);
        }
    }

    /// <summary>远程：散布 + 按开火模式射击 + 换弹。</summary>
    private void HandleRanged(Vector2 aimDir, float delta)
    {
        // ★ 必须**手上真的有远程武器**才走这条 —— 否则把枪丢地上之后，
        //   Weapon 节点里还留着上一把枪的数据，会变成"空手开枪"。
        if (!IsRangedActive) return;
        if (_weapon == null || _weapon.GunData == null) return;

        // ★ 开火瞬间转向攻击方向（按住不放只转第一发，不会和移动抢朝向）
        if (!_uiBlocking && Input.IsActionJustPressed("shoot")) FaceAim(aimDir);

        // ★ 按住开火期间**持续**朝准星（否则边退边打会转回去）
        if (!_uiBlocking && Input.IsActionPressed("shoot")) FaceAim(aimDir);

        // ★ 注意：**后坐 / 枪口火焰不在这里播** —— 那是由 Weapon 真射出子弹后发的
        //   Fired 信号驱动的（见 OnWeaponFired）。以前按输入播，会出现
        //   "没子弹了还冒火 / 冷却中还在抖" 的假动作。

        // 更新散布（瞄准过渡由 AimTime 控制，运动状态附加立刻切换）
        UpdateSpread(delta);

        HandleFireModeSwitch();

        // 开火：用哪种触发方式由 Weapon 依据 CurrentFireMode 决定
        // UI 面板打开时屏蔽开火（面板里的左键点击不该同时射击）
        if (!_uiBlocking)
        {
            _weapon.HandleTrigger(
                _muzzle.GlobalPosition,
                aimDir,
                CurrentSpread,
                Input.IsActionPressed("shoot"),
                Input.IsActionJustPressed("shoot"));
        }

        // 换弹（动作动画同样由 ReloadStarted 信号驱动，见 OnReloadStarted）
        if (Input.IsActionJustPressed("reload"))
            _weapon.Reload();
    }

    /// <summary>**真的射出一发**才播后坐 + 枪口火焰（空仓 / 冷却中 / 换弹中都不会播）。</summary>
    private void OnWeaponFired(Vector2 from, Vector2 direction)
        => _weaponVisual?.PlayShoot();

    /// <summary>**真的开始换弹**才播换弹动作（弹匣已满 / 没备弹时 Weapon 不会发这个信号）。</summary>
    private void OnReloadStarted()
    {
        if (_weaponVisual == null || _weapon?.GunData == null) return;
        _weaponVisual.PlayReload(WeaponMods.ReloadTime(WeaponMods.Active, _weapon.GunData));
    }

    private Vector2 GetAimDirection()
    {
        var toMouse = GetGlobalMousePosition() - GlobalPosition;
        if (toMouse.LengthSquared() < 0.001f)
            return Vector2.Right;
        return toMouse.Normalized();
    }

    /// <summary>
    /// 更新当前散布，分两部分：
    /// 1) 基础散布 = 腰射 ↔ 瞄准 按瞄准进度插值，过渡时长由 GunData.AimTime 精确控制
    ///    —— 静止时按下右键，AimTime 秒后准星收至最小（= AimSpread）；
    /// 2) 运动状态（静止 / 移动 / 冲刺）的附加散布：**立刻切换，不做过渡**。
    /// </summary>
    private void UpdateSpread(float delta)
    {
        var g = _weapon.GunData;

        bool isAiming = Input.IsActionPressed("aim");
        var inputDir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        bool moving = inputDir != Vector2.Zero;
        bool sprinting = Input.IsActionPressed("sprint") && moving;

        // 1) 瞄准进度：唯一保留的过渡，按住右键按 AimTime 收回，松开按同样速率放回
        float step = g.AimTime > 0.001f ? delta / g.AimTime : 1.0f;
        _aimProgress = Mathf.MoveToward(_aimProgress, isAiming ? 1.0f : 0.0f, step);

        // 2) 运动状态附加散布：立刻切换
        _moveBonus = sprinting ? g.SprintSpreadBonus : (moving ? g.MoveSpreadBonus : 0.0f);

        // 3) 合计
        RecomputeSpread();
    }

    /// <summary>按当前「瞄准进度 + 运动附加」重算 <see cref="CurrentSpread"/>（公式只此一处）。</summary>
    private void RecomputeSpread()
    {
        var g = _weapon?.GunData;
        if (g == null || IsMeleeActive)
        {
            CurrentSpread = 0.0f;
            return;
        }

        CurrentSpread = Mathf.Lerp(
            WeaponMods.Inaccuracy(WeaponMods.Active, g),
            WeaponMods.AimInaccuracy(WeaponMods.Active, g),
            _aimProgress) + _moveBonus;
    }

    /// <summary>
    /// **打断瞄准** —— 把瞄准进度清零（对应 CDDA 拾取时的 <c>recoil = MAX_RECOIL</c>）。
    ///
    /// 效果：刚收拢的准星立刻绽开回腰射散布，必须重新花 AimTime 收回。
    /// 这样「弯腰捡东西」就有代价了 —— 逼你在「贪装备」和「保持枪口指向」之间做选择。
    /// </summary>
    public void InterruptAim()
    {
        _aimProgress = 0.0f;
        RecomputeSpread();   // 立刻生效，不等下一物理帧
    }

    // ---------- HUD 信息 ----------

    private void OnFireModeChanged(int mode) => EmitWeaponInfo();

    /// <summary>把当前手持武器的信息广播给 HUD（切武器 / 换模式时调用）。</summary>
    private void EmitWeaponInfo()
    {
        EventBus.Instance.EmitSignal(
            EventBus.SignalName.WeaponInfoChanged,
            CurrentWeaponName,
            (int)CurrentFireMode,
            IsRangedActive);
    }

    // ---------- 受伤 / 死亡 ----------

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health -= amount;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, Health, MaxHealth);
        if (Health <= 0.0f)
            Die();
    }

    /// <summary>死亡：搜打撤不重生。本局结束，短暂停留后回主菜单。</summary>
    private async void Die()
    {
        IsDead = true;
        // 注意：真正显示的是 _sprite（_visual 那个色块节点在 BuildSprite 里已经隐藏了）
        if (_sprite != null) _sprite.Modulate = new Color(0.5f, 0.2f, 0.2f, 1.0f);   // 死亡变色
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDied);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        SceneManager.Instance.GotoMainMenu();
    }
}
