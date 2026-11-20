using Godot;
using System.Collections.Generic;

/// <summary>
/// 树碰撞箱回归测试：**贴图怎么画，碰撞就怎么挡**。
///
/// 复现的 bug：`WorldProps` 把 32×80 的树贴图当"16px 素材"又 ×2，
/// 还把 <see cref="StaticBody2D"/> 挂在**被缩放的精灵**下面 ——
/// 碰撞圆心与半径一起被放大 2 倍，并整体偏到格子右下方：
/// 人站在树冠前被空气挡住，树干旁边反而能穿过去。
///
/// 断言（抽查 12 棵）：
/// 1. 精灵**不缩放**（32px 素材 1:1；AGENTS 规则 7 只针对 16px 素材）；
/// 2. 精灵**底边贴格子底**、**水平居中于格子**；
/// 3. 碰撞落在精灵**下部**（树干根部）且水平居中；
/// 4. 碰撞宽度 ≤ 大半格（不能连树冠一起当墙）；
/// 5. 碰撞世界矩形不越出贴图矩形。
///
/// Godot --headless --path . res://tests/TreeCollisionSelfTest.tscn
/// </summary>
public partial class TreeCollisionSelfTest : Node
{
    private int _pass;
    private int _fail;

    public override async void _Ready()
    {
        const float cell = 32.0f;
        var origin = new Vector2(-960.0f, -640.0f);

        var props = new WorldProps { Name = "WorldProps" };
        AddChild(props);

        // Trees 列表由 MapLayout.Build() 填（Game 里就是这么调的）
        MapLayout.Build();
        props.Build(origin, cell, 12345);

        var trees = Collect(props);
        Check("建出了树", trees.Count > 0, $"{trees.Count} 棵");
        if (trees.Count == 0) { Finish(); return; }

        int sampled = Mathf.Min(trees.Count, 12);
        for (int i = 0; i < sampled; i++)
        {
            var (sprite, shape) = trees[i];
            var spriteRect = WorldRect(sprite);
            var shapeRect = WorldRect(shape);
            var cellRect = CellRectOf(sprite, origin, cell);
            string tag = $"树{i}";

            Check($"{tag} 精灵不缩放",
                Mathf.IsEqualApprox(sprite.Scale.X, 1.0f) && Mathf.IsEqualApprox(sprite.Scale.Y, 1.0f),
                $"scale={sprite.Scale}");

            Check($"{tag} 底边贴格子底", Mathf.Abs(spriteRect.End.Y - cellRect.End.Y) <= 1.0f,
                $"树底 {spriteRect.End.Y:0.#} vs 格子底 {cellRect.End.Y:0.#}");

            Check($"{tag} 水平居中于格子", Mathf.Abs(CenterX(spriteRect) - CenterX(cellRect)) <= 1.5f,
                $"树心 {CenterX(spriteRect):0.#} vs 格子心 {CenterX(cellRect):0.#}");

            Check($"{tag} 碰撞在树干",
                shapeRect.GetCenter().Y > spriteRect.Position.Y + spriteRect.Size.Y * 0.75f,
                $"碰撞心Y {shapeRect.GetCenter().Y:0.#} 树底 {spriteRect.End.Y:0.#}");

            Check($"{tag} 碰撞水平居中于贴图", Mathf.Abs(CenterX(shapeRect) - CenterX(spriteRect)) <= 2.0f,
                $"碰撞心 {CenterX(shapeRect):0.#} vs 树心 {CenterX(spriteRect):0.#}");

            Check($"{tag} 碰撞宽度合理", shapeRect.Size.X <= cell * 0.6f,
                $"宽 {shapeRect.Size.X:0.#}（上限 {cell * 0.6f:0.#}）");

            Check($"{tag} 碰撞不越出贴图",
                shapeRect.Position.X >= spriteRect.Position.X - 1.0f &&
                shapeRect.End.X <= spriteRect.End.X + 1.0f &&
                shapeRect.Position.Y >= spriteRect.Position.Y - 1.0f &&
                shapeRect.End.Y <= spriteRect.End.Y + 1.0f,
                $"碰撞 {shapeRect} / 贴图 {spriteRect}");
        }

        // 物理层再验一次：树干中心必须真的挂在 layer1 的空间里（不然玩家会直接穿过去）
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        VerifyTrunkInSpace(trees);
        await VerifyPlayerBlocked(trees[0].Sprite, trees[0].Shape);
        CheckPlayerColliderAlignsArt();

        Finish();
    }

