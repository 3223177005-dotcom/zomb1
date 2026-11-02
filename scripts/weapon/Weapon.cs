using Godot;

/// <summary>
/// 武器：封装一把枪的弹药、开火模式、射速、换弹与开火逻辑。
/// 未来「改枪系统」会在此之上叠加属性修改（更换配件 → 修改 GunData 字段）。
/// </summary>
public partial class Weapon : Node2D
{
	[Signal] public delegate void FiredEventHandler(Vector2 from, Vector2 direction);
	[Signal] public delegate void AmmoChangedEventHandler(int magazine, int reserve);
	[Signal] public delegate void AmmoEmptyEventHandler();
	[Signal] public delegate void ReloadStartedEventHandler();
	[Signal] public delegate void ReloadFinishedEventHandler();
	[Signal] public delegate void FireModeChangedEventHandler(int mode);

	[Export] public GunData GunData { get; set; }
	[Export] public bool InfiniteReserve { get; set; } = true;   // 测试用：总弹药无限

	public int Magazine { get; private set; } = 0;
	private int _reserveAmmo = 0;

	/// <summary>
	/// 备弹数。
	///
	/// ★ **有口径的枪 = 实时查背包**，不是缓存值 ——
	/// 否则捡到弹药之后 HUD 还显示 0，看着像"枪认不出子弹"。
	/// 没有口径的枪才用内部字段（无限备弹/测试模式）。
	/// </summary>
	public int ReserveAmmo
	{
		get
		{
			if (GunData != null && !string.IsNullOrWhiteSpace(GunData.Caliber))
				return ResolvePlayer()?.CountItem(GunData.Caliber) ?? 0;

			return _reserveAmmo;
		}
		private set => _reserveAmmo = value;
	}
	public bool IsReloading { get; private set; } = false;

	/// <summary>当前开火模式（运行时状态，初始取自 GunData.DefaultMode）。</summary>
	public FireMode CurrentFireMode { get; private set; } = FireMode.None;

	private float _cooldown = 0.0f;
	private int _burstRemaining = 0;

	public override void _Ready()
	{
		if (GunData != null)
			ApplyGunData(GunData);
	}

	public override void _Process(double delta)
	{
		if (_cooldown > 0.0f)
			_cooldown = Mathf.Max(_cooldown - (float)delta, 0.0f);
	}

	/// <summary>
	/// 换装一把枪：重置弹匣 / 开火模式 / 点射状态，并广播信号。
	/// 「最小装备桥接」和将来的装备栏都通过它把物品数据装到武器上。
	/// </summary>
	public void ApplyGunData(GunData data)
	{
		GunData = data;
		_cooldown = 0.0f;
		_burstRemaining = 0;
		IsReloading = false;
		CurrentFireMode = ResolveDefaultMode(data);

		Magazine = data?.MagazineSize ?? 0;
		ReserveAmmo = data != null ? data.MagazineSize * 3 : 0;

		// 有口径的枪**不吃"无限备弹"** —— 弹药从背包里的实弹来
		if (!string.IsNullOrWhiteSpace(data?.Caliber))
		{
			InfiniteReserve = false;
			ReserveAmmo = 0;
		}

		EmitSignal(SignalName.FireModeChanged, (int)CurrentFireMode);
		EmitSignal(SignalName.AmmoChanged, Magazine, ReserveAmmo);
	}

	/// <summary>默认模式：优先 GunData.DefaultMode，若不支持则取支持列表里第一个。</summary>
	private static FireMode ResolveDefaultMode(GunData data)
	{
		if (data == null) return FireMode.None;
		if (FireModeInfo.Has(data.SupportedModes, data.DefaultMode)) return data.DefaultMode;
		return FireModeInfo.First(data.SupportedModes);
	}

	/// <summary>循环切换开火模式（按 B 键调用）。只有一种模式时不动作。</summary>
	public void CycleFireMode()
	{
		if (GunData == null) return;
		if (FireModeInfo.Count(GunData.SupportedModes) <= 1) return;

		CurrentFireMode = FireModeInfo.Next(GunData.SupportedModes, CurrentFireMode);
		_burstRemaining = 0;
		EmitSignal(SignalName.FireModeChanged, (int)CurrentFireMode);
	}

	/// <summary>
	/// 按当前开火模式处理扳机输入。
	/// triggerHeld = 按住；triggerJustPressed = 本帧刚按下。
	/// </summary>
	public void HandleTrigger(Vector2 muzzleGlobal, Vector2 aimDir, float spreadDegrees,
							  bool triggerHeld, bool triggerJustPressed)
	{
		if (GunData == null || IsReloading) return;

		switch (CurrentFireMode)
		{
			case FireMode.Auto:
				if (triggerHeld) TryFire(muzzleGlobal, aimDir, spreadDegrees);
				break;

			case FireMode.Semi:
			case FireMode.Bolt:
				if (triggerJustPressed) TryFire(muzzleGlobal, aimDir, spreadDegrees);
				break;

			case FireMode.Burst:
				if (triggerJustPressed && _burstRemaining <= 0)
					_burstRemaining = Mathf.Max(1, GunData.BurstCount);
				break;
		}

		// 点射推进：按射速一发一发打完
		if (_burstRemaining > 0 && _cooldown <= 0.0f)
		{
			if (TryFire(muzzleGlobal, aimDir, spreadDegrees))
			{
				_burstRemaining--;
				if (_burstRemaining <= 0 && GunData.BurstCooldown > 0.0f)
					_cooldown = GunData.BurstCooldown;
			}
		}
	}

