using Godot;

/// <summary>
/// 敌人基类：视野/听觉寻敌 + 寻路追击 + 近战攻击 + 受击死亡。
/// 未来多种丧尸/怪物：继承本类覆盖行为，或用不同 EnemyData 配置数值。
/// </summary>
public partial class Enemy : CharacterBody2D, IDamageable
{
	[Export] public EnemyData EnemyData { get; set; }
	[Export] public float SeparationRadius { get; set; } = 44.0f;    // 与其它丧尸的分离半径（像素）
	[Export] public float SeparationStrength { get; set; } = 100.0f;  // 分离力强度

	public float Health { get; private set; } = 0.0f;

	private bool _aggroed = false;   // 是否已发现玩家（发现后持续追击）
	private Node2D _player = null;
	private Vector2[] _path = System.Array.Empty<Vector2>();
	private int _pathIndex = 0;
	private float _repathTimer = 0.0f;
	private float _attackCooldown = 0.0f;
	private Node2D _visual;

	// ---------- 精灵动画 ----------
	private Sprite2D _sprite;
	private float _animTime;
	private string _animDir = "";
	private string _animName = "";
	private static readonly float AnimFps = 8.0f;

	private static readonly string[] AnimDirs = { "down", "up", "side" };

	/// <summary>每种动画的帧数（和切出来的 PNG 一致）。</summary>
	private static readonly System.Collections.Generic.Dictionary<string, int> AnimFrames =
		new()
		{
			{ "idle", 6 }, { "walk", 8 }, { "first_attack", 7 }, { "second_attack", 9 }, { "first_death", 6 },
		};

	public override void _Ready()
	{
		AddToGroup("enemy");
		if (EnemyData != null)
			Health = EnemyData.MaxHealth;
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_visual = GetNode<Node2D>("Visual");
		BuildSprite();
		EventBus.Instance.NoiseEmitted += OnNoiseHeard;
		ApplyStateVisual();
	}

	/// <summary>
	/// 建一个 <see cref="Sprite2D"/> 来显示切片好的丧尸动画，
	/// 并把原来的色块（<c>Visual</c>）藏起来。
	/// </summary>
	private void BuildSprite()
	{
		_sprite = new Sprite2D { Name = "Sprite", Centered = true };
		// 世界格 32px：16px 时代的老帧图先 2× 放大占位
		_sprite.Scale = new Vector2(2.0f, 2.0f);
		AddChild(_sprite);

		if (_visual != null) _visual.Visible = false;

		SetAnim("down", "idle");
	}

	/// <summary>换动画（方向 + 动作）。同一个组合重复调用不会重新加载贴图。</summary>
	private void SetAnim(string direction, string action, bool oneShot = false)
	{
		if (_sprite == null) return;
		if (_animDir == direction && _animName == action && _oneShot == oneShot) return;

		int frames = AnimFrames.TryGetValue(action, out int n) ? n : 8;
		string path = $"res://assets/sprites/characters/zombie/{direction}_{action}.png";
		var tex = GD.Load<Texture2D>(path);

		if (tex == null)
		{
			// 该方向没这个动画（例如 up 没有 death）—— 退回同一方向的 walk
			path = $"res://assets/sprites/characters/zombie/{direction}_walk.png";
			tex = GD.Load<Texture2D>(path);
			if (tex == null) return;
			frames = AnimFrames["walk"];
			action = "walk";
		}

		_animDir = direction;
		_animName = action;
		_animTime = 0.0f;
		_oneShot = oneShot;

		_sprite.Texture = tex;
		_sprite.Hframes = frames;
		_sprite.Frame = 0;

		// 脚底对齐：让精灵底部压在节点原点上，看起来是"站在地上"
		_sprite.Offset = new Vector2(0, Mathf.Round(-tex.GetHeight() / (float)frames * 0.5f + 4.0f));
	}

	/// <summary>攻击动画还要播多久（秒）；&gt;0 时覆盖走路/待机动画。</summary>
	private float _attackAnimLeft;
	private bool _oneShot;

	/// <summary>受击闪红还要持续多久（秒）。</summary>
	private float _hitFlashLeft;

	/// <summary>待执行的受击击退位移（在物理帧里 MoveAndCollide，避免穿墙）。</summary>
	private Vector2 _knockback;

	/// <summary>受击击退距离（像素）。</summary>
	private const float KnockbackDistance = 10.0f;

	/// <summary>受击硬直还剩多久（秒）—— 硬直期间不动、不追、不攻击。</summary>
	private float _stunLeft;

	/// <summary>受击硬直时长（秒）。</summary>
	private const float StunTime = 0.15f;

	/// <summary>受击闪烁总时长。</summary>
	private const float HitFlashTime = 0.20f;

	/// <summary>开头这段是**过曝纯白**，之后转红。</summary>
	private const float HitFlashWhiteTime = 0.07f;