    /// <summary>
    /// 玩家碰撞盒**顶端**必须对准"看得见的头顶"，而不是精灵矩形顶。
    ///
    /// 角色贴图上方有 2px 透明留白（×2 缩放 = 4px）。碰撞盒顶若与矩形顶齐平，
    /// 玩家会被树挡在**离树干 4px**的地方 —— 看起来就是"树下方有一小条走不了路"。
    /// 期望：碰撞盒顶比精灵矩形顶低 2~6px。
    /// </summary>
    private void CheckPlayerColliderAlignsArt()
    {
        var scene = GD.Load<PackedScene>("res://scenes/player.tscn");
        if (scene == null) { Check("加载 player.tscn", false, "加载失败"); return; }

        var player = scene.Instantiate<Player>();
        AddChild(player);

        var shape = player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        var sprite = player.GetNodeOrNull<Sprite2D>("Sprite");
        if (shape?.Shape is not RectangleShape2D box || sprite == null)
        {
            Check("玩家碰撞盒对准可见头顶", false, "找不到碰撞盒或精灵");
            return;
        }

        float spriteLocalTop = sprite.GetRect().Position.Y * sprite.Scale.Y + sprite.Position.Y;
        float colliderLocalTop = shape.Position.Y - box.Size.Y * 0.5f;
        float delta = colliderLocalTop - spriteLocalTop;

        Check("玩家碰撞盒对准可见头顶", delta >= 2.0f && delta <= 6.0f,
            $"碰撞盒顶比精灵顶低 {delta:0.#}px（期望 2~6；0 = 会留 4px 幽灵间隙）");

        player.QueueFree();
    }