	/// <summary>尝试开火。返回本帧是否真的射出一发。</summary>
	public bool TryFire(Vector2 muzzleGlobal, Vector2 aimDir, float spreadDegrees)
	{
		if (GunData == null) return false;
		if (IsReloading || _cooldown > 0.0f) return false;
		if (Magazine <= 0) return false;   // 空仓：不射击，也不重复提示

		Magazine--;
		_cooldown = 1.0f / Mathf.Max(GunData.FireRate, 0.01f);

		// 在散布锥内随机偏转一个角度
		float angle = aimDir.Angle() + Mathf.DegToRad((float)GD.RandRange(-spreadDegrees, spreadDegrees));
		var dir = Vector2.Right.Rotated(angle);
		DoHitscan(muzzleGlobal, dir);

		EmitSignal(SignalName.Fired, muzzleGlobal, dir);
		EventBus.Instance.EmitSignal(EventBus.SignalName.NoiseEmitted, muzzleGlobal, WeaponMods.NoiseRadius(WeaponMods.Active, GunData));  // 枪声吸引丧尸（配件可降噪）
		EmitSignal(SignalName.AmmoChanged, Magazine, ReserveAmmo);

		if (Magazine <= 0)
			EmitSignal(SignalName.AmmoEmpty);   // 刚打空这一弹匣，触发「弹药耗尽！」提示
		return true;
	}

	/// <summary>同步备弹显示（有口径的枪 = 背包里的实弹数，由 Player 调用）。</summary>
	public void SyncReserve(int count)
	{
		InfiniteReserve = false;
		ReserveAmmo = Mathf.Max(0, count);
		EmitSignal(SignalName.AmmoChanged, Magazine, ReserveAmmo);
	}

	/// <summary>持枪者（缓存一次；枪是玩家的子节点，用组查找最稳）。</summary>
	private Player _player;

	private Player ResolvePlayer()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player)) return _player;
		_player = GetTree()?.GetFirstNodeInGroup("player") as Player;
		return _player;
	}

	/// <summary>换弹（手动，按 R）。</summary>
	public async void Reload()
	{
		if (GunData == null || IsReloading) return;

		int maxSize = WeaponMods.MagazineSize(WeaponMods.Active, GunData);
		if (Magazine >= maxSize) return;

		// ★ 有口径 = 从背包扣实弹；没口径 = 老行为（无限备弹）
		string caliber = GunData.Caliber;
		bool usesAmmo = !string.IsNullOrWhiteSpace(caliber);

		int onHand = int.MaxValue;
		if (usesAmmo)
		{
			onHand = ResolvePlayer()?.CountItem(caliber) ?? 0;
			if (onHand <= 0)
			{
				EmitSignal(SignalName.AmmoEmpty);   // 没有对应弹药
				return;
			}
		}
		else if (!InfiniteReserve && ReserveAmmo <= 0) return;

		IsReloading = true;
		_burstRemaining = 0;   // 换弹打断点射
		EmitSignal(SignalName.ReloadStarted);
		await ToSignal(GetTree().CreateTimer(WeaponMods.ReloadTime(WeaponMods.Active, GunData)), SceneTreeTimer.SignalName.Timeout);

		int need = maxSize - Magazine;

		if (usesAmmo)
		{
			// 只装背包里有的，装多少算多少
			int take = Mathf.Min(need, onHand);
			int got = ResolvePlayer()?.ConsumeItem(caliber, take) ?? 0;
			Magazine += got;
			ReserveAmmo = ResolvePlayer()?.CountItem(caliber) ?? 0;
		}
		else if (InfiniteReserve)
		{
			Magazine += need;
		}
		else
		{
			int take = Mathf.Min(need, ReserveAmmo);
			Magazine += take;
			ReserveAmmo -= take;
		}

		IsReloading = false;
		EmitSignal(SignalName.ReloadFinished);
		EmitSignal(SignalName.AmmoChanged, Magazine, ReserveAmmo);
	}

	/// <summary>即时命中（hitscan）射线检测。</summary>
	private void DoHitscan(Vector2 muzzleGlobal, Vector2 dir)
	{
		// 命中判定从「持枪者身体中心」发出 —— 枪口只是视觉位置。
		// 原因：贴脸时敌人被碰撞体挤在约 14px 处，而枪口恰好在玩家前方 14px，
		// 正好落进敌人身体内部；射线若从枪口起步就会从目标体内出发，容易打空。
		var origin = GetParent() is Node2D holder ? holder.GlobalPosition : muzzleGlobal;
		var to = origin + dir * GunData.BulletRange;

		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(origin, to);
		query.HitFromInside = true;   // 起点若已在目标体内（完全贴脸）也要能命中
		if (GetParent() is CollisionObject2D parent)
			query.Exclude = new Godot.Collections.Array<Rid> { parent.GetRid() };

		var result = spaceState.IntersectRay(query);
		var hitPos = to;
		if (result.Count > 0)
		{
			hitPos = (Vector2)result["position"];
			if (result["collider"].AsGodotObject() is IDamageable damageable)
				damageable.TakeDamage(GunData.Damage);
		}

		// 弹道线仍然从枪口画起（视觉正确）
		SpawnTracer(muzzleGlobal, hitPos);
	}

	/// <summary>绘制一条短暂淡出的弹道线。</summary>
	private void SpawnTracer(Vector2 from, Vector2 to)
	{
		var line = new Line2D
		{
			Width = 1.5f,
			DefaultColor = new Color(1.0f, 0.85f, 0.35f, 0.9f)
		};
		line.AddPoint(from);
		line.AddPoint(to);

		var root = GetTree().CurrentScene;
		root?.AddChild(line);

		var tween = CreateTween();
		tween.TweenProperty(line, "modulate", new Color(1, 1, 1, 0), 0.06);
		tween.TweenCallback(Callable.From(line.QueueFree));
	}
}