	/// <summary>根据移动方向 / 状态选动画并推进帧。</summary>
	private void UpdateAnimation(float delta)
	{
		if (_sprite == null) return;

		var v = Velocity;
		bool moving = v.LengthSquared() > 4.0f;

		string direction;
		bool flip = false;

		if (moving)
		{
			if (Mathf.Abs(v.X) >= Mathf.Abs(v.Y)) { direction = "side"; flip = v.X < 0.0f; }
			else direction = v.Y > 0.0f ? "down" : "up";
		}
		else
		{
			// 站着不动：面朝玩家（有玩家时），否则保持上一次朝向
			var to = _player != null ? _player.GlobalPosition - GlobalPosition : Vector2.Zero;
			if (to.LengthSquared() > 1.0f)
			{
				if (Mathf.Abs(to.X) >= Mathf.Abs(to.Y)) { direction = "side"; flip = to.X < 0.0f; }
				else direction = to.Y > 0.0f ? "down" : "up";
			}
			else direction = _animDir;
		}

		// ★ 攻击中：播一次性攻击动画，播完再回到走路/待机
		if (_attackAnimLeft > 0.0f)
		{
			_attackAnimLeft -= delta;
			SetAnim(direction, "first_attack", true);
		}
		else
		{
			SetAnim(direction, moving ? "walk" : "idle", false);
		}

		_sprite.FlipH = flip;

		_animTime += delta;
		if (_sprite.Hframes > 0)
		{
			int raw = (int)(_animTime * AnimFps);
			_sprite.Frame = _oneShot ? Mathf.Min(raw, _sprite.Hframes - 1) : raw % _sprite.Hframes;
		}

		// ★ 受击闪烁：先过曝成**纯白**（0.04 秒），再**泛红**（剩下的时间）
		if (_hitFlashLeft > 0.0f)
		{
			_hitFlashLeft -= delta;

			bool whitePhase = _hitFlashLeft > HitFlashTime - HitFlashWhiteTime;
			_sprite.Modulate = whitePhase
				? new Color(2.5f, 2.5f, 2.5f)      // >1 过曝 = 纯白
				: new Color(1.0f, 0.30f, 0.30f);   // 泛红
		}
		else
		{
			_sprite.Modulate = Colors.White;
		}
	}