    /// <summary>
    /// 行为验证：一个和玩家同尺寸（28×28、mask=1）的刚体从树下方往上走，
    /// 必须**停在树干下方**，而不是穿过整棵树。
    /// </summary>
    private async System.Threading.Tasks.Task VerifyPlayerBlocked(Sprite2D sprite, CollisionShape2D shape)
    {
        var shapeRect = WorldRect(shape);
        var trunkBottom = shapeRect.End.Y;

        var body = new CharacterBody2D { Name = "FakePlayer", CollisionLayer = 2, CollisionMask = 1 };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(28, 28) } });
        AddChild(body);

        body.GlobalPosition = new Vector2(shapeRect.GetCenter().X, trunkBottom + 60.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        for (int i = 0; i < 40; i++)
        {
            body.Velocity = new Vector2(0.0f, -160.0f);   // 朝树走
            body.MoveAndSlide();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        float gap = body.GlobalPosition.Y - trunkBottom;
        Check($"{sprite.Name} 挡住玩家（不穿树）", gap > 10.0f, $"停在树干下方 {gap:0.#}px");

        body.QueueFree();
    }

    /// <summary>树干中心点查询：必须命中一个 layer1 的静态体（玩家 CollisionMask 含 layer1）。</summary>
    private void VerifyTrunkInSpace(List<(Sprite2D Sprite, CollisionShape2D Shape)> trees)
    {
        var space = GetTree().Root.World2D.DirectSpaceState;

        foreach (var (sprite, shape) in trees)
        {
            var center = WorldRect(shape).GetCenter();
            var query = new PhysicsPointQueryParameters2D
            {
                Position = center,
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = false,
            };

            var hits = space.IntersectPoint(query, 4);
            bool hitTrunk = false;
            foreach (var hit in hits)
            {
                if (hit["collider"].AsGodotObject() is StaticBody2D body &&
                    body.Name.ToString().StartsWith("Trunk_"))
                {
                    hitTrunk = true;
                    break;
                }
            }

            if (!hitTrunk)
            {
                Check($"{sprite.Name} 树干在物理空间里", false, $"点 {center} 未命中 Trunk_*");
                return;      // 一次失败就够，不必刷屏
            }
        }

        Check("所有树的树干都在物理空间里（layer1）", true, $"{trees.Count} 棵");
    }

    /// <summary>把每棵树和它的碰撞形状配成一对（碰撞可能挂在精灵下，也可能挂在道具层下）。</summary>
    private static List<(Sprite2D Sprite, CollisionShape2D Shape)> Collect(Node root)
    {
        var all = new List<Node>();
        Walk(root, all);

        var result = new List<(Sprite2D, CollisionShape2D)>();
        foreach (var node in all)
        {
            if (node is not Sprite2D sprite || !sprite.Name.ToString().StartsWith("Tree_")) continue;

            var shape = FindShapeUnder(sprite);
            if (shape == null)
            {
                // 修复后的做法：树干挂在道具层，按名字配对 Tree_x_y ↔ Trunk_x_y
                string suffix = sprite.Name.ToString()["Tree_".Length..];
                shape = FindShapeByName(root, "Trunk_" + suffix);
            }
            if (shape != null) result.Add((sprite, shape));
        }
        return result;
    }

    private static CollisionShape2D FindShapeUnder(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CollisionShape2D shape) return shape;
            if (child is Node nested)
            {
                var found = FindShapeUnder(nested);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static CollisionShape2D FindShapeByName(Node root, string bodyName)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is StaticBody2D body && body.Name.ToString() == bodyName)
                foreach (var shapeChild in body.GetChildren())
                    if (shapeChild is CollisionShape2D shape) return shape;

            if (child is Node nested)
            {
                var found = FindShapeByName(nested, bodyName);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static void Walk(Node node, List<Node> outList)
    {
        outList.Add(node);
        foreach (var child in node.GetChildren()) Walk(child, outList);
    }

    /// <summary>贴图在世界里的实际矩形（Offset / Centered / Scale 都算进去）。</summary>
    private static Rect2 WorldRect(Sprite2D sprite)
    {
        var local = sprite.GetRect();
        var t = sprite.GetGlobalTransform();
        return Aabb(t * local.Position, t * (local.Position + local.Size));
    }

    /// <summary>碰撞形状在世界里的包围盒（含父节点缩放）。</summary>
    private static Rect2 WorldRect(CollisionShape2D shape)
    {
        var local = shape.Shape.GetRect();
        var t = shape.GetGlobalTransform();
        return Aabb(t * local.Position, t * (local.Position + local.Size));
    }

    private static Rect2 Aabb(Vector2 a, Vector2 b) => new Rect2(a, Vector2.Zero).Expand(b);

    private static Rect2 CellRectOf(Sprite2D sprite, Vector2 origin, float cell)
    {
        // 格子直接把名字里的 x_y 解析出来（修复后 Position 是贴图左上角，不能拿来反推格子）
        string suffix = sprite.Name.ToString()["Tree_".Length..];
        var parts = suffix.Split('_');
        if (parts.Length == 2 && int.TryParse(parts[0], out int gx) && int.TryParse(parts[1], out int gy))
            return new Rect2(origin + new Vector2(gx, gy) * cell, new Vector2(cell, cell));

        var index = (sprite.Position - origin) / cell;
        var topLeft = origin + new Vector2(Mathf.Floor(index.X), Mathf.Floor(index.Y)) * cell;
        return new Rect2(topLeft, new Vector2(cell, cell));
    }

    private static float CenterX(Rect2 r) => r.Position.X + r.Size.X * 0.5f;

    private void Finish()
    {
        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private void Check(string name, bool ok, string detail)
    {
        if (ok) _pass++;
        else _fail++;
        GD.Print(ok ? $"  [PASS] {name}" : $"  [FAIL] {name}  （{detail}）");
    }
}