	/// <summary>
	/// 离开场景树时断开 EventBus 连接。
	/// 必须做：EventBus 是常驻 autoload，若不断开，丧尸被销毁后 EventBus 上
	/// 仍留有指向已释放对象的委托，之后每次发射 NoiseEmitted 都会抛 ObjectDisposedException。
	/// </summary>
	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
			EventBus.Instance.NoiseEmitted -= OnNoiseHeard;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (EnemyData == null) return;
		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			return;
		}

		float dt = (float)delta;
		_attackCooldown = Mathf.Max(_attackCooldown - dt, 0.0f);
		UpdateAnimation(dt);

		// ★ 执行待处理的击退（MoveAndCollide 会正确地被墙挡住）
		if (_knockback != Vector2.Zero)
		{
			MoveAndCollide(_knockback);
			_knockback = Vector2.Zero;
		}

		// ★ 硬直：这段时间站着挨打，不追不打
		if (_stunLeft > 0.0f)
		{
			_stunLeft -= dt;
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		if (!_aggroed)
		{
			// 未发现玩家：原地待机，持续用视野检测
			Velocity = Vector2.Zero;
			UpdateVision();
		}
		else
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
			if (dist <= EnemyData.AttackRange)
			{
				Velocity = Vector2.Zero;
				TryAttack();
			}
			else
			{
				UpdateChaseVelocity(dt);
			}
		}

		// 与其它丧尸保持间距：既碰撞、又互相推开，避免挤成一团或原地抖动
		ApplySeparation();

		if (Velocity.LengthSquared() > 1.0f)
			_visual.Rotation = Velocity.Angle();
		MoveAndSlide();
	}

	/// <summary>视野检测：玩家在视野范围内且无遮挡 → 发现玩家。</summary>
	private void UpdateVision()
	{
		if (_player == null) return;
		if (GlobalPosition.DistanceTo(_player.GlobalPosition) <= EnemyData.VisionRange && HasLineOfSightToPlayer())
			SetAggroed(true);
	}

	/// <summary>听觉：听到枪声等噪声 → 发现玩家。</summary>
	private void OnNoiseHeard(Vector2 pos, float radius)
	{
		if (_aggroed) return;
		if (GlobalPosition.DistanceTo(pos) <= Mathf.Min(radius, EnemyData.HearingRange))
			SetAggroed(true);
	}

	private void SetAggroed(bool value)
	{
		if (_aggroed == value) return;
		_aggroed = value;
		ApplyStateVisual();
	}

	private Color GetStateColor()
	{
		// 未发现：暗淡（待机）；发现：明亮（被惊动）
		return _aggroed ? new Color(1, 1, 1, 1) : new Color(0.55f, 0.55f, 0.55f, 1);
	}

	private void ApplyStateVisual()
	{
		_visual.Modulate = GetStateColor();
	}

	private void UpdateChaseVelocity(float delta)
	{
		// 能直视玩家：直接走直线
		if (HasLineOfSightToPlayer())
		{
			Velocity = GlobalPosition.DirectionTo(_player.GlobalPosition) * EnemyData.MoveSpeed;
			return;
		}

		// 被墙挡住：用 A* 路径绕行（节流重算）
		_repathTimer -= delta;
		if (_repathTimer <= 0.0f || _pathIndex >= _path.Length)
		{
			RequestPath();
			_repathTimer = 0.3f;
		}

		if (_pathIndex < _path.Length)
		{
			var target = _path[_pathIndex];
			Velocity = GlobalPosition.DirectionTo(target) * EnemyData.MoveSpeed;
			if (GlobalPosition.DistanceTo(target) < 8.0f)
				_pathIndex++;
		}
		else
		{
			Velocity = GlobalPosition.DirectionTo(_player.GlobalPosition) * EnemyData.MoveSpeed;
		}
	}

	/// <summary>分离力：远离靠得太近的其它丧尸（越近推力越大）。</summary>
	private void ApplySeparation()
	{
		var push = Vector2.Zero;
		foreach (var node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node == this || !IsInstanceValid(node)) continue;
			if (node is not Node2D other) continue;
			var offset = GlobalPosition - other.GlobalPosition;
			float dist = offset.Length();
			if (dist < SeparationRadius && dist > 0.001f)
				push += offset.Normalized() * (SeparationRadius - dist) / SeparationRadius;
		}
		Velocity += push * SeparationStrength;
	}

	private void RequestPath()
	{
		if (_player == null) return;
		_path = NavigationManager.Instance.FindPath(GlobalPosition, _player.GlobalPosition);
		// 跳过起点（自己所在格子），避免每次重算都先朝自己格子中心走
		_pathIndex = _path.Length > 1 ? 1 : 0;
	}

	/// <summary>射线检测：敌人到玩家之间是否无障碍（可直视）。</summary>
	private bool HasLineOfSightToPlayer()
	{
		if (_player == null) return false;
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, _player.GlobalPosition, 1);  // 只检测墙体层，不被其它丧尸挡视线
		var result = spaceState.IntersectRay(query);
		return result.Count == 0;
	}

	private void TryAttack()
	{
		if (_attackCooldown > 0.0f) return;
		_attackCooldown = EnemyData.AttackCooldown;

		// ★ 播一次挥砍动画（7 帧 ÷ 8fps ≈ 0.88 秒）
		//   这里**必须清掉动画缓存**，否则"同方向连续攻击"会被 SetAnim 的
		//   "参数没变就不重来"判断吞掉，表现成"只有转身才触发攻击动作"。
		_attackAnimLeft = 7.0f / AnimFps;
		_animDir = "";
		_animName = "";
		_oneShot = false;

		if (_player is IDamageable damageable)
			damageable.TakeDamage(EnemyData.Damage);
	}

	public void TakeDamage(float amount)
	{
		_hitFlashLeft = HitFlashTime;
		_stunLeft = StunTime;   // 闪红

		// ★ 击退：朝**远离玩家**的方向推 5px
		if (_player != null)
		{
			var away = GlobalPosition - _player.GlobalPosition;
			if (away.LengthSquared() > 1.0f)
				_knockback = away.Normalized() * KnockbackDistance;
		}
		Health -= amount;
		FlashHit();
		if (Health <= 0.0f)
			Die();
	}

	private void Die()
	{
		SpawnCorpse();
		DropLoot();
		QueueFree();
	}

	/// <summary>死亡掉落：在脚下生成一个**战利品容器**（要先搜才能看到里面）。</summary>
	private void DropLoot()
	{
		var game = GetTree().GetFirstNodeInGroup("game") as Game;
		if (game?.Items == null || game.Grid == null) return;

		var items = ZombieLoot.Roll();
		if (items.Count == 0) return;

		game.Items.SpawnLootSpot(game.Grid.WorldToCell(GlobalPosition), items);
	}

	private void FlashHit()
	{
		_visual.Modulate = new Color(1.0f, 0.3f, 0.3f, 1.0f);
		var tween = CreateTween();
		tween.TweenProperty(_visual, "modulate", GetStateColor(), 0.15);
	}

	/// <summary>死亡时在地上留一张尸体贴图（取死亡动画最后一帧）。</summary>
	private void SpawnCorpse()
	{
		var game = GetTree().GetFirstNodeInGroup("game") as Game;
		if (game?.Grid == null) return;

		var tex = GD.Load<Texture2D>("res://assets/sprites/objects/corpse_axe.png");
		if (tex == null) return;

		var cell = game.Grid.WorldToCell(GlobalPosition);
		var centre = game.Grid.CellToWorld(cell) + Vector2.One * (Game.CellSize * 0.5f);

		// 尸体贴图是 16px 时代的：世界格 32px，2× 放大并按可视化高度贴底
		const float corpseScale = 2.0f;
		float drawHeight = tex.GetHeight() * corpseScale;

		var corpse = new Sprite2D
		{
			Name = "Corpse",
			Texture = tex,
			Centered = true,
			Scale = new Vector2(corpseScale, corpseScale),
			Position = centre + new Vector2(0, (Game.CellSize - drawHeight) * 0.5f),
		};

		game.AddChild(corpse);
		game.RegisterLootSprite(cell, corpse);
	}

}
